import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class PlanningService {

  private api = environment.apiUrl; // http://localhost:5000/api

  constructor(private http: HttpClient) {}

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token')
      || localStorage.getItem('access_token')
      || '';
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

  getEquipePlannings(authUserId: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.api}/planning/equipe?authUserId=${authUserId}`,
      { headers: this.getHeaders() }
    );
  }

  getPlanningById(id: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/${id}`,
      { headers: this.getHeaders() }
    );
  }

  // ── Demandes de changement ─────────────────
  createChangeRequest(authUserId: number, dto: {
    currentAssignmentId: number;
    reason: string;
    proposedSwapUserId: number;
  }): Observable<any> {
    return this.http.post(
      `${this.api}/planning/change-requests?authUserId=${authUserId}`,
      dto,
      { headers: this.getHeaders() }
    );
  }

  getMyChangeRequests(authUserId: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.api}/planning/change-requests/my?authUserId=${authUserId}`,
      { headers: this.getHeaders() }
    );
  }

  getSwapCandidates(assignmentId: number, authUserId: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.api}/planning/change-requests/swap-candidates?assignmentId=${assignmentId}&authUserId=${authUserId}`,
      { headers: this.getHeaders() }
    );
  }

  cancelChangeRequest(id: number, authUserId: number): Observable<any> {
    return this.http.post(
      `${this.api}/planning/change-requests/${id}/cancel?authUserId=${authUserId}`,
      {},
      { headers: this.getHeaders() }
    );
  }

  partnerAcceptChangeRequest(id: number, authUserId: number): Observable<any> {
    return this.http.post(
      `${this.api}/planning/change-requests/${id}/partner-accept?authUserId=${authUserId}`,
      {},
      { headers: this.getHeaders() }
    );
  }

  partnerRejectChangeRequest(id: number, authUserId: number, reason?: string): Observable<any> {
    return this.http.post(
      `${this.api}/planning/change-requests/${id}/partner-reject?authUserId=${authUserId}`,
      { reason: reason || null },
      { headers: this.getHeaders() }
    );
  }

  // ── Demandes exceptionnelles ─────────────────
  createExceptionalRequest(
    authUserId: number,
    data: {
      requestedDate: string;
      requestedShiftTemplateId: number;
      reason: string;
      file?: File | null;
    },
  ): Observable<any> {
    const fd = new FormData();
    fd.append('requestedDate', data.requestedDate);
    fd.append('requestedShiftTemplateId', String(data.requestedShiftTemplateId));
    fd.append('reason', data.reason);
    if (data.file) {
      fd.append('file', data.file, data.file.name);
    }
    return this.http.post(
      `${this.api}/planning/exceptional-requests?authUserId=${authUserId}`,
      fd,
      { headers: this.getHeaders() },
    );
  }

  getMyExceptionalRequests(authUserId: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.api}/planning/exceptional-requests/my?authUserId=${authUserId}`,
      { headers: this.getHeaders() },
    );
  }

  getExceptionalQuota(authUserId: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/exceptional-requests/quota?authUserId=${authUserId}`,
      { headers: this.getHeaders() },
    );
  }

  getExceptionalAvailableShifts(authUserId: number): Observable<any[]> {
    return this.http.get<any[]>(
      `${this.api}/planning/exceptional-requests/available-shifts?authUserId=${authUserId}`,
      { headers: this.getHeaders() },
    );
  }

  getExceptionalTargetWeek(): Observable<any> {
    return this.http.get(
      `${this.api}/planning/exceptional-requests/target-week`,
      { headers: this.getHeaders() },
    );
  }

  cancelExceptionalRequest(id: number, authUserId: number): Observable<any> {
    return this.http.post(
      `${this.api}/planning/exceptional-requests/${id}/cancel?authUserId=${authUserId}`,
      {},
      { headers: this.getHeaders() },
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