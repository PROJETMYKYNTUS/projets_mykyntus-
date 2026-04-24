// src/app/core/services/reclamation.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Reclamation, ReclamationDetail, PaginatedResult, SatisfactionReport,
  CreateReclamationPayload, UpdateStatusPayload, AssignPayload,
  PrioriserPayload, SatisfactionPayload
} from '../models/reclamation.model';

@Injectable({ providedIn: 'root' })
export class ReclamationService {

  private base = `${environment.apiUrl}/reclamation`;

  constructor(private http: HttpClient) {}

  // ── Tous les rôles ──────────────────────────────
  soumettre(payload: CreateReclamationPayload): Observable<Reclamation> {
    return this.http.post<Reclamation>(this.base, payload);
  }

  getMesDemandes(page = 1, pageSize = 10): Observable<PaginatedResult<Reclamation>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PaginatedResult<Reclamation>>(`${this.base}/mes-demandes`, { params });
  }

  getById(id: number): Observable<ReclamationDetail> {
    return this.http.get<ReclamationDetail>(`${this.base}/${id}`);
  }

  noteSatisfaction(id: number, payload: SatisfactionPayload): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/satisfaction`, payload);
  }

  // ── RH, Manager, RP, Admin ──────────────────────
  getAll(page = 1, pageSize = 20, status?: string, priorite?: string): Observable<PaginatedResult<Reclamation>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status)   params = params.set('status', status);
    if (priorite) params = params.set('priorite', priorite);
    return this.http.get<PaginatedResult<Reclamation>>(this.base, { params });
  }

  traiter(id: number, payload: UpdateStatusPayload): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/traiter`, payload);
  }

  assigner(id: number, payload: AssignPayload): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/assigner`, payload);
  }

  prioriser(id: number, payload: PrioriserPayload): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/prioriser`, payload);
  }

  // ── RH, Manager, RP, Admin, Audit ──────────────
  getReporting(from?: string, to?: string): Observable<SatisfactionReport> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to)   params = params.set('to', to);
    return this.http.get<SatisfactionReport>(`${this.base}/reporting`, { params });
  }

  // ── RP, Admin, Audit ────────────────────────────
  getHistorique(reclamationId?: number, page = 1, pageSize = 20): Observable<PaginatedResult<ReclamationDetail>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (reclamationId) params = params.set('reclamationId', reclamationId);
    return this.http.get<PaginatedResult<ReclamationDetail>>(`${this.base}/historique`, { params });
  }
}