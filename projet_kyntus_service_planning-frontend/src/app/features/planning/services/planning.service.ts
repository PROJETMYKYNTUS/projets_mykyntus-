// features/planning/services/planning.service.ts

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface ShiftConfig {
  shiftId:       number;
  shiftLabel:    string;
  startTime:     string;
  requiredCount: number;
  percentage:    number;
}

export interface DayAssignment {
  assignmentId:      number;
  day:               string;
  assignedDate:      string;
  shiftLabel:        string;
  startTime:         string;
  endTime:           string;
  isSaturday:        boolean;
  isManagerOverride: boolean;
  breakTime?:        string;
  isOnLeave:         boolean;
  isHalfDaySaturday: boolean;
   absenceType:       string | null; 
  saturdaySlot:      number;
  slotLabel:         string;
  isHoliday:   boolean;
holidayName: string;
}

export interface EmployeePlanning {
  userId:          number;
  fullName:        string;
  isNewEmployee:   boolean;
  days:            DayAssignment[];
  managerComment?: string;
  level:           number;
}

export interface WeeklyPlanningResponse {
  id:              number;
  weekCode:        string;
  weekStartDate:   string;
  status:          string;
  totalEffectif:   number;
  subServiceId:    number;
  saturdayGroupId: number;
  subServiceName:  string;
  shiftConfigs:    ShiftConfig[];
  assignments:     EmployeePlanning[];
}

export interface CreatePlanningDto {
  subServiceId:  number;
  weekCode:      string;
  weekStartDate: string;
  totalEffectif: number;
}

export interface GeneratePlanningDto {
  weeklyPlanningId: number;
  totalEffectif:    number;
}

// ✅ CORRIGÉ — newSubServiceShiftConfigId ajouté
export interface OverrideShiftDto {
  shiftAssignmentId:          number;
  newShiftId:                 number;   // ancien système (0 si inutilisé)
  newSubServiceShiftConfigId: number;   // ✅ nouveau système
}

export interface SubServiceSimple {
  id:   number;
  name: string;
}

export interface ShiftSimple {
  id:        number;
  label:     string;
  startTime: string;
}

export interface EmployeeItem {
  id:            number;
  fullName:      string;
  isNewEmployee: boolean;
  isActive:      boolean;
}

export interface SavePlanningCommentDto {
  weeklyPlanningId: number;
  userId:           number;
  comment:          string;
  createdBy:        number;
}

export interface PlanningCommentDto {
  id:         number;
  userId:     number;
  fullName:   string;
  comment:    string;
  createdAt:  string;
  updatedAt?: string;
}

export interface ShiftConfigItem {
  label:                string;
  startTime:            string;
  workHours:            number;
  breakDurationMinutes: number;
  breakRangeStart?:     string;
  breakRangeEnd?:       string;
  requiredCount:        number;
  minPresencePercent:   number;
  displayOrder:         number;
}

export interface SaveShiftConfigDto {
  subServiceId:  number;
  weekCode:      string;
  weekStartDate: string;
  shifts:        ShiftConfigItem[];
}

export interface ShiftConfigResponseNew {
  id:                   number;
  label:                string;
  startTime:            string;
  endTime:              string;
  workHours:            number;
  breakRangeStart:      string;
  breakRangeEnd:        string;
  breakDurationMinutes: number;
  requiredCount:        number;
  percentage:           number;
  minPresencePercent:   number;
  displayOrder:         number;
}

export interface WeekShiftConfigResponse {
  subServiceId:   number;
  subServiceName: string;
  weekCode:       string;
  weekStartDate:  string;
  totalEffectif:  number;
  shifts:         ShiftConfigResponseNew[];
}

export interface GeneratePlanningFromConfigDto {
  subServiceId:     number;
  weekCode:         string;
  weeklyPlanningId: number;
}

export interface SaturdayHistoryEntry {
  userId:         number;
  workedSaturday: boolean;
}

export interface SetSaturdayHistoryDto {
  subServiceId: number;
  weekCode:     string;
  entries:      SaturdayHistoryEntry[];
}

export interface SaturdayHistoryResponse {
  userId:         number;
  fullName:       string;
  weekCode:       string;
  workedSaturday: boolean;
  isManualEntry:  boolean;
}

export interface ShiftOption {
  label: string;
  value: string;
}

// ✅ CORRIGÉ — weeklyPlanningId + userId pour le cas OFF → WORK
export interface OverrideSaturdayDto {
  shiftAssignmentId:          number;
  newSubServiceShiftConfigId: number;
  weeklyPlanningId?:          number;
  userId?:                    number;
}

// ════════════════════════════════════════════════════
// SERVICE
// ════════════════════════════════════════════════════
@Injectable({ providedIn: 'root' })
export class PlanningService {

  private base      = `${environment.apiUrl}/planning`;
  private subSvcUrl = `${environment.apiUrl}/SubServices`;

  constructor(private http: HttpClient) {}

  // ── CRUD Planning ──────────────────────────────────

  create(dto: CreatePlanningDto): Observable<WeeklyPlanningResponse> {
    return this.http.post<WeeklyPlanningResponse>(this.base, dto);
  }

  getById(id: number): Observable<WeeklyPlanningResponse> {
    return this.http.get<WeeklyPlanningResponse>(`${this.base}/${id}`);
  }

  getBySubService(subServiceId: number): Observable<WeeklyPlanningResponse[]> {
    return this.http.get<WeeklyPlanningResponse[]>(
      `${this.base}/subservice/${subServiceId}`);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  // ── Génération ────────────────────────────────────

  generate(dto: GeneratePlanningDto): Observable<WeeklyPlanningResponse> {
    return this.http.post<WeeklyPlanningResponse>(`${this.base}/generate`, dto);
  }

  // ── Config shifts ─────────────────────────────────

  saveShiftConfig(dto: SaveShiftConfigDto): Observable<WeekShiftConfigResponse> {
    return this.http.post<WeekShiftConfigResponse>(`${this.base}/config`, dto);
  }

  getShiftConfig(
    subServiceId: number,
    weekCode: string
  ): Observable<WeekShiftConfigResponse> {
    return this.http.get<WeekShiftConfigResponse>(
      `${this.base}/config/${subServiceId}/${weekCode}`);
  }

  generateFromConfig(
    dto: GeneratePlanningFromConfigDto
  ): Observable<WeeklyPlanningResponse> {
    return this.http.post<WeeklyPlanningResponse>(
      `${this.base}/generate-from-config`, dto);
  }

  // ── Publication + Override ─────────────────────────

  publish(id: number, validatorId: number): Observable<WeeklyPlanningResponse> {
    return this.http.post<WeeklyPlanningResponse>(
      `${this.base}/${id}/publish?validatorId=${validatorId}`, {});
  }

  // ✅ CORRIGÉ — envoie newSubServiceShiftConfigId au backend
  overrideShift(dto: OverrideShiftDto): Observable<DayAssignment> {
    return this.http.put<DayAssignment>(`${this.base}/override`, dto);
  }

  overrideBreakTime(dto: { shiftAssignmentId: number; newBreakTime: string }): Observable<any> {
    return this.http.put(`${this.base}/override-break`, dto);
  }

  overrideSaturdayShift(dto: OverrideSaturdayDto): Observable<DayAssignment> {
    return this.http.put<DayAssignment>(`${this.base}/override-saturday`, dto);
  }

  getShiftConfigsForSaturday(
    subServiceId: number,
    weekCode: string
  ): Observable<ShiftConfigResponseNew[]> {
    return this.http.get<WeekShiftConfigResponse>(
      `${this.base}/config/${subServiceId}/${weekCode}`)
      .pipe(map((r: WeekShiftConfigResponse) => r.shifts));
  }

  // ── Groupes samedi ────────────────────────────────

  autoAssignSaturdayGroups(subServiceId: number): Observable<any> {
    return this.http.post(
      `${this.base}/saturday-groups/auto/${subServiceId}`, {});
  }

  setSaturdayGroup(dto: {
    userId: number;
    groupNumber: number;
    isNewEmployee: boolean;
  }): Observable<any> {
    return this.http.post(`${this.base}/saturday-group`, dto);
  }
setSaturdayOff(weeklyPlanningId: number, userId: number): Observable<any> {
  return this.http.delete(
    `${this.base}/${weeklyPlanningId}/saturday/${userId}/off`
  );
}
  getSaturdayGroups(subServiceId: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/saturday-groups/${subServiceId}`);
  }

  getSaturdayHistory(
    subServiceId: number,
    weekCode: string
  ): Observable<SaturdayHistoryResponse[]> {
    return this.http.get<SaturdayHistoryResponse[]>(
      `${this.base}/saturday-history/${subServiceId}/${weekCode}`);
  }

  saveSaturdayHistory(dto: SetSaturdayHistoryDto): Observable<any> {
    return this.http.post(`${this.base}/saturday-history`, dto);
  }

  // ── Commentaires ──────────────────────────────────

  saveComment(dto: SavePlanningCommentDto): Observable<PlanningCommentDto> {
    return this.http.post<PlanningCommentDto>(`${this.base}/comment`, dto);
  }

  deleteComment(planningId: number, userId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${planningId}/comment/${userId}`);
  }

  // ── Sous-services + Employés ──────────────────────

  getSubServices(): Observable<SubServiceSimple[]> {
    return this.http.get<SubServiceSimple[]>(this.subSvcUrl);
  }

  getSubServiceEmployees(subServiceId: number): Observable<EmployeeItem[]> {
    return this.http.get<EmployeeItem[]>(
      `${this.subSvcUrl}/${subServiceId}/employees`);
  }

  // ── Options horaires ──────────────────────────────

  getShiftStartOptions(): ShiftOption[] {
    const options: ShiftOption[] = [];
    for (let h = 5; h <= 14; h++) {
      options.push({
        label: `${h.toString().padStart(2, '0')}:00`,
        value: `${h.toString().padStart(2, '0')}:00`
      });
      options.push({
        label: `${h.toString().padStart(2, '0')}:30`,
        value: `${h.toString().padStart(2, '0')}:30`
      });
    }
    return options;
  }

  getBreakSlotOptions(): ShiftOption[] {
    const options: ShiftOption[] = [];
    for (let h = 11; h <= 16; h++) {
      options.push({
        label: `${h.toString().padStart(2, '0')}:00`,
        value: `${h.toString().padStart(2, '0')}:00`
      });
      if (h < 16) {
        options.push({
          label: `${h.toString().padStart(2, '0')}:30`,
          value: `${h.toString().padStart(2, '0')}:30`
        });
      }
    }
    return options;
  }

  calculateEndTime(startTime: string, workHours: number): string {
    if (!startTime) return '';
    const [h, m] = startTime.split(':').map(Number);
    const totalMinutes = h * 60 + m + workHours * 60 + 60;
    const endH = Math.floor(totalMinutes / 60);
    const endM = totalMinutes % 60;
    return `${endH.toString().padStart(2, '0')}:${endM.toString().padStart(2, '0')}`;
  }

  // Ancien getShifts() gardé pour compatibilité
  getShifts(): Observable<ShiftSimple[]> {
    return of([
      { id: 1, label: '8h',  startTime: '08:00' },
      { id: 2, label: '9h',  startTime: '09:00' },
      { id: 3, label: '10h', startTime: '10:00' },
      { id: 4, label: '11h', startTime: '11:00' },
    ]);
  }
}