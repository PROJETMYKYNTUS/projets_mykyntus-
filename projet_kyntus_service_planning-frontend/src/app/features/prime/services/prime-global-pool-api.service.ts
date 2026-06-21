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

export interface GlobalPoolScopeSynthesisInboxItemDto {
  scopeSynthesisId: string;
  period: string;
  scopeType: string;
  scopeId: string;
  scopeDisplayName: string;
  hasFile: boolean;
  fileName?: string | null;
  generatedAt?: string | null;
  managerApprovedAt?: string | null;
  rhApprovedAt?: string | null;
  comptaAckAt?: string | null;
  poolDistributionUnlocked: boolean;
  pendingActionForUser: boolean;
  stepStatuses?: GlobalPoolInboxStepStatusDto[] | null;
  suggestedApproveStepId?: string | null;
  paymentState: 'Unpaid' | 'Partial' | 'Paid';
  paidLines: number;
  totalLines: number;
  rhDecidedLines: number;
  managerDecidedLines: number;
  approvedLines: number;
  rejectedLines: number;
}

export interface SupervisorSynthesisTrackingItemDto {
  ficheId: string;
  employeeId: string;
  employeeDisplayName: string;
  celluleName: string;
  serviceName: string;
  validationStatus: string;
  lineStatus?: string | null;
  rhDecision: 'Pending' | 'Approved' | 'Rejected';
  managerDecision: 'Pending' | 'Approved' | 'Rejected';
  rhRejectionReason?: string | null;
  managerRejectionReason?: string | null;
  rejectedByRole?: string | null;
  paymentStatus: 'Unpaid' | 'Paid';
  paidAt?: string | null;
  managerApproved: boolean;
  rhApproved: boolean;
  poolDistributionUnlocked: boolean;
  scopeSynthesisId?: string | null;
  scopeLabel?: string | null;
}

/** Vue pilote : sa fiche de prime (aperçu après double validation) + suivi du paiement. */
export interface EmployeePrimePaymentTrackingDto {
  ficheId: string;
  period: string;
  celluleName: string;
  serviceName: string;
  primeAmount?: number | null;
  challengeAmount?: number | null;
  totalAmount?: number | null;
  lineStatus?: string | null;
  paymentStatus: 'Unpaid' | 'Paid';
  paidAt?: string | null;
  paymentReference?: string | null;
  canViewFiche: boolean;
}

export interface GlobalPoolServiceReadinessDto {
  serviceId: string;
  serviceName: string;
  celluleId: string;
  poleId: string;
  ready: boolean;
  fichesTotal: number;
  fichesValidated: number;
  blockingReason?: string | null;
}

export interface GlobalPoolCelluleReadinessDto {
  celluleId: string;
  celluleName: string;
  poleId: string;
  ready: boolean;
  servicesReady: number;
  servicesTotal: number;
  blockingReason?: string | null;
}

export interface GlobalPoolPoleReadinessDto {
  poleId: string;
  poleName: string;
  ready: boolean;
  cellulesReady: number;
  cellulesTotal: number;
  blockingReason?: string | null;
}

export interface GlobalPoolReadinessDto {
  period: string;
  services: GlobalPoolServiceReadinessDto[];
  cellules: GlobalPoolCelluleReadinessDto[];
  poles: GlobalPoolPoleReadinessDto[];
}

export interface GlobalSynthesisLineDto {
  lineId?: string | null;
  ficheId: string;
  employeeId: string;
  employeeDisplayName: string;
  employeeRole: string;
  poleId: string;
  poleName: string;
  celluleId: string;
  celluleName: string;
  serviceId: string;
  serviceName: string;
  primeAmount?: number | null;
  challengeAmount?: number | null;
  totalAmount?: number | null;
  validationStatus: string;
  fillingStatus: string;
  lineStatus?: string | null;
  lineRejectionReason?: string | null;
  rhDecision: 'Pending' | 'Approved' | 'Rejected';
  managerDecision: 'Pending' | 'Approved' | 'Rejected';
  rhRejectionReason?: string | null;
  managerRejectionReason?: string | null;
  rejectedByRole?: string | null;
  paymentStatus: 'Unpaid' | 'Paid';
  paidAt?: string | null;
  paymentReference?: string | null;
  supervisorUserId: string;
  templateId: string;
}

export interface GlobalSynthesisSummaryDto {
  lineCount: number;
  totalPrime: number;
  totalChallenge: number;
  totalAmount: number;
  linesRejected: number;
}

export interface SynthesisTrackingFeedItemDto {
  id: string;
  ficheId: string;
  lineId?: string | null;
  at: string;
  action: string;
  fromStatus: string;
  toStatus: string;
  lineStatus?: string | null;
  actorUserId: string;
  actorRole: string;
  actorDisplayName?: string | null;
  comment?: string | null;
  employeeId: string;
  employeeDisplayName: string;
  period: string;
  celluleName: string;
  serviceName: string;
  currentValidationStatus: string;
  phase: string;
  scopeLabel?: string | null;
  lineRejectionReason?: string | null;
  rejectedByRole?: string | null;
}

export interface GlobalPoolSynthesisLineHistoryDto {
  id: string;
  lineId: string;
  at: string;
  action: string;
  actorUserId: string;
  actorRole: string;
  actorDisplayName?: string | null;
  comment?: string | null;
}

/** @deprecated brouillon superviseur */
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

@Injectable({ providedIn: 'root' })
export class PrimeGlobalPoolApiService {
  private readonly http = inject(HttpClient);

  listPeriods(): Observable<string[]> {
    return this.http.get<string[]>(`${base}/periods`);
  }

  readiness(period: string): Observable<GlobalPoolReadinessDto> {
    const params = new HttpParams().set('period', period);
    return this.http.get<GlobalPoolReadinessDto>(`${base}/readiness`, { params });
  }

  synthesisLines(
    period: string,
    scopeType: string,
    scopeId: string,
    scopeSynthesisId?: string,
    userId?: string,
  ): Observable<{ scopeSynthesisId?: string | null; validationReady: boolean; lines: GlobalSynthesisLineDto[] }> {
    let params = new HttpParams()
      .set('period', period)
      .set('scopeType', scopeType)
      .set('scopeId', scopeId);
    if (scopeSynthesisId) params = params.set('scopeSynthesisId', scopeSynthesisId);
    if (userId) params = params.set('userId', userId);
    return this.http.get<{ scopeSynthesisId?: string | null; validationReady: boolean; lines: GlobalSynthesisLineDto[] }>(
      `${base}/synthesis/lines`,
      { params },
    );
  }

  synthesisSummary(
    period: string,
    scopeType: string,
    scopeId: string,
    scopeSynthesisId?: string,
  ): Observable<GlobalSynthesisSummaryDto> {
    let params = new HttpParams()
      .set('period', period)
      .set('scopeType', scopeType)
      .set('scopeId', scopeId);
    if (scopeSynthesisId) params = params.set('scopeSynthesisId', scopeSynthesisId);
    return this.http.get<GlobalSynthesisSummaryDto>(`${base}/synthesis/summary`, { params });
  }

  generateSynthesis(body: {
    userId: string;
    period: string;
    scopeType: string;
    scopeId: string;
  }): Observable<{ scopeSynthesisId: string; fileName?: string }> {
    return this.http.post<{ scopeSynthesisId: string; fileName?: string }>(
      `${base}/synthesis/generate`,
      body,
    );
  }

  /** Prépare la synthèse à l'ouverture d'un périmètre prêt (sans réinitialiser les validations). */
  ensureSynthesis(body: {
    userId: string;
    period: string;
    scopeType: string;
    scopeId: string;
  }): Observable<{ scopeSynthesisId: string | null; ready: boolean; fileName?: string; error?: string }> {
    return this.http.post<{ scopeSynthesisId: string | null; ready: boolean; fileName?: string; error?: string }>(
      `${base}/synthesis/ensure`,
      body,
    );
  }

  setLinePayment(
    lineId: string,
    body: { userId: string; role?: string; paid: boolean; paidAt?: string; reference?: string },
  ): Observable<void> {
    return this.http.post<void>(`${base}/synthesis/lines/${lineId}/payment`, body);
  }

  payAll(
    scopeSynthesisId: string,
    body: { userId: string; role?: string; paidAt?: string; reference?: string },
  ): Observable<void> {
    return this.http.post<void>(`${base}/scope-synthesis/${scopeSynthesisId}/pay-all`, body);
  }

  /** Suivi pilote : avancement synthèse + paiement par employé pour les fiches d'un superviseur. */
  supervisorSynthesisTracking(
    supervisorUserId: string,
    period: string,
  ): Observable<SupervisorSynthesisTrackingItemDto[]> {
    const params = new HttpParams().set('supervisorUserId', supervisorUserId).set('period', period);
    return this.http.get<SupervisorSynthesisTrackingItemDto[]>(`${base}/supervisor-synthesis-tracking`, {
      params,
    });
  }

  scopeInbox(userId: string, role?: string): Observable<GlobalPoolScopeSynthesisInboxItemDto[]> {
    let params = new HttpParams().set('userId', userId);
    if (role?.trim()) params = params.set('role', role.trim());
    return this.http.get<GlobalPoolScopeSynthesisInboxItemDto[]>(`${base}/scope-inbox`, { params });
  }

  /** Vue pilote : sa fiche de prime (consultable après double validation) + suivi du paiement. */
  myPaymentTracking(userId: string, role: string): Observable<EmployeePrimePaymentTrackingDto[]> {
    const params = new HttpParams().set('userId', userId).set('role', role);
    return this.http.get<EmployeePrimePaymentTrackingDto[]>(`${base}/my-synthesis-tracking`, { params });
  }

  downloadScopeExcel(scopeSynthesisId: string, userId: string): Observable<Blob> {
    const params = new HttpParams().set('userId', userId);
    return this.http.get(`${base}/scope-synthesis/${scopeSynthesisId}/excel`, {
      params,
      responseType: 'blob',
    });
  }

  approveScopeStep(
    scopeSynthesisId: string,
    body: { userId: string; stepId: string; role?: string },
  ): Observable<GlobalPoolScopeSynthesisInboxItemDto> {
    return this.http.post<GlobalPoolScopeSynthesisInboxItemDto>(
      `${base}/scope-synthesis/${scopeSynthesisId}/approve-step`,
      body,
    );
  }

  approveScopeManager(
    scopeSynthesisId: string,
    userId: string,
  ): Observable<GlobalPoolScopeSynthesisInboxItemDto> {
    return this.http.post<GlobalPoolScopeSynthesisInboxItemDto>(
      `${base}/scope-synthesis/${scopeSynthesisId}/approve-manager`,
      { userId },
    );
  }

  approveScopeRh(
    scopeSynthesisId: string,
    userId: string,
  ): Observable<GlobalPoolScopeSynthesisInboxItemDto> {
    return this.http.post<GlobalPoolScopeSynthesisInboxItemDto>(
      `${base}/scope-synthesis/${scopeSynthesisId}/approve-rh`,
      { userId },
    );
  }

  ackScopeCompta(
    scopeSynthesisId: string,
    userId: string,
  ): Observable<GlobalPoolScopeSynthesisInboxItemDto> {
    return this.http.post<GlobalPoolScopeSynthesisInboxItemDto>(
      `${base}/scope-synthesis/${scopeSynthesisId}/ack-compta`,
      { userId },
    );
  }

  rejectLine(lineId: string, body: { userId: string; role?: string; reason: string }): Observable<void> {
    return this.http.post<void>(`${base}/synthesis/lines/${lineId}/reject`, body);
  }

  approveLine(lineId: string, body: { userId: string; role?: string }): Observable<void> {
    return this.http.post<void>(`${base}/synthesis/lines/${lineId}/approve`, body);
  }

  synthesisTrackingFeed(params: {
    userId: string;
    role?: string;
    period?: string;
    mineOnly?: boolean;
    action?: string;
  }): Observable<SynthesisTrackingFeedItemDto[]> {
    let httpParams = new HttpParams().set('userId', params.userId);
    if (params.role) httpParams = httpParams.set('role', params.role);
    if (params.period) httpParams = httpParams.set('period', params.period);
    if (params.mineOnly != null) httpParams = httpParams.set('mineOnly', String(params.mineOnly));
    if (params.action) httpParams = httpParams.set('action', params.action);
    return this.http.get<SynthesisTrackingFeedItemDto[]>(`${base}/synthesis-tracking-feed`, {
      params: httpParams,
    });
  }

  synthesisLineHistory(
    lineId: string,
    params: { userId: string; role?: string },
  ): Observable<GlobalPoolSynthesisLineHistoryDto[]> {
    let httpParams = new HttpParams().set('userId', params.userId);
    if (params.role) httpParams = httpParams.set('role', params.role);
    return this.http.get<GlobalPoolSynthesisLineHistoryDto[]>(
      `${base}/synthesis/lines/${lineId}/history`,
      { params: httpParams },
    );
  }

  /** File historique brouillons (legacy). */
  inbox(userId: string): Observable<GlobalPoolInboxItemDto[]> {
    const params = new HttpParams().set('userId', userId);
    return this.http.get<GlobalPoolInboxItemDto[]>(`${base}/inbox`, { params });
  }
}
