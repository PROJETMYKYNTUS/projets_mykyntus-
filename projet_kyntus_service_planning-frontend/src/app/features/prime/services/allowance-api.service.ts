import { Injectable, inject, signal } from '@angular/core';
import { primeApiGet, primeApiPatch, primeApiPost, primeApiDelete } from './prime-http';

export interface AllowanceTypeDto {
  id: string;
  code: string;
  label: string;
  category: string;
  calculationMode: string;
  defaultAmount?: number;
  minAmount?: number;
  maxAmount?: number;
  requiresJustification: boolean;
  applicableDepartmentKinds: string;
  isActive: boolean;
}

export interface AllowanceRequestDto {
  id: string;
  employeeId: string;
  businessDepartmentId: string;
  allowanceTypeId: string;
  typeCode: string;
  typeLabel: string;
  period: string;
  amount: number;
  currency: string;
  reason: string;
  source: string;
  status: string;
  createdByUserId: string;
  rejectionReason?: string;
  managerApprovedAt?: string;
  rhApprovedAt?: string;
  comptaApprovedAt?: string;
  paidAt?: string;
  createdAt: string;
}

export interface AllowanceContextDto {
  userId: string;
  role: string;
  businessDepartmentId?: string;
  businessDepartmentKind?: string;
  isSupportDepartmentManager: boolean;
  isOperationalDepartmentManager?: boolean;
  managedDepartmentId?: string;
  managedDepartmentKind?: string;
  managedDepartmentName?: string;
  managedDepartmentCode?: string;
  directReportCount?: number;
}

export interface AllowanceTeamMemberDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface BusinessDepartmentMirrorDto {
  id: string;
  code: string;
  name: string;
  kind: string;
  managerEmployeeId?: string;
  isActive: boolean;
  poleIds: string[];
}

export type AllowanceTreatmentStatus =
  | 'NotStarted'
  | 'HasDrafts'
  | 'Submitted'
  | 'Validated'
  | 'Rejected'
  | 'NoBonus';

export interface AllowanceTeamProgressSummaryDto {
  totalEmployees: number;
  notStartedCount: number;
  inProgressCount: number;
  submittedCount: number;
  validatedCount: number;
  noBonusCount: number;
  totalAmount: number;
}

export interface AllowanceTeamMemberProgressDto {
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
  requestCount: number;
  draftCount: number;
  submittedCount: number;
  treatmentStatus: AllowanceTreatmentStatus;
  noBonusMarked: boolean;
}

export interface AllowanceTeamProgressDto {
  period: string;
  summary: AllowanceTeamProgressSummaryDto;
  members: AllowanceTeamMemberProgressDto[];
}

export interface AllowanceEmployeeAllocationsDto {
  employeeId: string;
  period: string;
  requests: AllowanceRequestDto[];
  availableTypes: AllowanceTypeDto[];
  noBonusMarked: boolean;
  noBonusComment?: string;
  noBonusMarkedAt?: string;
}

export interface AllowanceHistoryEntryDto {
  request: AllowanceRequestDto;
  employeeFirstName: string;
  employeeLastName: string;
}

export interface AllowancePeriodSummaryDto {
  period: string;
  requestCount: number;
  draftCount: number;
  submittedCount: number;
  validatedCount: number;
  noBonusCount: number;
  totalAmount: number;
}

@Injectable({ providedIn: 'root' })
export class AllowanceApiService {
  getContext(): Promise<AllowanceContextDto> {
    return primeApiGet<AllowanceContextDto>('/api/prime/allowances/context/me');
  }

  listTeamMembers(): Promise<AllowanceTeamMemberDto[]> {
    return primeApiGet<AllowanceTeamMemberDto[]>('/api/prime/allowances/team');
  }

  getTeamProgress(period: string): Promise<AllowanceTeamProgressDto> {
    return primeApiGet<AllowanceTeamProgressDto>(
      `/api/prime/allowances/team-progress?period=${encodeURIComponent(period.trim())}`,
    );
  }

  getEmployeeAllocations(period: string, employeeId: string): Promise<AllowanceEmployeeAllocationsDto> {
    const params = new URLSearchParams({
      period: period.trim(),
      employeeId: employeeId.trim(),
    });
    return primeApiGet<AllowanceEmployeeAllocationsDto>(
      `/api/prime/allowances/employee-allocations?${params.toString()}`,
    );
  }

  generateTeamProposals(period: string): Promise<{ created: number }> {
    return primeApiPost<{ created: number }>(
      `/api/prime/allowances/team/generate-proposals?period=${encodeURIComponent(period.trim())}`,
      {},
    );
  }

  markNoBonus(period: string, employeeId: string, comment?: string): Promise<{ marked: boolean }> {
    const params = new URLSearchParams({ period: period.trim(), employeeId: employeeId.trim() });
    return primeApiPost<{ marked: boolean }>(`/api/prime/allowances/no-bonus?${params.toString()}`, { comment: comment ?? null });
  }

  clearNoBonus(period: string, employeeId: string): Promise<{ cleared: boolean }> {
    const params = new URLSearchParams({ period: period.trim(), employeeId: employeeId.trim() });
    return primeApiDelete<{ cleared: boolean }>(`/api/prime/allowances/no-bonus?${params.toString()}`);
  }

  getHistory(fromPeriod?: string, toPeriod?: string): Promise<AllowanceHistoryEntryDto[]> {
    const params = new URLSearchParams();
    if (fromPeriod?.trim()) params.set('fromPeriod', fromPeriod.trim());
    if (toPeriod?.trim()) params.set('toPeriod', toPeriod.trim());
    const q = params.toString();
    return primeApiGet<AllowanceHistoryEntryDto[]>(`/api/prime/allowances/history${q ? `?${q}` : ''}`);
  }

  getPeriodSummaries(): Promise<AllowancePeriodSummaryDto[]> {
    return primeApiGet<AllowancePeriodSummaryDto[]>('/api/prime/allowances/period-summaries');
  }

  listTypes(): Promise<AllowanceTypeDto[]> {
    return primeApiGet<AllowanceTypeDto[]>('/api/prime/allowances/types');
  }

  listEligibleTypes(businessDepartmentId?: string): Promise<AllowanceTypeDto[]> {
    const q = businessDepartmentId ? `?businessDepartmentId=${encodeURIComponent(businessDepartmentId)}` : '';
    return primeApiGet<AllowanceTypeDto[]>(`/api/prime/allowances/types/eligible${q}`);
  }

  listRequests(departmentId?: string, period?: string): Promise<AllowanceRequestDto[]> {
    const params = new URLSearchParams();
    if (departmentId) params.set('departmentId', departmentId);
    if (period) params.set('period', period);
    const q = params.toString();
    return primeApiGet<AllowanceRequestDto[]>(`/api/prime/allowances/requests${q ? `?${q}` : ''}`);
  }

  inbox(): Promise<AllowanceRequestDto[]> {
    return primeApiGet<AllowanceRequestDto[]>('/api/prime/allowances/requests/inbox');
  }

  createRequest(body: {
    employeeId: string;
    allowanceTypeId: string;
    period: string;
    amount: number;
    currency?: string;
    reason?: string;
  }): Promise<AllowanceRequestDto> {
    return primeApiPost<AllowanceRequestDto>('/api/prime/allowances/requests', body);
  }

  submit(id: string): Promise<AllowanceRequestDto> {
    return primeApiPost<AllowanceRequestDto>(`/api/prime/allowances/requests/${id}/submit`, {});
  }

  updateDraft(
    id: string,
    body: {
      allowanceTypeId?: string;
      period?: string;
      amount?: number;
      reason?: string;
    },
  ): Promise<AllowanceRequestDto> {
    return primeApiPatch<AllowanceRequestDto>(`/api/prime/allowances/requests/${id}`, body);
  }

  approve(id: string): Promise<AllowanceRequestDto> {
    return primeApiPost<AllowanceRequestDto>(`/api/prime/allowances/requests/${id}/approve`, {});
  }

  reject(id: string, reason: string): Promise<AllowanceRequestDto> {
    return primeApiPost<AllowanceRequestDto>(`/api/prime/allowances/requests/${id}/reject`, { reason });
  }

  listBusinessDepartments(): Promise<BusinessDepartmentMirrorDto[]> {
    return primeApiGet<BusinessDepartmentMirrorDto[]>('/api/prime/allowances/business-departments');
  }

  generateProposals(period: string, businessDepartmentId?: string): Promise<{ created: number }> {
    const params = new URLSearchParams({ period });
    if (businessDepartmentId?.trim()) {
      params.set('businessDepartmentId', businessDepartmentId.trim());
    }
    return primeApiPost<{ created: number }>(
      `/api/prime/allowances/rules/generate-proposals?${params.toString()}`,
      {},
    );
  }

  createType(body: {
    code: string;
    label: string;
    category: string;
    calculationMode?: string;
    defaultAmount?: number;
    minAmount?: number;
    maxAmount?: number;
    requiresJustification: boolean;
    applicableDepartmentKinds?: string;
  }): Promise<AllowanceTypeDto> {
    return primeApiPost<AllowanceTypeDto>('/api/prime/allowances/types', body);
  }
}

@Injectable({ providedIn: 'root' })
export class DepartmentContextService {
  private readonly allowanceApi = inject(AllowanceApiService);

  readonly context = signal<AllowanceContextDto | null>(null);
  readonly loaded = signal(false);

  async load(): Promise<void> {
    try {
      const ctx = await this.allowanceApi.getContext();
      this.context.set(ctx);
    } catch {
      this.context.set(null);
    } finally {
      this.loaded.set(true);
    }
  }

  isSupportManager(): boolean {
    return this.context()?.isSupportDepartmentManager === true;
  }

  isOperationalManager(): boolean {
    return this.context()?.isOperationalDepartmentManager === true;
  }

  isDepartmentManager(): boolean {
    return this.isSupportManager() || this.isOperationalManager();
  }

  managedDepartmentLabel(): string {
    const c = this.context();
    const code = c?.managedDepartmentCode?.trim();
    const name = c?.managedDepartmentName?.trim();
    if (code && name) return `${code} — ${name}`;
    return name ?? code ?? '—';
  }

  directReportCount(): number {
    return this.context()?.directReportCount ?? 0;
  }

  departmentKind(): 'Support' | 'Operational' | null {
    const managed = this.context()?.managedDepartmentKind;
    if (managed === 'Support' || managed === 'Operational') return managed;
    return null;
  }
}
