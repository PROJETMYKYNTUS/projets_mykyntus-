import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * Service Angular (Phase 2) qui consomme les APIs réelles d'administration PRIME :
 *  - {@code /api/prime/admin/rbac} (matrice rôle/action/scope)
 *  - {@code /api/prime/admin/workflow} (étapes + config globale)
 *  - {@code /api/prime/admin/audit-logs} (journal d'audit)
 *  - {@code /api/prime/admin/anomalies} (détection + cycle de vie)
 */

const baseRbac = '/api/prime/admin/rbac';
const baseWorkflow = '/api/prime/admin/workflow';
const baseAudit = '/api/prime/admin/audit-logs';
const baseAnomalies = '/api/prime/admin/anomalies';

// ===== RBAC =====

export type RbacAction = 'Read' | 'Edit' | 'Validate' | 'Configure';
export type RbacScope = 'Global' | 'Pole' | 'Cellule' | 'Service' | 'Self';

export interface RbacCatalogDto {
  actions: string[];
  scopes: string[];
  roles: string[];
}

export interface RbacPermissionDto {
  id: string;
  role: string;
  action: RbacAction | string;
  scope: RbacScope | string;
  isAllowed: boolean;
  updatedAt?: string | null;
}

export interface UpsertRbacPermissionRequest {
  role: string;
  action: RbacAction | string;
  scope: RbacScope | string;
  isAllowed: boolean;
}

// ===== Workflow config =====

export interface WorkflowStepConfigDto {
  id: string;
  sortOrder: number;
  approverRole: string;
  fromStatus: string;
  toStatus: string;
  isActive: boolean;
  slaHours: number;
  capturesAmountsOnApproval: boolean;
  terminalApproved: boolean;
  updatedAt?: string | null;
}

export interface UpsertWorkflowStepConfigRequest {
  sortOrder: number;
  approverRole: string;
  fromStatus: string;
  toStatus: string;
  isActive: boolean;
  slaHours: number;
  capturesAmountsOnApproval: boolean;
  terminalApproved: boolean;
}

export interface WorkflowGlobalConfigDto {
  id: string;
  notificationsEnabled: boolean;
  globalSlaHours: number;
  allowBulkApprove: boolean;
  requireRejectReason: boolean;
  updatedAt?: string | null;
}

export interface UpdateWorkflowGlobalConfigRequest {
  notificationsEnabled: boolean;
  globalSlaHours: number;
  allowBulkApprove: boolean;
  requireRejectReason: boolean;
}

// ===== Audit logs =====

export interface AuditLogDto {
  id: string;
  at: string;
  userId: string;
  userDisplayName: string;
  role: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  detailJson?: string | null;
  ipAddress?: string | null;
}

export interface AuditLogFilters {
  userId?: string;
  role?: string;
  action?: string;
  entityType?: string;
  entityId?: string;
  from?: string;
  to?: string;
  take?: number;
}

// ===== Anomalies =====

export type AnomalyStatus = 'Open' | 'InReview' | 'Resolved' | 'Ignored';
export type AnomalyType =
  | 'ComputationMismatch'
  | 'DuplicateFiche'
  | 'OutOfRange'
  | 'MissingApprover'
  | 'StaleValidation'
  | 'InvalidScope';
export type AnomalySeverity = 'Critical' | 'High' | 'Medium' | 'Low';

export interface AnomalyDto {
  id: string;
  detectedAt: string;
  updatedAt?: string | null;
  type: AnomalyType | string;
  severity: AnomalySeverity | string;
  status: AnomalyStatus | string;
  description: string;
  targetEntityType?: string | null;
  targetEntityId?: string | null;
  period?: string | null;
  serviceId?: string | null;
  celluleId?: string | null;
  poleId?: string | null;
  contextJson?: string | null;
  resolvedByUserId?: string | null;
  resolvedAt?: string | null;
  resolutionNote?: string | null;
}

export interface AnomalyFilters {
  status?: AnomalyStatus;
  type?: AnomalyType;
  severity?: AnomalySeverity;
  period?: string;
  serviceId?: string;
  celluleId?: string;
  poleId?: string;
}

export interface UpdateAnomalyStatusRequest {
  status: AnomalyStatus;
  resolvedByUserId?: string | null;
  resolutionNote?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PrimeAdminService {
  private readonly http = inject(HttpClient);

  // ----- RBAC -----
  listRbac(): Observable<RbacPermissionDto[]> {
    return this.http.get<RbacPermissionDto[]>(baseRbac);
  }

  rbacCatalog(): Observable<RbacCatalogDto> {
    return this.http.get<RbacCatalogDto>(`${baseRbac}/catalog`);
  }

  upsertRbac(body: UpsertRbacPermissionRequest): Observable<RbacPermissionDto> {
    return this.http.put<RbacPermissionDto>(baseRbac, body);
  }

  deleteRbac(id: string): Observable<void> {
    return this.http.delete<void>(`${baseRbac}/${id}`);
  }

  // ----- Workflow -----
  listWorkflowSteps(): Observable<WorkflowStepConfigDto[]> {
    return this.http.get<WorkflowStepConfigDto[]>(`${baseWorkflow}/steps`);
  }

  createWorkflowStep(body: UpsertWorkflowStepConfigRequest): Observable<WorkflowStepConfigDto> {
    return this.http.post<WorkflowStepConfigDto>(`${baseWorkflow}/steps`, body);
  }

  updateWorkflowStep(id: string, body: UpsertWorkflowStepConfigRequest): Observable<WorkflowStepConfigDto> {
    return this.http.put<WorkflowStepConfigDto>(`${baseWorkflow}/steps/${id}`, body);
  }

  deleteWorkflowStep(id: string): Observable<void> {
    return this.http.delete<void>(`${baseWorkflow}/steps/${id}`);
  }

  /** Recalcule les FromStatus selon SortOrder (à appeler après réordonnancement). */
  rechainWorkflowSteps(): Observable<WorkflowStepConfigDto[]> {
    return this.http.post<WorkflowStepConfigDto[]>(`${baseWorkflow}/steps/rechain`, {});
  }

  getWorkflowGlobal(): Observable<WorkflowGlobalConfigDto> {
    return this.http.get<WorkflowGlobalConfigDto>(`${baseWorkflow}/global`);
  }

  updateWorkflowGlobal(body: UpdateWorkflowGlobalConfigRequest): Observable<WorkflowGlobalConfigDto> {
    return this.http.put<WorkflowGlobalConfigDto>(`${baseWorkflow}/global`, body);
  }

  // ----- Audit logs -----
  listAuditLogs(filters?: AuditLogFilters): Observable<AuditLogDto[]> {
    let params = new HttpParams();
    if (filters) {
      if (filters.userId) params = params.set('userId', filters.userId);
      if (filters.role) params = params.set('role', filters.role);
      if (filters.action) params = params.set('action', filters.action);
      if (filters.entityType) params = params.set('entityType', filters.entityType);
      if (filters.entityId) params = params.set('entityId', filters.entityId);
      if (filters.from) params = params.set('from', filters.from);
      if (filters.to) params = params.set('to', filters.to);
      if (filters.take !== undefined) params = params.set('take', String(filters.take));
    }
    return this.http.get<AuditLogDto[]>(baseAudit, { params });
  }

  recordAuditNavigation(body: {
    userId: string;
    userDisplayName: string;
    role: string;
    route: string;
  }): Observable<void> {
    return this.http.post<void>(`${baseAudit}/nav`, body);
  }

  // ----- Anomalies -----
  listAnomalies(filters?: AnomalyFilters): Observable<AnomalyDto[]> {
    let params = new HttpParams();
    if (filters) {
      if (filters.status) params = params.set('status', filters.status);
      if (filters.type) params = params.set('type', filters.type);
      if (filters.severity) params = params.set('severity', filters.severity);
      if (filters.period) params = params.set('period', filters.period);
      if (filters.serviceId) params = params.set('serviceId', filters.serviceId);
      if (filters.celluleId) params = params.set('celluleId', filters.celluleId);
      if (filters.poleId) params = params.set('poleId', filters.poleId);
    }
    return this.http.get<AnomalyDto[]>(baseAnomalies, { params });
  }

  updateAnomalyStatus(id: string, body: UpdateAnomalyStatusRequest): Observable<AnomalyDto> {
    return this.http.put<AnomalyDto>(`${baseAnomalies}/${id}`, body);
  }

  recomputeAnomalies(): Observable<{ upsertedCount: number }> {
    return this.http.post<{ upsertedCount: number }>(`${baseAnomalies}/recompute-all`, {});
  }
}
