import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const directoryBase = '/api/directory';

export interface PilotRotationEligibilityDto {
  eligible: boolean;
  isSameService: boolean;
  currentServiceId?: string | null;
  currentServiceName?: string | null;
  currentSince?: string | null;
  eligibleAt?: string | null;
  daysRemaining: number;
}

export interface PilotRotationHistoryEntryDto {
  serviceId: string;
  serviceName: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  durationDays?: number | null;
  changeReason?: string | null;
  isOverride: boolean;
}

export interface PilotRotationSummaryDto {
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
  rotationCount: number;
  currentServiceId?: string | null;
  currentServiceName?: string | null;
  firstEffectiveFrom?: string | null;
  lastEffectiveFrom?: string | null;
  segments: PilotRotationHistoryEntryDto[];
}

export type PilotRotationSort = 'rotationCountDesc' | 'rotationCountAsc' | 'name';

export interface PilotRotationListQuery {
  serviceId?: string;
  from?: string;
  to?: string;
  minRotations?: number;
  maxRotations?: number;
  sort?: PilotRotationSort;
}

@Injectable({ providedIn: 'root' })
export class DirectoryEmployeeApiService {
  private readonly http = inject(HttpClient);

  getPilotRotationEligibility(
    employeeId: string,
    targetServiceId: string,
  ): Observable<PilotRotationEligibilityDto> {
    return this.http.get<PilotRotationEligibilityDto>(
      `${directoryBase}/employees/${encodeURIComponent(employeeId)}/pilot-rotation-eligibility`,
      { params: { targetServiceId } },
    );
  }

  getPilotRotationHistory(employeeId: string): Observable<PilotRotationHistoryEntryDto[]> {
    return this.http.get<PilotRotationHistoryEntryDto[]>(
      `${directoryBase}/employees/${encodeURIComponent(employeeId)}/pilot-rotation-history`,
    );
  }

  listPilotRotations(query: PilotRotationListQuery = {}): Observable<PilotRotationSummaryDto[]> {
    const params: Record<string, string> = {};
    if (query.serviceId?.trim()) params['serviceId'] = query.serviceId.trim();
    if (query.from?.trim()) params['from'] = query.from.trim();
    if (query.to?.trim()) params['to'] = query.to.trim();
    if (query.minRotations != null && Number.isFinite(query.minRotations)) {
      params['minRotations'] = String(query.minRotations);
    }
    if (query.maxRotations != null && Number.isFinite(query.maxRotations)) {
      params['maxRotations'] = String(query.maxRotations);
    }
    if (query.sort) params['sort'] = query.sort;
    return this.http.get<PilotRotationSummaryDto[]>(`${directoryBase}/pilot-rotations`, { params });
  }
}
