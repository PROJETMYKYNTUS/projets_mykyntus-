import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class PlanningService {

  private api = environment.apiUrl; // http://localhost:5000/api

  constructor(private http: HttpClient) {}

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('access_token');
    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }

  // ── Employee ──────────────────────────────
  getMyPlanning(weekCode: string, userId: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/my/${weekCode}?userId=${userId}`,
      { headers: this.getHeaders() }
    );
  }
  // Ajouter cette méthode
getMyCurrentPlanning(userId: number): Observable<any> {
  return this.http.get(
    `${this.api}/planning/my/current?userId=${userId}`,
    { headers: this.getHeaders() }
  );
}

  getMyHistory(userId: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/my/history?userId=${userId}`,
      { headers: this.getHeaders() }
    );
  }

  // ── Admin ─────────────────────────────────
  generatePlanning(dto: any): Observable<any> {
    return this.http.post(
      `${this.api}/planning/generate-from-config`, dto,
      { headers: this.getHeaders() }
    );
  }

  publishPlanning(id: number, validatorId: number): Observable<any> {
    return this.http.post(
      `${this.api}/planning/${id}/publish?validatorId=${validatorId}`, {},
      { headers: this.getHeaders() }
    );
  }
}