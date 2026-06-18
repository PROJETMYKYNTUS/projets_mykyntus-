import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, tap, retry, delay } from 'rxjs/operators';  // ← ajouter retry
import { Floor } from '../models/floor.model';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class FloorService {
  private apiUrl = `${environment.apiUrl}/floor`;

  constructor(private http: HttpClient) {
    console.log('FloorService - API URL:', this.apiUrl);
  }

  getAllFloors(): Observable<Floor[]> {
  console.log('→ Appel getAllFloors à:', new Date().toISOString());
  return this.http.get<Floor[]>(this.apiUrl).pipe(
    retry({ count: 3, delay: 2000 }),
    tap(data => console.log('← Floors reçus:', data.length, 'étages', data)),
    catchError(err => {
      console.error('Erreur getAllFloors:', err);
      return throwError(() => err);
    })
  );
}

  getFloorById(id: number): Observable<Floor> {
    return this.http.get<Floor>(`${this.apiUrl}/${id}`);
  }

  createFloor(dto: any): Observable<Floor> {
    return this.http.post<Floor>(this.apiUrl, dto);
  }

  updateFloor(id: number, dto: any): Observable<Floor> {
    return this.http.put<Floor>(`${this.apiUrl}/${id}`, dto);
  }

  deleteFloor(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}