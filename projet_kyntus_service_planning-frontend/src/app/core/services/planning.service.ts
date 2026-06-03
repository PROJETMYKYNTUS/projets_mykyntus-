import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// Interface ajoutée
export interface WeeklyPlanningResponse {
  id: number;
  weekCode: string;
  weekStartDate: string;
  subServiceName: string;
  status: string;
  assignments: any[];
}

@Injectable({ providedIn: 'root' })
export class PlanningService {

  private api = environment.apiUrl;

  constructor(private http: HttpClient) {}
private getHeaders(): HttpHeaders {
const token = localStorage.getItem('token')      // ← alignez ici
            || localStorage.getItem('access_token')
            || '';
  return new HttpHeaders({ Authorization: `Bearer ${token}` });
}
  getMyPlanning(weekCode: string, userId: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/my/${weekCode}?userId=${userId}`,
      { headers: this.getHeaders() }
    );
  }

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

  // ✅ Correction : this.base → this.api
  getEquipePlannings(authUserId: number): Observable<WeeklyPlanningResponse[]> {
    return this.http.get<WeeklyPlanningResponse[]>(
      `${this.api}/planning/equipe?authUserId=${authUserId}`,
      { headers: this.getHeaders() }
    );
  }

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