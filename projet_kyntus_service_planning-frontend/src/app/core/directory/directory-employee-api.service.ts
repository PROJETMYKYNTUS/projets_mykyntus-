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
}
