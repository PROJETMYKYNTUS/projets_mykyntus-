import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
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

@Component({
  selector: 'app-planning-view',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './planning-view.component.html',
  styleUrls: ['./planning-view.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class PlanningViewComponent implements OnInit {

  planning:     WeeklyPlanningResponse | null = null;
  shifts:       ShiftSimple[] = [];
  loading       = false;
  publishing    = false;
  error         = '';
  successMsg    = '';

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

  // ── Commentaire modal ──
  showCommentModal    = false;
  commentEmployeeId   = 0;
  commentEmployeeName = '';
  commentText         = '';
  savingComment       = false;

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

  private readonly shiftColorPalette = [
    'shift-color-1', 'shift-color-2', 'shift-color-3', 'shift-color-4',
    'shift-color-5', 'shift-color-6', 'shift-color-7', 'shift-color-8',
  ];

  private shiftColorMap: Record<string, string> = {};

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private planningService: PlanningService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadPlanning(+id);
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

        // ✅ Charger les configs shifts une seule fois pour lun–ven ET samedi
        if (data.subServiceId && data.weekCode) {
          this.planningService.getShiftConfigsForSaturday(
            data.subServiceId,
            data.weekCode
          ).subscribe(configs => {
            this.weekShiftConfigs    = configs;
            this.saturdayShiftConfigs = configs; // partagé avec modal samedi
            this.cdr.detectChanges();
          });
        }

        this.cdr.detectChanges();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
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

  // ── Publication ────────────────────────────────────
  publishPlanning(): void {
    if (!this.planning) return;
    this.publishing = true;
    this.cdr.detectChanges();
    this.planningService.publish(this.planning.id, 3).subscribe({
      next: data => {
        this.planning   = data;
        this.publishing = false;
        this.successMsg = 'Planning publié ! Les employés peuvent voir leur planning.';
        this.cdr.detectChanges();
      },
      error: () => { this.publishing = false; this.cdr.detectChanges(); }
    });
  }

  getAssignment(employee: EmployeePlanning, day: string): DayAssignment | null {
    return employee.days.find(d => d.day === day) ?? null;
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
    this.showBreakOverride         = true;
    this.cdr.detectChanges();
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
    this.selectedSaturdayAction       = assignment && !assignment.isOnLeave ? 'off' : 'work';
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
        this.successMsg           = '✅ Samedi mis à OFF !';
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
        this.successMsg           = '✅ Samedi mis à jour !';
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

    const dto: SavePlanningCommentDto = {
      weeklyPlanningId: this.planning.id,
      userId:           this.commentEmployeeId,
      comment:          this.commentText.trim(),
      createdBy:        3
    };

    this.planningService.saveComment(dto).subscribe({
      next: () => {
        this.savingComment    = false;
        this.showCommentModal = false;
        this.successMsg       = '💬 Commentaire sauvegardé !';
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
  goBack(): void { this.router.navigate(['/planning']); }

  getStatusClass(status: string): string {
    return ({ Draft: 'st-draft', Published: 'st-published' } as any)[status] ?? '';
  }

  getStatusLabel(status: string): string {
    return ({ Draft: '📝 Brouillon', Published: '✅ Publié' } as any)[status] ?? status;
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