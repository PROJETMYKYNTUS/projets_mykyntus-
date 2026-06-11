import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const base = '/api/prime/supervisor-fiches';

export interface SupervisorPrimeFicheDto {
  id: string;
  supervisorUserId: string;
  poleId: string | null;
  period: string;
  templateId: string;
  templateDisplayName: string;
  templateFormatVersion: number;
  status: string;
  schemaJson: string;
  saisieJson: string;
  computedJson: string | null;
  createdAt: string;
  validatedAt: string | null;
}

export interface CreateSupervisorPrimeFicheRequest {
  supervisorUserId: string;
  poleId?: string | null;
  period: string;
  templateId: string;
  templateDisplayName: string;
  templateFormatVersion: number;
  schemaJson: string;
  saisieJson: string;
  computedJson?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PrimeFicheApiService {
  private readonly http = inject(HttpClient);

  createDraft(body: CreateSupervisorPrimeFicheRequest): Observable<SupervisorPrimeFicheDto> {
    return this.http.post<SupervisorPrimeFicheDto>(base, body);
  }

  updateSaisie(id: string, body: { saisieJson: string; computedJson?: string | null }): Observable<SupervisorPrimeFicheDto> {
    return this.http.put<SupervisorPrimeFicheDto>(`${base}/${id}/saisie`, body);
  }

  validate(id: string): Observable<SupervisorPrimeFicheDto> {
    return this.http.post<SupervisorPrimeFicheDto>(`${base}/${id}/validate`, {});
  }

  list(supervisorUserId: string, period?: string): Observable<SupervisorPrimeFicheDto[]> {
    const q = period ? `?supervisorUserId=${encodeURIComponent(supervisorUserId)}&period=${encodeURIComponent(period)}` : `?supervisorUserId=${encodeURIComponent(supervisorUserId)}`;
    return this.http.get<SupervisorPrimeFicheDto[]>(`${base}${q}`);
  }
}
