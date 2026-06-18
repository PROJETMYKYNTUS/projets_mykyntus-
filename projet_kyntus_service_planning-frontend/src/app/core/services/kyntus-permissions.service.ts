import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface EffectivePermissions {
  subjectId: string;
  role: string;
  permissions: string[];
}

@Injectable({ providedIn: 'root' })
export class KyntusPermissionsService {
  private readonly http = inject(HttpClient);
  private cache$?: Observable<EffectivePermissions>;

  loadEffectivePermissions(force = false): Observable<EffectivePermissions> {
    if (!force && this.cache$) return this.cache$;
    this.cache$ = this.http
      .get<EffectivePermissions>(`${environment.apiUrl}/iam/effective-permissions`)
      .pipe(
        catchError(() =>
          of({ subjectId: '', role: '', permissions: [] as string[] }),
        ),
        shareReplay(1),
      );
    return this.cache$;
  }

  can(action: string, scope: string, permissions: readonly string[]): boolean {
    if (permissions.includes('*:Global')) return true;
    return permissions.includes(`${action}:${scope}`);
  }
}
