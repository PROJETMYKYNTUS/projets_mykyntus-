import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  PlanningService,
  SubServiceSimple,
  ShiftConfigItem,
  SaveShiftConfigDto,
  WeekShiftConfigResponse,
  ShiftOption
} from '../../services/planning.service';

@Component({
  selector: 'app-shift-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './shift-config.component.html',
  styleUrls: ['./shift-config.component.css']
})
export class ShiftConfigComponent implements OnInit {

  // ── Formulaire ──
  subServiceId  = 0;
  weekCode      = '';
  weekStartDate = '';

  // ── State ──
  subServices:      SubServiceSimple[] = [];
  startOptions:     ShiftOption[] = [];
  breakSlotOptions: ShiftOption[] = [];
  savedConfig:      WeekShiftConfigResponse | null = null;
  loading     = false;
  saving      = false;
  generating  = false;
  error       = '';
  successMsg  = '';
  currentWeekCode = '';

  // ── Tableau shifts (ce que le responsable remplit) ──
  shifts: ShiftConfigItem[] = [];

  constructor(
    private planningService: PlanningService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.initCurrentWeek();
    this.loadSubServices();
    this.startOptions     = this.planningService.getShiftStartOptions();
    this.breakSlotOptions = this.planningService.getBreakSlotOptions();
    this.initShifts();
  }

  // ── Initialiser 4 shifts par défaut ──
  initShifts(): void {
    this.shifts = [
      this.createShift('Shift 1', '08:00', 1),
      this.createShift('Shift 2', '09:00', 2),
      this.createShift('Shift 3', '10:00', 3),
      this.createShift('Shift 4', '11:00', 4),
    ];
  }

  createShift(label: string, startTime: string, order: number): ShiftConfigItem {
    return {
      label,
      startTime,
      workHours: 8,
      breakDurationMinutes: 60,
      breakRangeStart: undefined,
      breakRangeEnd: undefined,
      requiredCount: 0,
      minPresencePercent: 70,
      displayOrder: order
    };
  }
  loadSubServices(): void {
  this.planningService.getSubServices().subscribe({
    next: data => {
      this.subServices = data;
      if (data.length > 0) {
        this.subServiceId = data[0].id;
        this.loadExistingConfig();  // charger config existante si elle existe
      }
      this.cdr.detectChanges();
    },
    error: () => { this.cdr.detectChanges(); }
  });
}

  // ── Ajouter un shift ──
  addShift(): void {
    if (this.shifts.length >= 8) return;
    this.shifts.push(
      this.createShift(`Shift ${this.shifts.length + 1}`, '08:00', this.shifts.length + 1)
    );
  }

  // ── Supprimer un shift ──
  removeShift(index: number): void {
    if (this.shifts.length <= 1) return;
    this.shifts.splice(index, 1);
    // Recalculer les displayOrder
    this.shifts.forEach((s, i) => s.displayOrder = i + 1);
  }

  // ── Calcul auto heure de fin ──
  getEndTime(shift: ShiftConfigItem): string {
    return this.planningService.calculateEndTime(shift.startTime, shift.workHours);
  }

  // ── Calcul auto plage pause si non définie ──
  getBreakRangeAuto(shift: ShiftConfigItem): string {
    if (!shift.startTime) return '';
    const [h, m] = shift.startTime.split(':').map(Number);
    const startMin = h * 60 + m;
    const breakStart = startMin + 3 * 60;
    const breakEnd   = startMin + (shift.workHours - 1) * 60;
    const fmt = (min: number) =>
      `${Math.floor(min/60).toString().padStart(2,'0')}:${(min%60).toString().padStart(2,'0')}`;
    return `${fmt(breakStart)} → ${fmt(breakEnd)}`;
  }

  // ── Total effectif ──
  get totalEffectif(): number {
    return this.shifts.reduce((sum, s) => sum + (s.requiredCount || 0), 0);
  }

  // ── Pourcentage par shift ──
  getPercentage(shift: ShiftConfigItem): number {
    if (this.totalEffectif === 0) return 0;
    return Math.round((shift.requiredCount / this.totalEffectif) * 100);
  }

  // ── Sauvegarder la config ──
  saveConfig(): void {
    if (!this.subServiceId || !this.weekCode) {
      this.error = 'Veuillez sélectionner un sous-service et une semaine.';
      return;
    }
    if (this.totalEffectif === 0) {
      this.error = 'Veuillez définir le nombre d\'employés pour chaque shift.';
      return;
    }

    this.saving = true;
    this.error  = '';
    this.successMsg = '';

    const dto: SaveShiftConfigDto = {
      subServiceId:  this.subServiceId,
      weekCode:      this.weekCode,
      weekStartDate: this.weekStartDate,
      shifts:        this.shifts
    };

    this.planningService.saveShiftConfig(dto).subscribe({
      next: result => {
        this.savedConfig = result;
        this.saving      = false;
        this.successMsg  = `✅ Config sauvegardée — ${result.totalEffectif} employés sur ${result.shifts.length} shifts`;
        this.cdr.detectChanges();
      },
      error: err => {
        this.saving = false;
        this.error  = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Générer le planning depuis la config ──
 // Remplacer generatePlanning() par ces 3 méthodes dans shift-config.component.ts

generatePlanning(): void {
  if (!this.savedConfig) {
    this.error = 'Veuillez d\'abord sauvegarder la configuration.';
    return;
  }

  this.generating = true;
  this.error      = '';
  this.successMsg = '';
  this.cdr.detectChanges();

  this.planningService.create({
    subServiceId:  this.subServiceId,
    weekCode:      this.weekCode,
    weekStartDate: this.weekStartDate,
    totalEffectif: 0  // ✅ recalculé automatiquement côté backend
  }).subscribe({
    next: planning => {
      this.runGenerateFromConfig(planning.id);
    },
    error: err => {
      if (err.status === 409) {
        this.getExistingPlanningAndGenerate();
      } else {
        this.generating = false;
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    }
  });
}

private getExistingPlanningAndGenerate(): void {
  this.planningService.getBySubService(this.subServiceId).subscribe({
    next: plannings => {
      const existing = plannings.find(p => p.weekCode === this.weekCode);
      if (existing) {
        this.runGenerateFromConfig(existing.id);
      } else {
        this.generating = false;
        this.error = 'Planning introuvable après conflit 409.';
        this.cdr.detectChanges();
      }
    },
    error: () => {
      this.generating = false;
      this.error = 'Impossible de récupérer le planning existant.';
      this.cdr.detectChanges();
    }
  });
}

private runGenerateFromConfig(planningId: number): void {
  this.planningService.generateFromConfig({
    subServiceId:     this.subServiceId,
    weekCode:         this.weekCode,
    weeklyPlanningId: planningId
  }).subscribe({
    next: result => {
      this.generating = false;
      this.successMsg = `🎉 Planning ${result.weekCode} généré avec succès !`;
      this.cdr.detectChanges();
      setTimeout(() => this.router.navigate(['/planning/view', result.id]), 1500);
    },
    error: err => {
      this.generating = false;
      this.error = `Erreur génération : ${err.error?.message ?? 'Erreur serveur'}`;
      this.cdr.detectChanges();
    }
  });
}
  // ── Charger config existante ──
  loadExistingConfig(): void {
    if (!this.subServiceId || !this.weekCode) return;
    this.loading = true;

    this.planningService.getShiftConfig(this.subServiceId, this.weekCode).subscribe({
      next: config => {
        this.savedConfig = config;
        // Remplir le tableau avec la config existante
        this.shifts = config.shifts.map(s => ({
          label:                s.label,
          startTime:            s.startTime,
          workHours:            s.workHours,
          breakDurationMinutes: s.breakDurationMinutes,
          breakRangeStart:      s.breakRangeStart,
          breakRangeEnd:        s.breakRangeEnd,
          requiredCount:        s.requiredCount,
          minPresencePercent:   s.minPresencePercent,
          displayOrder:         s.displayOrder
        }));
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        // Pas de config existante → garder les valeurs par défaut
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Events ──
  onSubServiceChange(): void {
    this.savedConfig = null;
    this.loadExistingConfig();
  }

  onWeekChange(): void {
    if (this.weekStartDate) {
      const d = new Date(this.weekStartDate);
      this.weekCode = this.getWeekCode(d);
      this.savedConfig = null;
      this.loadExistingConfig();
    }
  }

  goBack(): void { this.router.navigate(['/planning']); }

  // ── Helpers ──
  initCurrentWeek(): void {
    const today  = new Date();
    const monday = this.getMondayOfWeek(today);
    this.weekStartDate   = this.formatDate(monday);
    this.weekCode        = this.getWeekCode(monday);
    this.currentWeekCode = this.weekCode;
  }

  getMondayOfWeek(date: Date): Date {
    const d   = new Date(date);
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1);
    d.setDate(diff);
    return d;
  }

  getWeekCode(monday: Date): string {
    const year    = monday.getFullYear();
    const weekNum = this.getISOWeek(monday);
    return `${year}-W${weekNum.toString().padStart(2, '0')}`;
  }

  getISOWeek(date: Date): number {
    const d = new Date(date);
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + 3 - (d.getDay() + 6) % 7);
    const week1 = new Date(d.getFullYear(), 0, 4);
    return 1 + Math.round(((d.getTime() - week1.getTime()) / 86400000
      - 3 + (week1.getDay() + 6) % 7) / 7);
  }

  formatDate(d: Date): string {
    return d.toISOString().substring(0, 10);
  }
}