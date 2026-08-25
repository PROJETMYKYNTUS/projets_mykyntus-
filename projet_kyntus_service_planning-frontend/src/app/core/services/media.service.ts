import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, from, map, shareReplay, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';

export type MediaOwnerType = 'Orphan' | 'Newsletter' | 'Reclamation' | 'Proposition' | 'TicketComment';
export type MediaKind = 'Image' | 'Video' | 'Document';

export interface MediaAsset {
  id: number;
  ownerType: string;
  ownerId?: number | null;
  kind: MediaKind | string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sortOrder: number;
  url: string;
  createdAt: string;
}

export interface TicketComment {
  id: number;
  ownerType: string;
  ownerId: number;
  authorId: string;
  authorNom: string;
  text: string;
  createdAt: string;
  media: MediaAsset[];
}

@Injectable({ providedIn: 'root' })
export class MediaService {
  private base = `${environment.apiUrl}/media`;
  private blobCache = new Map<number, Observable<string>>();

  constructor(private http: HttpClient) {}

  upload(file: File): Observable<MediaAsset> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<MediaAsset>(this.base, form);
  }

  listByOwner(ownerType: MediaOwnerType, ownerId: number): Observable<MediaAsset[]> {
    const params = new HttpParams()
      .set('ownerType', ownerType)
      .set('ownerId', ownerId);
    return this.http.get<MediaAsset[]>(`${this.base}/by-owner`, { params });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Authenticated blob URL for img/video (Bearer via interceptor). */
  blobUrl(id: number): Observable<string> {
    let cached = this.blobCache.get(id);
    if (!cached) {
      cached = this.http.get(`${this.base}/${id}`, { responseType: 'blob' }).pipe(
        map(blob => URL.createObjectURL(blob)),
        shareReplay(1)
      );
      this.blobCache.set(id, cached);
    }
    return cached;
  }

  listComments(ownerType: 'Reclamation' | 'Proposition', ownerId: number): Observable<TicketComment[]> {
    const params = new HttpParams()
      .set('ownerType', ownerType)
      .set('ownerId', ownerId);
    return this.http.get<TicketComment[]>(`${this.base}/comments`, { params });
  }

  addComment(
    ownerType: 'Reclamation' | 'Proposition',
    ownerId: number,
    text: string,
    mediaIds?: number[]
  ): Observable<TicketComment> {
    const params = new HttpParams()
      .set('ownerType', ownerType)
      .set('ownerId', ownerId);
    return this.http.post<TicketComment>(`${this.base}/comments`, { text, mediaIds }, { params });
  }

  uploadMany(files: File[]): Observable<MediaAsset[]> {
    return from(files).pipe(
      switchMap(f => this.upload(f)),
      // collect manually via caller for progress; keep simple sequential API here
    ) as unknown as Observable<MediaAsset[]>;
  }
}
