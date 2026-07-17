// features/planning/services/planning.service.ts

import { Injectable } from '@angular/core';
import { HttpClient,HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface CoverageDayShift {
  date: string;
  day: string;
  shiftConfigId: number;
  shiftLabel: string;
  shiftKind?: string;
  requiredCount: number;
  assignedCount: number;
  minPresencePercent: number;
  presencePercent: number;
  isUnderstaffed: boolean;
  hasLevelBalanceAnomaly?: boolean;
}

export interface PlanningAnomaly {
  code: string;
  severity: string;
  date: string;
  day: string;
  shiftConfigId: number;
  shiftLabel: string;
  message: string;
  isForced?: boolean;
}

export interface DaySynthesisShift {
  shiftConfigId: number;
  shiftLabel: string;
  shiftKind: string;
  assignedCount: number;
  requiredCount: number;
  delta: number;
  beginnerCount?: number;
  seniorCount?: number;
  isUnderstaffed: boolean;
  hasLevelBalanceAnomaly: boolean;
}

export interface DaySynthesis {
  date: string;
  day: string;
  shifts: DaySynthesisShift[];
  leaveCount: number;
  holidayCount: number;
  presentCount?: number;
  saturdayBeginners?: number;
  saturdaySeniors?: number;
  hasAnyAnomaly: boolean;
}

export interface CoverageReport {
  hasUnderstaffing: boolean;
  hasLevelBalanceAnomaly?: boolean;
  warnings: string[];
  levelBalanceAnomalies?: PlanningAnomaly[];
  items: CoverageDayShift[];
  daySynthesis?: DaySynthesis[];
}

export interface ShiftConfig {
  shiftId:       number;
  shiftLabel:    string;
  startTime:     string;
  shiftKind?:    string;
  requiredCount: number;
  percentage:    number;
}

export interface SaturdayYtd {
  userId: number;
  fullName: string;
  workedCount: number;
  offCount: number;
  totalWeeksRecorded: number;
  workedPercent: number;
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
  coverageReport?: CoverageReport | null;
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
  weekCode?:     string | null;
  weekStartDate?: string | null;
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
  isTemplate?:    boolean;
  totalEffectif:  number;
  shifts:         ShiftConfigResponseNew[];
}

export interface ShiftConfigStatusItem {
  subServiceId: number;
  subServiceName: string;
  primeServiceId?: string | null;
  hasTemplate: boolean;
  shiftCount: number;
  templateEffectif: number;
  activeEmployeeCount: number;
}

export interface ShiftConfigStatusResponse {
  items: ShiftConfigStatusItem[];
  configuredCount: number;
  totalCount: number;
}

export interface GeneratePlanningFromConfigDto {
  subServiceId:     number;
  weekCode?:        string;
  weeklyPlanningId: number;
}

export interface PlanningWeekItem {
  subServiceId: number;
  subServiceName: string;
  orgLabel: string;
  planningId?: number | null;
  status?: string | null;
  totalEffectif: number;
  hasTemplate: boolean;
  coverageOk: boolean;
  hasConsulted: boolean;
}

export interface PlanningWeekList {
  weekCode: string;
  weekStartDate: string;
  items: PlanningWeekItem[];
}

export interface AutoGenerateSettings {
  enabled: boolean;
  dayOfWeek: number;
  hourLocal: number;
  minuteLocal: number;
  timeZone: string;
  target: string;
  lastRunAt?: string | null;
  lastRunWeekCode?: string | null;
}

export interface AutoGenerateWeekResult {
  weekCode: string;
  created: number;
  skipped: number;
  errors: number;
  messages: string[];
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
  private api       = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token')
      || localStorage.getItem('access_token')
      || '';
    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }

  // ── CRUD Planning ──────────────────────────────────
  create(dto: CreatePlanningDto): Observable<WeeklyPlanningResponse> {
    return this.http.post<WeeklyPlanningResponse>(this.base, dto);
  }

  getById(id: number): Observable<WeeklyPlanningResponse> {
    return this.http.get<WeeklyPlanningResponse>(`${this.base}/${id}`);
  }

  getBySubService(subServiceId: number): Observable<WeeklyPlanningResponse[]> {
    return this.http.get<WeeklyPlanningResponse[]>(`${this.base}/subservice/${subServiceId}`);
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

  getShiftTemplate(subServiceId: number): Observable<WeekShiftConfigResponse> {
    return this.http.get<WeekShiftConfigResponse>(`${this.base}/config/${subServiceId}`);
  }

  getShiftConfigStatus(): Observable<ShiftConfigStatusResponse> {
    return this.http.get<ShiftConfigStatusResponse>(`${this.base}/config/status`);
  }

  getShiftConfig(subServiceId: number, weekCode: string): Observable<WeekShiftConfigResponse> {
    return this.http.get<WeekShiftConfigResponse>(`${this.base}/config/${subServiceId}/${weekCode}`);
  }

  generateFromConfig(dto: GeneratePlanningFromConfigDto): Observable<WeeklyPlanningResponse> {
    return this.http.post<WeeklyPlanningResponse>(`${this.base}/generate-from-config`, dto);
  }

  getWeekOverview(weekCode: string, viewerUserId?: number): Observable<PlanningWeekList> {
    const q = viewerUserId != null ? `?viewerUserId=${viewerUserId}` : '';
    return this.http.get<PlanningWeekList>(`${this.base}/week/${weekCode}${q}`);
  }

  consultPlanning(id: number, userId: number): Observable<{ consulted: boolean }> {
    return this.http.post<{ consulted: boolean }>(`${this.base}/${id}/consult?userId=${userId}`, {});
  }

  getAutoGenerateSettings(): Observable<AutoGenerateSettings> {
    return this.http.get<AutoGenerateSettings>(`${this.base}/auto-generate-settings`);
  }

  saveAutoGenerateSettings(dto: AutoGenerateSettings, updatedByUserId?: number): Observable<AutoGenerateSettings> {
    const q = updatedByUserId != null ? `?updatedByUserId=${updatedByUserId}` : '';
    return this.http.put<AutoGenerateSettings>(`${this.base}/auto-generate-settings${q}`, dto);
  }

  autoGenerateWeek(weekCode: string, force = false): Observable<AutoGenerateWeekResult> {
    return this.http.post<AutoGenerateWeekResult>(
      `${this.base}/week/${weekCode}/auto-generate?force=${force}`, {});
  }

  // ── Vue Employé ───────────────────────────────────
  getMyCurrentPlanning(userId: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/my/current?userId=${userId}`,
      { headers: this.getHeaders() }
    );
  }

  getMyPlanning(weekCode: string, userId: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/my/${weekCode}?userId=${userId}`,
      { headers: this.getHeaders() }
    );
  }

  getMyHistory(userId: number): Observable<any> {
    return this.http.get(
      `${this.api}/planning/my/history?userId=${userId}`,
      { headers: this.getHeaders() }
    );
  }

  getEquipePlannings(authUserId: number): Observable<WeeklyPlanningResponse[]> {
    return this.http.get<WeeklyPlanningResponse[]>(
      `${this.api}/planning/equipe?authUserId=${authUserId}`,
      { headers: this.getHeaders() }
    );
  }

  // ── Publication + Override ─────────────────────────
  publish(id: number, validatorId: number): Observable<WeeklyPlanningResponse> {
    return this.http.post<WeeklyPlanningResponse>(
      `${this.base}/${id}/publish?validatorId=${validatorId}`, {});
  }

  overrideShift(dto: OverrideShiftDto): Observable<DayAssignment> {
    return this.http.put<DayAssignment>(`${this.base}/override`, dto);
  }

  overrideBreakTime(dto: { shiftAssignmentId: number; newBreakTime: string }): Observable<any> {
    return this.http.put(`${this.base}/override-break`, dto);
  }

  overrideSaturdayShift(dto: OverrideSaturdayDto): Observable<DayAssignment> {
    return this.http.put<DayAssignment>(`${this.base}/override-saturday`, dto);
  }

  getShiftConfigsForSaturday(subServiceId: number, weekCode: string): Observable<ShiftConfigResponseNew[]> {
    return this.http.get<WeekShiftConfigResponse>(`${this.base}/config/${subServiceId}/${weekCode}`)
      .pipe(map((r: WeekShiftConfigResponse) => r.shifts));
  }

  // ── Groupes samedi ────────────────────────────────
  autoAssignSaturdayGroups(subServiceId: number): Observable<any> {
    return this.http.post(`${this.base}/saturday-groups/auto/${subServiceId}`, {});
  }

  setSaturdayGroup(dto: { userId: number; groupNumber: number; isNewEmployee: boolean }): Observable<any> {
    return this.http.post(`${this.base}/saturday-group`, dto);
  }

  setSaturdayOff(weeklyPlanningId: number, userId: number): Observable<any> {
    return this.http.delete(`${this.base}/${weeklyPlanningId}/saturday/${userId}/off`);
  }

  getSaturdayGroups(subServiceId: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/saturday-groups/${subServiceId}`);
  }

  getSaturdayHistory(subServiceId: number, weekCode: string): Observable<SaturdayHistoryResponse[]> {
    return this.http.get<SaturdayHistoryResponse[]>(
      `${this.base}/saturday-history/${subServiceId}/${weekCode}`);
  }

  getSaturdayYtd(subServiceId: number, year?: number): Observable<SaturdayYtd[]> {
    const y = year ?? new Date().getFullYear();
    return this.http.get<SaturdayYtd[]>(
      `${this.base}/saturday-history/${subServiceId}/ytd?year=${y}`);
  }

  saveSaturdayHistory(dto: SetSaturdayHistoryDto): Observable<any> {
    return this.http.post(`${this.base}/saturday-history`, dto);
  }

  // ── Demandes de changement (RH) ───────────────────
  getChangeRequests(status?: string, weekCode?: string): Observable<any[]> {
    const params: string[] = [];
    if (status) params.push(`status=${encodeURIComponent(status)}`);
    if (weekCode) params.push(`weekCode=${encodeURIComponent(weekCode)}`);
    const qs = params.length ? `?${params.join('&')}` : '';
    return this.http.get<any[]>(`${this.base}/change-requests${qs}`);
  }

  getChangeRequestStats(weekCode?: string): Observable<any[]> {
    const qs = weekCode ? `?weekCode=${encodeURIComponent(weekCode)}` : '';
    return this.http.get<any[]>(`${this.base}/change-requests/stats-by-employee${qs}`);
  }

  approveChangeRequest(id: number, authUserId: number): Observable<any> {
    return this.http.post(`${this.base}/change-requests/${id}/approve?authUserId=${authUserId}`, {});
  }

  rejectChangeRequest(id: number, authUserId: number, reason?: string): Observable<any> {
    return this.http.post(
      `${this.base}/change-requests/${id}/reject?authUserId=${authUserId}`,
      { reason: reason || null },
    );
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
    return this.http.get<EmployeeItem[]>(`${this.subSvcUrl}/${subServiceId}/employees`);
  }

  // ── Options horaires ──────────────────────────────
  getShiftStartOptions(): ShiftOption[] {
    const options: ShiftOption[] = [];
    for (let h = 5; h <= 14; h++) {
      options.push({ label: `${h.toString().padStart(2, '0')}:00`, value: `${h.toString().padStart(2, '0')}:00` });
      options.push({ label: `${h.toString().padStart(2, '0')}:30`, value: `${h.toString().padStart(2, '0')}:30` });
    }
    return options;
  }

  getBreakSlotOptions(): ShiftOption[] {
    const options: ShiftOption[] = [];
    for (let h = 11; h <= 16; h++) {
      options.push({ label: `${h.toString().padStart(2, '0')}:00`, value: `${h.toString().padStart(2, '0')}:00` });
      if (h < 16) {
        options.push({ label: `${h.toString().padStart(2, '0')}:30`, value: `${h.toString().padStart(2, '0')}:30` });
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

  getShifts(): Observable<ShiftSimple[]> {
    return of([
      { id: 1, label: '8h',  startTime: '08:00' },
      { id: 2, label: '9h',  startTime: '09:00' },
      { id: 3, label: '10h', startTime: '10:00' },
      { id: 4, label: '11h', startTime: '11:00' },
    ]);
  }

} 