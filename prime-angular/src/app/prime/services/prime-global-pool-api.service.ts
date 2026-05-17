import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const base = '/api/prime/global-pool';

export interface GlobalPoolInboxStepStatusDto {
  stepId: string;
  sortOrder: number;
  approverRole: string;
  isRequired: boolean;
  approvedAt?: string | null;
}

export interface GlobalPoolInboxItemDto {
  draftId: string;
  supervisorUserId: string;
  celluleId: string;
  period: string;
  hasFile: boolean;
  fileName?: string | null;
  uploadedAt?: string | null;
  managerApprovedAt?: string | null;
  rhApprovedAt?: string | null;
  comptaAckAt?: string | null;
  poolDistributionUnlocked: boolean;
  pendingActionForUser: boolean;
  stepStatuses?: GlobalPoolInboxStepStatusDto[] | null;
  suggestedApproveStepId?: string | null;
}

export interface CelluleDraftGlobalPoolStateDto {
  draftId: string;
  celluleId: string;
  period: string;
  hasFile: boolean;
  fileName?: string | null;
  uploadedAt?: string | null;
  managerApprovedAt?: string | null;
  rhApprovedAt?: string | null;
  comptaAckAt?: string | null;
  poolDistributionUnlocked: boolean;
}

const wfAdmin = '/api/prime/admin/global-pool-workflow';

export interface GlobalPoolWorkflowStepDto {
  id: string;
  sortOrder: number;
  approverRole: string;
  isRequired: boolean;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class PrimeGlobalPoolApiService {
  private readonly http = inject(HttpClient);

  inbox(userId: string): Observable<GlobalPoolInboxItemDto[]> {
    const params = new HttpParams().set('userId', userId);
    return this.http.get<GlobalPoolInboxItemDto[]>(`${base}/inbox`, { params });
  }

  downloadExcel(draftId: string, userId: string): Observable<Blob> {
    const params = new HttpParams().set('userId', userId);
    return this.http.get(`${base}/${draftId}/excel`, { params, responseType: 'blob' });
  }

  approveManager(draftId: string, userId: string): Observable<CelluleDraftGlobalPoolStateDto> {
    return this.http.post<CelluleDraftGlobalPoolStateDto>(`${base}/${draftId}/approve-manager`, { userId });
  }

  approveRh(draftId: string, userId: string): Observable<CelluleDraftGlobalPoolStateDto> {
    return this.http.post<CelluleDraftGlobalPoolStateDto>(`${base}/${draftId}/approve-rh`, { userId });
  }

  ackCompta(draftId: string, userId: string): Observable<CelluleDraftGlobalPoolStateDto> {
    return this.http.post<CelluleDraftGlobalPoolStateDto>(`${base}/${draftId}/ack-compta`, { userId });
  }

  listGlobalPoolWorkflowSteps(): Observable<GlobalPoolWorkflowStepDto[]> {
    return this.http.get<GlobalPoolWorkflowStepDto[]>(`${wfAdmin}/steps`);
  }

  approveStep(
    draftId: string,
    body: { userId: string; stepId: string; role?: string },
  ): Observable<CelluleDraftGlobalPoolStateDto> {
    return this.http.post<CelluleDraftGlobalPoolStateDto>(`${base}/${draftId}/approve-step`, body);
  }
}
