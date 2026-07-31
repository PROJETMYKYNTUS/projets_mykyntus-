import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { switchMap } from 'rxjs/operators';
import { ABSENCE_TYPES } from '../../services/conge.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { UserService } from '../../../users/services/user.service';
import {
  PlanningService,
  WeeklyPlanningResponse,
  EmployeePlanning,
  DayAssignment,
  ShiftSimple,
  ShiftOption,
  SavePlanningCommentDto,
  SetSaturdayHistoryDto
} from '../../services/planning.service';
import { contractLevelLabel } from '../../../../core/hr/user-hr-display.util';

@Component({
  selector: 'app-planning-view',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './planning-view.component.html',
  styleUrls: ['./planning-view.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class PlanningViewComponent implements OnInit {

  readonly contractLevelLabel = contractLevelLabel;
  planning:     WeeklyPlanningResponse | null = null;
  shifts:       ShiftSimple[] = [];
  loading       = false;
  publishing    = false;
  regenerating  = false;
  hasConsulted  = false;
  consulting    = false;
  canValidate   = false;
  coverageOpen  = false;
  error         = '';
  successMsg    = '';
  private planningUserId: number | null = null;

  /** Surbrillance depuis une demande de changement sans switch. */
  highlightAssignmentId: number | null = null;
  highlightUserId: number | null = null;
  highlightDay: string | null = null;
  changeRequestId: number | null = null;
  markingChangeRequest = false;

  // ── Override shift modal ──
  showOverride             = false;
  selectedAssignmentId     = 0;
  selectedEmployeeName     = '';
  selectedDay              = '';
  selectedNewShiftConfigId = 0;       // ✅ SubServiceShiftConfig (nouveau système)
  weekShiftConfigs: any[]  = [];      // ✅ configs chargées une fois pour lun–ven + sam

  // ── Override pause modal ──
  showBreakOverride         = false;
  selectedBreakAssignmentId = 0;
  selectedBreakEmployeeName = '';
  selectedBreakDay          = '';
  selectedNewBreakTime      = '';
  breakSlotOptions: ShiftOption[] = [];

  // ── Day insights modal (diagrammes) ──
  showDayInsights = false;
  insightsDay = '';
  insightsDateLabel = '';
  availHoverIndex: number | null = null;
  availTooltipLeft = 50;
  availTooltipTop = 0;

  // ── Commentaire modal ──
  showCommentModal    = false;
  commentEmployeeId   = 0;
  commentEmployeeName = '';
  commentText         = '';
  savingComment       = false;
// ── Override FÉRIÉ modal ── (ajouter avec les autres variables)
showHolidayOverride         = false;
selectedHolidayEmployeeId   = 0;
selectedHolidayEmployeeName = '';
selectedHolidayAssignmentId = 0;
selectedHolidayDay          = '';
selectedHolidayAction       = 'shift'; // 'shift' | 'off'
selectedHolidayShiftId      = 0;
  // ── Override Samedi modal ──
  showSaturdayOverride         = false;
  selectedSaturdayEmployeeId   = 0;
  selectedSaturdayEmployeeName = '';
  selectedSaturdayAssignmentId = 0;
  selectedSaturdayAction       = ''; // 'work' = OFF → faire travailler | 'off' = WORK → mettre OFF
  selectedSaturdayShiftId      = 0;
  savingSaturday               = false;
  private currentSubServiceId  = 0;
  saturdayShiftConfigs: any[]  = [];  // ✅ alias de weekShiftConfigs pour le modal samedi

  readonly days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  readonly dayLabels: Record<string, string> = {
    Monday: 'Lun', Tuesday: 'Mar', Wednesday: 'Mer',
    Thursday: 'Jeu', Friday: 'Ven', Saturday: 'Sam'
  };
  readonly absenceTypes = ABSENCE_TYPES;

  private readonly shiftColorPalette = [
    'shift-color-1', 'shift-color-2', 'shift-color-3', 'shift-color-4',
    'shift-color-5', 'shift-color-6', 'shift-color-7', 'shift-color-8',
  ];

  private shiftColorMap: Record<string, string> = {};

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private planningService: PlanningService,
    private userService: UserService,
    private session: KyntusSessionService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const role = this.session.getRole();
    this.canValidate = role === 'Admin' || role === 'RH';

    const qp = this.route.snapshot.queryParamMap;
    const ha = Number(qp.get('highlightAssignmentId'));
    const hu = Number(qp.get('highlightUserId'));
    const cr = Number(qp.get('changeRequestId'));
    this.highlightAssignmentId = Number.isFinite(ha) && ha > 0 ? ha : null;
    this.highlightUserId = Number.isFinite(hu) && hu > 0 ? hu : null;
    this.highlightDay = qp.get('highlightDay');
    this.changeRequestId = Number.isFinite(cr) && cr > 0 ? cr : null;

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.userService.getCurrentUser().subscribe({
        next: (user) => {
          this.planningUserId = user?.id ?? null;
          this.loadPlanning(+id);
        },
        error: () => {
          const authId = this.session.getAuthUserId();
          if (authId > 0) {
            this.userService.getUserByAuthId(authId).subscribe({
              next: (user) => {
                this.planningUserId = user?.id ?? null;
                this.loadPlanning(+id);
              },
              error: () => this.loadPlanning(+id),
            });
          } else {
            this.loadPlanning(+id);
          }
        },
      });
      this.loadShifts();
    }
    this.breakSlotOptions = this.planningService.getBreakSlotOptions();
  }

  // ── Chargement planning ────────────────────────────
  loadPlanning(id: number): void {
    this.loading = true;
    this.cdr.detectChanges();
    this.planningService.getById(id).subscribe({
      next: data => {
        this.planning = data;
        this.loading  = false;
        this.buildShiftColorMap(data);

        if (data.subServiceId && data.weekCode) {
          this.planningService.getShiftConfigsForSaturday(
            data.subServiceId,
            data.weekCode
          ).subscribe(configs => {
            this.weekShiftConfigs    = configs;
            this.saturdayShiftConfigs = configs;
            this.cdr.detectChanges();
          });
        }

        // Ouvrir l’écran = consultation : Valider actif tout de suite
        if (this.canValidate && data.status === 'Draft') {
          this.hasConsulted = true;
          this.recordConsultation();
        }

        this.cdr.detectChanges();
        if (this.highlightAssignmentId) {
          setTimeout(() => this.scrollToHighlightedCell(), 80);
        }
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  /** Persiste la consultation côté API (requis pour publier) sans bloquer le bouton. */
  private recordConsultation(): void {
    if (!this.planning || !this.planningUserId || this.planning.status !== 'Draft') return;
    this.consulting = true;
    this.planningService.consultPlanning(this.planning.id, this.planningUserId).subscribe({
      next: () => {
        this.hasConsulted = true;
        this.consulting = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.consulting = false;
        this.cdr.detectChanges();
      },
    });
  }

  buildShiftColorMap(planning: WeeklyPlanningResponse): void {
    this.shiftColorMap = {};
    const labels = [...new Set(
      planning.assignments.flatMap(e => e.days.map(d => d.shiftLabel))
    )].filter(l => l !== 'CONGÉ' && l !== '—');

    labels.forEach((label, index) => {
      this.shiftColorMap[label] =
        this.shiftColorPalette[index % this.shiftColorPalette.length];
    });
  }

  loadShifts(): void {
    this.planningService.getShifts().subscribe({
      next: s => { this.shifts = s; this.cdr.detectChanges(); }
    });
  }

  getShiftColor(label: string): string {
    if (!label || label === 'CONGÉ' || label === '—') return 'shift-off-color';
    return this.shiftColorMap[label] ?? 'shift-color-1';
  }

  // ── Validation ────────────────────────────────────
  publishPlanning(): void {
    if (!this.planning || !this.canValidate) return;

    const under = this.planning.coverageReport?.hasUnderstaffing;
    if (under) {
      const details = (this.planning.coverageReport?.warnings ?? [])
        .filter(w => !w.includes('débutant'))
        .slice(0, 5)
        .join('\n');
      const ok = confirm(
        `Points de couverture à vérifier.\n\n${details}\n\nValider quand même ?`,
      );
      if (!ok) return;
    }

    this.publishing = true;
    this.error = '';

    if (!this.planningUserId) {
      this.error = 'Session expirée — veuillez vous reconnecter.';
      this.publishing = false;
      this.cdr.detectChanges();
      return;
    }

    const userId = this.planningUserId;
    const planningId = this.planning.id;

    // Garantit la consultation API avant publish (ouverture de l’écran = consultation)
    this.planningService.consultPlanning(planningId, userId).pipe(
      switchMap(() => this.planningService.publish(planningId, userId)),
    ).subscribe({
      next: data => {
        this.planning   = data;
        this.hasConsulted = true;
        this.publishing = false;
        this.successMsg = 'Planning validé !';
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = err.error?.message || 'Erreur validation';
        this.publishing = false;
        this.cdr.detectChanges();
      }
    });
  }

  /** Régénère le brouillon à partir du modèle de shifts (écrase les overrides). */
  regeneratePlanning(): void {
    if (!this.planning || !this.canValidate || this.planning.status !== 'Draft') return;
    const ok = confirm(
      'Régénérer ce planning ?\n\nLes affectations et modifications manuelles de ce brouillon seront recalculées depuis le modèle de shifts.',
    );
    if (!ok) return;

    this.regenerating = true;
    this.error = '';
    this.successMsg = '';

    this.planningService.generateFromConfig({
      subServiceId: this.planning.subServiceId,
      weekCode: this.planning.weekCode,
      weeklyPlanningId: this.planning.id,
    }).subscribe({
      next: () => {
        this.regenerating = false;
        this.successMsg = 'Planning régénéré.';
        this.loadPlanning(this.planning!.id);
        setTimeout(() => { this.successMsg = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.regenerating = false;
        this.error = err.error?.message ?? 'Erreur régénération.';
        this.cdr.detectChanges();
      },
    });
  }

  get coverageWarnings(): string[] {
    return this.planning?.coverageReport?.warnings ?? [];
  }

  get coverageQuotaWarnings(): string[] {
    return this.coverageWarnings.filter((w) => w.includes('(quota)'));
  }

  get coveragePresenceWarnings(): string[] {
    return this.coverageWarnings.filter(
      (w) => !w.includes('(quota)') && !w.includes('débutant'),
    );
  }

  get levelBalanceAnomalies(): { message: string }[] {
    return this.planning?.coverageReport?.levelBalanceAnomalies ?? [];
  }

  get hasLevelBalanceAnomaly(): boolean {
    return !!this.planning?.coverageReport?.hasLevelBalanceAnomaly;
  }

  daySynthesisFor(day: string) {
    return this.planning?.coverageReport?.daySynthesis?.find(d => d.day === day) ?? null;
  }

  get weekPerfKpis(): {
    plateau: number;
    plateauTarget: number;
    level: number;
    rotation: number;
    rotationOk: number;
    rotationTotal: number;
  } | null {
    const r = this.planning?.coverageReport;
    if (!r || r.plateauAvailabilityPercent == null) return null;
    const total = r.rotationEmployeesCount ?? 0;
    const violators = r.rotationViolatorsCount ?? 0;
    return {
      plateau: r.plateauAvailabilityPercent,
      plateauTarget: r.plateauAvailabilityTargetPercent ?? 70,
      level: r.levelBalancePercent ?? 100,
      rotation: r.rotationCompliancePercent ?? 100,
      rotationOk: Math.max(0, total - violators),
      rotationTotal: total,
    };
  }

  /** Couleur KPI : vert ≥90 / ambre ≥70 / rouge &lt;70 ; plateau vert si ≥ cible. */
  kpiTone(
    value: number | null | undefined,
    opts?: { plateauTarget?: number },
  ): 'ok' | 'warn' | 'bad' {
    const v = value ?? 100;
    if (opts?.plateauTarget != null) {
      return v >= opts.plateauTarget ? 'ok' : v >= 70 ? 'warn' : 'bad';
    }
    if (v >= 90) return 'ok';
    if (v >= 70) return 'warn';
    return 'bad';
  }

  formatKpiPct(value: number | null | undefined): string {
    if (value == null || Number.isNaN(value)) return '—';
    return `${Math.round(value * 10) / 10} %`;
  }

  openDayInsights(day: string, event?: Event): void {
    event?.stopPropagation();
    this.insightsDay = day;
    this.insightsDateLabel = this.planning?.weekStartDate
      ? this.getDateForDay(this.planning.weekStartDate, day)
      : '';
    this.showDayInsights = true;
  }

  closeDayInsights(): void {
    this.showDayInsights = false;
    this.insightsDay = '';
    this.insightsDateLabel = '';
    this.clearAvailHover();
  }

  get insightsSyn() {
    return this.insightsDay ? this.daySynthesisFor(this.insightsDay) : null;
  }

  get insightsTarget(): number {
    return this.planning?.coverageReport?.plateauAvailabilityTargetPercent ?? 70;
  }

  get availHoverPoint() {
    const pts = this.insightsSyn?.availabilityTimeline ?? [];
    if (this.availHoverIndex == null || this.availHoverIndex < 0 || this.availHoverIndex >= pts.length) {
      return null;
    }
    return pts[this.availHoverIndex];
  }

  clearAvailHover(): void {
    this.availHoverIndex = null;
  }

  onAvailChartMove(event: MouseEvent, syn = this.insightsSyn): void {
    const pts = syn?.availabilityTimeline ?? [];
    if (pts.length === 0) {
      this.clearAvailHover();
      return;
    }

    const el = event.currentTarget as HTMLElement;
    const rect = el.getBoundingClientRect();
    const xPct = Math.max(0, Math.min(1, (event.clientX - rect.left) / Math.max(1, rect.width)));
    const n = pts.length;
    const idx = n === 1 ? 0 : Math.round(xPct * (n - 1));
    this.availHoverIndex = idx;
    this.availTooltipLeft = n === 1 ? 50 : (idx / (n - 1)) * 100;
    const pct = Math.max(0, Math.min(100, Number(pts[idx].availabilityPercent)));
    // Align with SVG y (0% at bottom of plot ≈ 40 in viewBox of height 48 → ~83% from top of svg)
    this.availTooltipTop = ((40 - pct * 0.38) / 48) * 100;
  }

  /** SVG polyline points for availability % (viewBox 0 0 100 40). */
  availabilityChartPoints(syn = this.insightsSyn): { x: number; y: number }[] {
    const pts = syn?.availabilityTimeline ?? [];
    if (pts.length === 0) return [];
    const n = pts.length;
    return pts.map((p, i) => ({
      x: n === 1 ? 50 : (i / (n - 1)) * 100,
      y: 40 - Math.max(0, Math.min(100, Number(p.availabilityPercent))) * 0.38,
    }));
  }

  availHoverMarker(syn = this.insightsSyn): { x: number; y: number } | null {
    if (this.availHoverIndex == null) return null;
    const pts = this.availabilityChartPoints(syn);
    return pts[this.availHoverIndex] ?? null;
  }

  availabilityPolyline(syn = this.insightsSyn): string {
    return this.availabilityChartPoints(syn)
      .map((p) => `${p.x.toFixed(2)},${p.y.toFixed(2)}`)
      .join(' ');
  }

  availabilityAreaPath(syn = this.insightsSyn): string {
    const pts = this.availabilityChartPoints(syn);
    if (pts.length === 0) return '';
    const line = pts.map((p) => `L ${p.x.toFixed(2)} ${p.y.toFixed(2)}`).join(' ');
    const lastX = pts[pts.length - 1].x;
    return `M 0 40 ${line} L ${lastX.toFixed(2)} 40 Z`;
  }

  targetLineY(): number {
    return 40 - Math.max(0, Math.min(100, this.insightsTarget)) * 0.38;
  }

  availabilityTickLabels(syn = this.insightsSyn): { x: number; label: string }[] {
    const pts = syn?.availabilityTimeline ?? [];
    if (pts.length === 0) return [];
    const n = pts.length;
    const indexes =
      n <= 8
        ? pts.map((_, i) => i)
        : [0, Math.floor((n - 1) / 3), Math.floor((2 * (n - 1)) / 3), n - 1].filter(
            (v, i, a) => a.indexOf(v) === i,
          );
    return indexes.map((i) => ({
      x: n === 1 ? 50 : (i / (n - 1)) * 100,
      label: pts[i].time,
    }));
  }

  maxQuotaCount(syn = this.insightsSyn): number {
    const shifts = syn?.shifts ?? [];
    if (!shifts.length) return 1;
    return Math.max(1, ...shifts.map((s) => Math.max(s.assignedCount, s.requiredCount)));
  }

  maxLevelCount(syn = this.insightsSyn): number {
    if (!syn) return 1;
    if (syn.day === 'Saturday') {
      return Math.max(1, syn.saturdayBeginners ?? 0, syn.saturdaySeniors ?? 0);
    }
    const shifts = syn.shifts ?? [];
    if (!shifts.length) return 1;
    return Math.max(
      1,
      ...shifts.map((s) => Math.max(s.beginnerCount ?? 0, s.seniorCount ?? 0)),
    );
  }

  /** Chips actionnables sous l’en-tête de jour (sous-effectif, excédent, débutant seul). */
  dayDecisionChips(day: string): { label: string; kind: 'shortage' | 'surplus' | 'alone' }[] {
    const syn = this.daySynthesisFor(day);
    if (!syn) return [];
    const chips: { label: string; kind: 'shortage' | 'surplus' | 'alone' }[] = [];

    if (day === 'Saturday') {
      const beginners = syn.saturdayBeginners ?? 0;
      const seniors = syn.saturdaySeniors ?? 0;
      if (beginners > 0 && seniors === 0) {
        chips.push({ label: 'Débutant seul · samedi', kind: 'alone' });
      }
      return chips;
    }

    if (!syn.shifts?.length) return chips;

    for (const s of syn.shifts) {
      const delta = s.delta ?? 0;
      if (delta < 0) {
        chips.push({ label: `Manque ${Math.abs(delta)} · ${s.shiftLabel}`, kind: 'shortage' });
      } else if (delta > 0) {
        chips.push({ label: `Excédent ${delta} · ${s.shiftLabel}`, kind: 'surplus' });
      }
      if (s.hasLevelBalanceAnomaly) {
        chips.push({
          label: `Débutant seul · ${this.shiftKindContext(s.shiftKind)}`,
          kind: 'alone',
        });
      }
    }
    return chips;
  }

  private shiftKindContext(kind?: string): string {
    switch ((kind ?? '').toLowerCase()) {
      case 'opening':
        return 'ouverture';
      case 'closing':
        return 'fermeture';
      default:
        return 'milieu';
    }
  }

  daySynthTooltip(day: string): string {
    const parts = this.dayDecisionChips(day).map((c) => c.label);
    const syn = this.daySynthesisFor(day);
    if (syn?.leaveCount) parts.unshift(`${syn.leaveCount} absence(s)`);
    return parts.join(' · ');
  }

  dayHasLevelAnomaly(day: string): boolean {
    const syn = this.daySynthesisFor(day);
    if (day === 'Saturday') {
      const beginners = syn?.saturdayBeginners ?? 0;
      const seniors = syn?.saturdaySeniors ?? 0;
      return beginners > 0 && seniors === 0;
    }
    return !!syn?.shifts?.some(s => s.hasLevelBalanceAnomaly);
  }

  getAssignment(employee: EmployeePlanning, day: string): DayAssignment | null {
    return employee.days.find(d => d.day === day) ?? null;
  }

  isHighlightedAssignment(a: DayAssignment | null): boolean {
    if (!a || !this.highlightAssignmentId) return false;
    return a.assignmentId === this.highlightAssignmentId;
  }

  isHighlightedEmployee(emp: EmployeePlanning): boolean {
    return this.highlightUserId != null && emp.userId === this.highlightUserId;
  }

  private scrollToHighlightedCell(): void {
    const el = document.querySelector('.cell-change-request-highlight');
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
    }
  }

  markChangeRequestTreated(): void {
    if (!this.changeRequestId || this.markingChangeRequest) return;
    const authUserId = this.session.getAuthUserId();
    if (!authUserId) {
      this.error = 'Session invalide — reconnectez-vous.';
      return;
    }
    this.markingChangeRequest = true;
    this.planningService.approveChangeRequest(this.changeRequestId, authUserId).subscribe({
      next: () => {
        this.markingChangeRequest = false;
        this.successMsg = 'Demande marquée comme traitée.';
        this.changeRequestId = null;
        this.highlightAssignmentId = null;
        this.highlightUserId = null;
        this.highlightDay = null;
        this.cdr.detectChanges();
        setTimeout(() => this.goToChangeRequests(), 800);
      },
      error: (err) => {
        this.markingChangeRequest = false;
        this.error = err.error?.message ?? 'Impossible de clôturer la demande.';
        this.cdr.detectChanges();
      },
    });
  }

  goToChangeRequests(): void {
    void this.router.navigate(['/planning/change-requests']);
  }

  // ── Override SHIFT (lun–ven) ──────────────────────
  openOverride(employee: EmployeePlanning, day: string, event: Event): void {
    event.stopPropagation();
    if (day === 'Saturday') return;
    const assignment = this.getAssignment(employee, day);
    if (!assignment || assignment.isOnLeave) return;

    this.selectedAssignmentId     = assignment.assignmentId;
    this.selectedEmployeeName     = employee.fullName;
    this.selectedDay              = this.dayLabels[day] ?? day;
    this.selectedNewShiftConfigId = 0; // ✅ reset
    this.showOverride             = true;
    this.cdr.detectChanges();
  }

  closeOverride(): void {
    this.showOverride = false;
    this.cdr.detectChanges();
  }

  confirmOverride(): void {
    if (!this.selectedNewShiftConfigId) return; // ✅

    this.planningService.overrideShift({
      shiftAssignmentId:          this.selectedAssignmentId,
      newShiftId:                 0,                             // ancien système inutilisé
      newSubServiceShiftConfigId: this.selectedNewShiftConfigId  // ✅ nouveau système
    }).subscribe({
      next: () => {
        this.showOverride = false;
        this.loadPlanning(this.planning!.id);
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Override PAUSE ────────────────────────────────
  openBreakOverride(employee: EmployeePlanning, day: string, event: Event): void {
    event.stopPropagation();
    const assignment = this.getAssignment(employee, day);
    if (!assignment || assignment.isOnLeave || !assignment.breakTime) return;

    this.selectedBreakAssignmentId = assignment.assignmentId;
    this.selectedBreakEmployeeName = employee.fullName;
    this.selectedBreakDay          = this.dayLabels[day] ?? day;
    this.selectedNewBreakTime      = assignment.breakTime;
    this.breakSlotOptions          = this.resolveBreakSlotOptions(assignment);
    this.showBreakOverride         = true;
    this.cdr.detectChanges();
  }

  private resolveBreakSlotOptions(assignment: DayAssignment): ShiftOption[] {
    const cfg = this.planning?.shiftConfigs?.find(
      (c) => c.shiftLabel === assignment.shiftLabel || c.startTime === assignment.startTime
    );
    const duration = cfg?.breakDurationMinutes && cfg.breakDurationMinutes > 0
      ? cfg.breakDurationMinutes
      : 60;
    const slots = cfg?.breakSlots?.length
      ? cfg.breakSlots
      : this.planningService.getBreakSlotOptions().map((o) => o.value);

    return slots.map((start) => {
      const end = this.addMinutesToTime(start, duration);
      return {
        value: start,
        label: end ? `${start} → ${end}` : start,
      };
    });
  }

  private addMinutesToTime(time: string, minutes: number): string {
    const [h, m] = time.split(':').map(Number);
    if (Number.isNaN(h) || Number.isNaN(m)) return '';
    const total = h * 60 + m + minutes;
    const eh = Math.floor(total / 60) % 24;
    const em = total % 60;
    return `${eh.toString().padStart(2, '0')}:${em.toString().padStart(2, '0')}`;
  }

  closeBreakOverride(): void {
    this.showBreakOverride = false;
    this.cdr.detectChanges();
  }

  confirmBreakOverride(): void {
    if (!this.selectedNewBreakTime) return;
    this.planningService.overrideBreakTime({
      shiftAssignmentId: this.selectedBreakAssignmentId,
      newBreakTime:      this.selectedNewBreakTime
    }).subscribe({
      next: () => {
        this.showBreakOverride = false;
        this.loadPlanning(this.planning!.id);
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Override SAMEDI ───────────────────────────────
  openSaturdayOverride(employee: EmployeePlanning, event: Event): void {
    event.stopPropagation();
    if (!this.planning || this.planning.status !== 'Draft') return;

    const assignment = this.getAssignment(employee, 'Saturday');

    this.selectedSaturdayEmployeeId   = employee.userId;
    this.selectedSaturdayEmployeeName = employee.fullName;
    this.selectedSaturdayAssignmentId = assignment?.assignmentId ?? 0;
    this.selectedSaturdayAction       = assignment && !assignment.isOnLeave ? 'off' : 'work';this.selectedSaturdayAction = (assignment && !assignment.isOnLeave) || assignment?.isOnLeave
  ? 'off'
  : 'work';
    this.selectedSaturdayShiftId      = 0;
    this.showSaturdayOverride         = true;

    // ✅ Réutiliser weekShiftConfigs déjà chargés, sinon recharger
    if (this.weekShiftConfigs.length > 0) {
      this.saturdayShiftConfigs = this.weekShiftConfigs;
      this.cdr.detectChanges();
    } else if (this.planning.subServiceId && this.planning.weekCode) {
      this.planningService.getShiftConfigsForSaturday(
        this.planning.subServiceId,
        this.planning.weekCode
      ).subscribe(configs => {
        this.weekShiftConfigs     = configs;
        this.saturdayShiftConfigs = configs;
        this.cdr.detectChanges();
      });
    }

    this.cdr.detectChanges();
  }

  closeSaturdayOverride(): void {
    this.showSaturdayOverride = false;
    this.cdr.detectChanges();
  }

  confirmSaturdayOverride(): void {
  if (!this.planning) return;
  this.savingSaturday = true;
  this.error          = '';
 
  if (this.selectedSaturdayAction === 'off') {
    // ✅ FIX — WORK → OFF :
    // 1. Supprimer l'assignation samedi en base
    // 2. Sauvegarder dans l'historique
    this.planningService.setSaturdayOff(
      this.planning.id,
      this.selectedSaturdayEmployeeId
    ).subscribe({
      next: () => {
        // Sauvegarder dans l'historique (non bloquant)
        const dto: SetSaturdayHistoryDto = {
          subServiceId: this.planning!.subServiceId,
          weekCode:     this.planning!.weekCode,
          entries:      [{ userId: this.selectedSaturdayEmployeeId, workedSaturday: false }]
        };
        this.planningService.saveSaturdayHistory(dto).subscribe();
 
        this.savingSaturday       = false;
        this.showSaturdayOverride = false;
        this.successMsg           = 'Samedi mis à OFF !';
        this.loadPlanning(this.planning!.id);
        setTimeout(() => { this.successMsg = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: err => {
        this.savingSaturday = false;
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
 
  } else {
    // OFF → WORK
    if (!this.selectedSaturdayShiftId) {
      this.savingSaturday = false;
      return;
    }
 
    this.planningService.overrideSaturdayShift({
      shiftAssignmentId:          this.selectedSaturdayAssignmentId,
      newSubServiceShiftConfigId: this.selectedSaturdayShiftId,
      weeklyPlanningId:           this.planning.id,
      userId:                     this.selectedSaturdayEmployeeId
    }).subscribe({
      next: () => {
        // Sauvegarder dans l'historique
        const dto: SetSaturdayHistoryDto = {
          subServiceId: this.planning!.subServiceId,
          weekCode:     this.planning!.weekCode,
          entries: [{ userId: this.selectedSaturdayEmployeeId, workedSaturday: true }]
        };
        this.planningService.saveSaturdayHistory(dto).subscribe();
 
        this.savingSaturday       = false;
        this.showSaturdayOverride = false;
        this.successMsg           = 'Samedi mis à jour !';
        this.loadPlanning(this.planning!.id);
        setTimeout(() => { this.successMsg = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: err => {
        this.savingSaturday = false;
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }
}

  private getSubServiceIdFromPlanning(): number {
    return this.planning?.subServiceId ?? 0;
  }
// ── Override FÉRIÉ ─── (ajouter avec les autres méthodes)
openHolidayOverride(employee: EmployeePlanning, day: string, event: Event): void {
  event.stopPropagation();
  if (!this.planning || this.planning.status !== 'Draft') return;

  const assignment = this.getAssignment(employee, day);
  if (!assignment || !assignment.isHoliday) return;

  this.selectedHolidayEmployeeId   = employee.userId;
  this.selectedHolidayEmployeeName = employee.fullName;
  this.selectedHolidayAssignmentId = assignment.assignmentId;
  this.selectedHolidayDay          = this.dayLabels[day] ?? day;
  this.selectedHolidayAction       = 'shift';
  this.selectedHolidayShiftId      = 0;
  this.showHolidayOverride         = true;
  this.cdr.detectChanges();
}

closeHolidayOverride(): void {
  this.showHolidayOverride = false;
  this.cdr.detectChanges();
}

confirmHolidayOverride(): void {
  if (!this.planning) return;

  if (this.selectedHolidayAction === 'off') {
    // Mettre en repos — réutiliser setSaturdayOff ou overrideShift avec repos
    this.planningService.overrideShift({
      shiftAssignmentId:          this.selectedHolidayAssignmentId,
      newShiftId:                 0,
      newSubServiceShiftConfigId: 0  // 0 = repos
    }).subscribe({
      next: () => {
        this.showHolidayOverride = false;
        this.successMsg = 'Jour modifié en repos !';
        this.loadPlanning(this.planning!.id);
        setTimeout(() => { this.successMsg = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  } else {
    if (!this.selectedHolidayShiftId) return;
    this.planningService.overrideShift({
      shiftAssignmentId:          this.selectedHolidayAssignmentId,
      newShiftId:                 0,
      newSubServiceShiftConfigId: this.selectedHolidayShiftId
    }).subscribe({
      next: () => {
        this.showHolidayOverride = false;
        this.successMsg = 'Shift assigné sur jour férié !';
        this.loadPlanning(this.planning!.id);
        setTimeout(() => { this.successMsg = ''; this.cdr.detectChanges(); }, 3000);
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }
}
  // ── COMMENTAIRE ───────────────────────────────────
  openCommentModal(employee: EmployeePlanning, event: Event): void {
    event.stopPropagation();
    this.commentEmployeeId   = employee.userId;
    this.commentEmployeeName = employee.fullName;
    this.commentText         = employee.managerComment ?? '';
    this.showCommentModal    = true;
    this.error               = '';
    this.cdr.detectChanges();
  }

  closeCommentModal(): void {
    this.showCommentModal = false;
    this.cdr.detectChanges();
  }

  saveComment(): void {
    if (!this.commentText.trim() || !this.planning) return;
    this.savingComment = true;
    this.error         = '';

    const authUserId = this.session.getAuthUserId();
    if (!authUserId) {
      this.error = 'Session expirée — veuillez vous reconnecter.';
      this.savingComment = false;
      this.cdr.detectChanges();
      return;
    }

    this.userService.getCurrentUser().pipe(
      switchMap((user) => {
        const dto: SavePlanningCommentDto = {
          weeklyPlanningId: this.planning!.id,
          userId:           this.commentEmployeeId,
          comment:          this.commentText.trim(),
          createdBy:        user.id,
        };
        return this.planningService.saveComment(dto);
      }),
    ).subscribe({
      next: () => {
        this.savingComment    = false;
        this.showCommentModal = false;
        this.successMsg       = 'Commentaire sauvegardé !';
        this.loadPlanning(this.planning!.id);
        this.cdr.detectChanges();
        setTimeout(() => { this.successMsg = ''; this.cdr.detectChanges(); }, 3000);
      },
      error: err => {
        this.savingComment = false;
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }

  deleteComment(employee: EmployeePlanning, event: Event): void {
    event.stopPropagation();
    if (!this.planning) return;
    if (!confirm(`Supprimer le commentaire pour ${employee.fullName} ?`)) return;

    this.planningService.deleteComment(this.planning.id, employee.userId)
      .subscribe({
        next: () => {
          this.loadPlanning(this.planning!.id);
          this.cdr.detectChanges();
        },
        error: err => {
          this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
          this.cdr.detectChanges();
        }
      });
  }

  // ── Stats ─────────────────────────────────────────
  getShiftCount(shiftLabel: string): number {
    if (!this.planning) return 0;
    return this.planning.assignments.reduce((total, emp) =>
      total + emp.days.filter(d => d.shiftLabel === shiftLabel).length, 0);
  }

  getUniqueShiftLabels(): string[] {
    if (!this.planning) return [];
    return Object.keys(this.shiftColorMap);
  }

  // ── Navigation ────────────────────────────────────
  goBack(): void { this.goBackToWeekList(); }

  /** Retour à la liste des plannings de la semaine (validation). */
  goBackToWeekList(): void {
    const qp = this.route.snapshot.queryParamMap;
    const weekCode =
      qp.get('weekCode') ||
      this.planning?.weekCode ||
      undefined;
    const weekStartRaw = this.planning?.weekStartDate;
    const weekStart = weekStartRaw ? weekStartRaw.split('T')[0] : undefined;

    void this.router.navigate(['/planning/validation'], {
      queryParams: {
        ...(weekCode ? { weekCode } : {}),
        ...(weekStart ? { weekStart } : {}),
      },
    });
  }

  getStatusClass(status: string): string {
    return ({ Draft: 'st-draft', Published: 'st-published' } as any)[status] ?? '';
  }

  getStatusLabel(status: string): string {
    return ({ Draft: 'À valider', Published: 'Validé' } as any)[status] ?? status;
  }
  
  getAbsenceLabel(value: string | null): string {
  if (!value) return 'Congé';
  return this.absenceTypes.find(t => t.value === value)?.label ?? 'Congé';
}
getShiftNumber(label: string): string {
  // "Shift 1" → "1" | "Shift 2" → "2"
  const match = label?.match(/\d+/);
  return match ? match[0] : label;
}
  getDateForDay(weekStartDate: string, day: string): string {
    const offsets: Record<string, number> = {
      Monday: 0, Tuesday: 1, Wednesday: 2,
      Thursday: 3, Friday: 4, Saturday: 5
    };
    const d = new Date(weekStartDate);
    d.setDate(d.getDate() + (offsets[day] ?? 0));
    return d.getDate() + '/' + (d.getMonth() + 1);
  }
}