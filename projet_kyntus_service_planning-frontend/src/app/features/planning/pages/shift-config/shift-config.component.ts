import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  PlanningService,
  ShiftConfigItem,
  SaveShiftConfigDto,
  WeekShiftConfigResponse,
  ShiftOption
} from '../../services/planning.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import type { SubService } from '../../../sub-services/sub-services-module';
import type { Department } from '../../../prime/models';
import {
  findOrgSelectionByPrimeServiceId,
  poleCells,
} from '../../../../core/org/planning-org-picker';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { Coffee, Info, Plus, Save, Settings, Trash2, Users } from 'lucide';

type SubServiceOption = {
  id: number;
  name: string;
  orgLabel: string;
  employeesCount: number;
};

@Component({
  selector: 'app-shift-config',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  templateUrl: './shift-config.component.html',
  styleUrls: ['./shift-config.component.css']
})
export class ShiftConfigComponent implements OnInit {
  readonly icons = {
    settings: Settings,
    coffee: Coffee,
    users: Users,
    save: Save,
    plus: Plus,
    trash: Trash2,
    info: Info,
  };

  subServiceId = 0;
  weekCode = '';
  weekStartDate = '';

  orgDepartments: Department[] = [];
  subServiceOptions: SubServiceOption[] = [];
  serviceEmployeeCount = 0;
  weekDateAdjusted = false;

  startOptions: ShiftOption[] = [];
  breakSlotOptions: ShiftOption[] = [];
  savedConfig: WeekShiftConfigResponse | null = null;
  loading = false;
  saving = false;
  generating = false;
  error = '';
  successMsg = '';
  currentWeekCode = '';

  shifts: ShiftConfigItem[] = [];

  constructor(
    private planningService: PlanningService,
    private subServiceService: SubServiceService,
    private http: HttpClient,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.initCurrentWeek();
    this.loadSubServices();
    this.startOptions = this.planningService.getShiftStartOptions();
    this.breakSlotOptions = this.planningService.getBreakSlotOptions();
    this.initShifts();
  }

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
    forkJoin({
      subServices: this.subServiceService.getAllSubServices(),
      departments: this.http.get<Department[]>('/api/prime/departments'),
    }).subscribe({
      next: ({ subServices, departments }) => {
        this.orgDepartments = departments ?? [];
        this.subServiceOptions = this.buildSubServiceOptions(subServices ?? []);
        if (this.subServiceOptions.length > 0) {
          this.subServiceId = this.subServiceOptions[0].id;
          this.onSubServiceChange();
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Impossible de charger les services.';
        this.cdr.detectChanges();
      },
    });
  }

  private buildSubServiceOptions(subServices: SubService[]): SubServiceOption[] {
    return subServices
      .map((sub) => {
        const primeId = sub.primeServiceId?.trim() ?? '';
        const sel = primeId
          ? findOrgSelectionByPrimeServiceId(this.orgDepartments, primeId)
          : null;

        let orgLabel = sub.name;
        if (sel) {
          const dept = this.orgDepartments.find((d) => d.id === sel.poleId);
          const cellule = dept?.poles?.find((p) => p.id === sel.celluleId);
          const service = cellule
            ? poleCells(cellule).find((c) => c.id === sel.serviceId)
            : undefined;
          if (dept && cellule) {
            orgLabel = `${dept.name} / ${cellule.name} / ${service?.name ?? sub.name}`;
          }
        }

        return {
          id: sub.id,
          name: sub.name,
          orgLabel,
          employeesCount: sub.employeesCount ?? 0,
        };
      })
      .sort((a, b) => a.orgLabel.localeCompare(b.orgLabel, 'fr'));
  }

  addShift(): void {
    if (this.shifts.length >= 8) return;
    this.shifts.push(
      this.createShift(`Shift ${this.shifts.length + 1}`, '08:00', this.shifts.length + 1)
    );
  }

  removeShift(index: number): void {
    if (this.shifts.length <= 1) return;
    this.shifts.splice(index, 1);
    this.shifts.forEach((s, i) => s.displayOrder = i + 1);
  }

  getEndTime(shift: ShiftConfigItem): string {
    return this.planningService.calculateEndTime(shift.startTime, shift.workHours);
  }

  getBreakRangeAuto(shift: ShiftConfigItem): string {
    if (!shift.startTime) return '';
    const [h, m] = shift.startTime.split(':').map(Number);
    const startMin = h * 60 + m;
    const breakStart = startMin + 3 * 60;
    const breakEnd = startMin + (shift.workHours - 1) * 60;
    const fmt = (min: number) =>
      `${Math.floor(min / 60).toString().padStart(2, '0')}:${(min % 60).toString().padStart(2, '0')}`;
    return `${fmt(breakStart)} → ${fmt(breakEnd)}`;
  }

  get totalEffectif(): number {
    return this.shifts.reduce((sum, s) => sum + (s.requiredCount || 0), 0);
  }

  getPercentage(shift: ShiftConfigItem): number {
    if (this.totalEffectif === 0) return 0;
    return Math.round((shift.requiredCount / this.totalEffectif) * 100);
  }

  onRequiredCountChange(): void {
    if (
      this.serviceEmployeeCount > 0 &&
      this.totalEffectif > this.serviceEmployeeCount
    ) {
      this.error = `Le total (${this.totalEffectif}) dépasse l'effectif du service (${this.serviceEmployeeCount}).`;
    } else if (this.error.includes('dépasse l\'effectif')) {
      this.error = '';
    }
  }

  saveConfig(): void {
    if (!this.subServiceId || !this.weekCode) {
      this.error = 'Veuillez sélectionner un service et une semaine.';
      return;
    }
    if (this.totalEffectif === 0) {
      this.error = 'Veuillez définir le nombre d\'employés pour chaque shift.';
      return;
    }
    if (this.serviceEmployeeCount > 0 && this.totalEffectif > this.serviceEmployeeCount) {
      this.error = `Le total ne peut pas dépasser ${this.serviceEmployeeCount} employés actifs.`;
      return;
    }

    this.saving = true;
    this.error = '';
    this.successMsg = '';

    const dto: SaveShiftConfigDto = {
      subServiceId: this.subServiceId,
      weekCode: this.weekCode,
      weekStartDate: this.weekStartDate,
      shifts: this.shifts
    };

    this.planningService.saveShiftConfig(dto).subscribe({
      next: result => {
        this.savedConfig = result;
        this.saving = false;
        this.successMsg = `Config sauvegardée — ${result.totalEffectif} employés sur ${result.shifts.length} shifts`;
        this.cdr.detectChanges();
      },
      error: err => {
        this.saving = false;
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      }
    });
  }

  generatePlanning(): void {
    if (!this.savedConfig) {
      this.error = 'Veuillez d\'abord sauvegarder la configuration.';
      return;
    }

    this.generating = true;
    this.error = '';
    this.successMsg = '';
    this.cdr.detectChanges();

    this.planningService.create({
      subServiceId: this.subServiceId,
      weekCode: this.weekCode,
      weekStartDate: this.weekStartDate,
      totalEffectif: this.totalEffectif
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
      subServiceId: this.subServiceId,
      weekCode: this.weekCode,
      weeklyPlanningId: planningId
    }).subscribe({
      next: result => {
        this.generating = false;
        this.successMsg = `Planning ${result.weekCode} généré avec succès.`;
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

  loadExistingConfig(): void {
    if (!this.subServiceId || !this.weekCode) return;
    this.loading = true;

    this.planningService.getShiftConfig(this.subServiceId, this.weekCode).subscribe({
      next: config => {
        this.savedConfig = config;
        this.shifts = config.shifts.map(s => ({
          label: s.label,
          startTime: s.startTime,
          workHours: s.workHours,
          breakDurationMinutes: s.breakDurationMinutes,
          breakRangeStart: s.breakRangeStart,
          breakRangeEnd: s.breakRangeEnd,
          requiredCount: s.requiredCount,
          minPresencePercent: s.minPresencePercent,
          displayOrder: s.displayOrder
        }));
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.savedConfig = null;
        this.initShifts();
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onSubServiceChange(): void {
    const opt = this.subServiceOptions.find((s) => s.id === this.subServiceId);
    this.serviceEmployeeCount = opt?.employeesCount ?? 0;
    this.savedConfig = null;
    this.loadExistingConfig();
  }

  onWeekChange(): void {
    if (!this.weekStartDate) return;
    const picked = this.parseDateInput(this.weekStartDate);
    const monday = this.getMondayOfWeek(picked);
    const mondayStr = this.formatDate(monday);
    this.weekDateAdjusted = mondayStr !== this.weekStartDate;
    this.weekStartDate = mondayStr;
    this.weekCode = this.getWeekCode(monday);
    this.savedConfig = null;
    this.loadExistingConfig();
    this.cdr.detectChanges();
  }

  initCurrentWeek(): void {
    const today = new Date();
    const monday = this.getMondayOfWeek(today);
    this.weekStartDate = this.formatDate(monday);
    this.weekCode = this.getWeekCode(monday);
    this.currentWeekCode = this.weekCode;
  }

  private parseDateInput(value: string): Date {
    const [y, m, d] = value.split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  getMondayOfWeek(date: Date): Date {
    const d = new Date(date);
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1);
    d.setDate(diff);
    return d;
  }

  getWeekCode(monday: Date): string {
    const year = monday.getFullYear();
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
    return d.toLocaleDateString('en-CA');
  }
}
