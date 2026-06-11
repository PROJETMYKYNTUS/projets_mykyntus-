// features/contract/services/contract.service.ts

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export type ContractType   = 'CDI' | 'CDD' | 'Stage' | 'ANAPEC';
export type ContractStatus = "En période d'essai" | 'Actif' | 'Expiré' | 'Résilié';

export interface ContractResponse {
  id: number;
  userId: number;
  userFullName: string;
  type: ContractType;
  status: ContractStatus;
  startDate: string;
  endDate?: string;
  probationEndDate?: string;
  joursRestants?: number;
  joursRestantsPeriodeEssai?: number;
  isAlertActive: boolean;
  alertThresholdDays: number;
  notes?: string;
  createdAt: string;
}

export interface CreateContractDto {
  userId: number;
  type: ContractType;
  startDate: string;
  endDate?: string;
  probationDays?: number;
  alertThresholdDays?: number;
  notes?: string;
}

export interface UpdateContractDto {
  type?: ContractType;
  status?: string;
  endDate?: string;
  probationDays?: number;
  alertThresholdDays?: number;
  notes?: string;
}

export interface ContractNotification {
  id: number;
  contractId: number;
  userId: number;
  userFullName: string;
  type: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ContractService {

  private base = `${environment.apiUrl}/contract`;

  constructor(private http: HttpClient) {}

  // ── Contrats ──
  getAll(): Observable<ContractResponse[]> {
  console.log('URL appelée:', this.base); // ← ajoute ça
  return this.http.get<ContractResponse[]>(this.base);
}

  getById(id: number): Observable<ContractResponse> {
    return this.http.get<ContractResponse>(`${this.base}/${id}`);
  }

  getByUser(userId: number): Observable<ContractResponse[]> {
    return this.http.get<ContractResponse[]>(`${this.base}/user/${userId}`);
  }

  create(dto: CreateContractDto): Observable<ContractResponse> {
    return this.http.post<ContractResponse>(this.base, dto);
  }

  update(id: number, dto: UpdateContractDto): Observable<ContractResponse> {
    return this.http.put<ContractResponse>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  // ── Notifications ──
  getNotifications(): Observable<ContractNotification[]> {
    return this.http.get<ContractNotification[]>(`${this.base}/notifications`);
  }

  getNotificationsCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.base}/notifications/count`);
  }

  markAsRead(id: number): Observable<void> {
    return this.http.put<void>(`${this.base}/notifications/${id}/read`, {});
  }

  markAllAsRead(): Observable<void> {
    return this.http.put<void>(`${this.base}/notifications/read-all`, {});
  }
}