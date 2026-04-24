// src/app/core/services/proposition.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Proposition, PropositionDetail, PaginatedResult, SatisfactionReport,
  CreatePropositionPayload, UpdateStatusPayload, AssignPayload,
  PrioriserPayload, SatisfactionPayload
} from '../models/reclamation.model';

@Injectable({ providedIn: 'root' })
export class PropositionService {

  private base = `${environment.apiUrl}/proposition`;

  constructor(private http: HttpClient) {}

  // ── Tous les rôles ──────────────────────────────
  soumettre(payload: CreatePropositionPayload): Observable<Proposition> {
    return this.http.post<Proposition>(this.base, payload);
  }

  getMesDemandes(page = 1, pageSize = 10): Observable<PaginatedResult<Proposition>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PaginatedResult<Proposition>>(`${this.base}/mes-demandes`, { params });
  }

  getById(id: number): Observable<PropositionDetail> {
    return this.http.get<PropositionDetail>(`${this.base}/${id}`);
  }

  noteSatisfaction(id: number, payload: SatisfactionPayload): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/satisfaction`, payload);
  }

  // ── RH, Manager, RP, Admin ──────────────────────
  getAll(page = 1, pageSize = 20, status?: string): Observable<PaginatedResult<Proposition>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<PaginatedResult<Proposition>>(this.base, { params });
  }

  evaluer(id: number, payload: UpdateStatusPayload): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/evaluer`, payload);
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
  getHistorique(propositionId?: number, page = 1, pageSize = 20): Observable<PaginatedResult<PropositionDetail>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (propositionId) params = params.set('propositionId', propositionId);
    return this.http.get<PaginatedResult<PropositionDetail>>(`${this.base}/historique`, { params });
  }
}