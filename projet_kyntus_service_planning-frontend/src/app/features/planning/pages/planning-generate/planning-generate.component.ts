// features/planning/pages/planning-generator/planning-generator.component.ts

import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  PlanningService,
  WeeklyPlanningResponse
} from '../../services/planning.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import type { SubService } from '../../../sub-services/sub-services-module';
import type { Department } from '../../../prime/models';
import {
  findOrgSelectionByPrimeServiceId,
  poleCells,
} from '../../../../core/org/planning-org-picker';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import {
  AlertTriangle,
  BarChart3,
  Bot,
  Calendar,
  CheckCircle,
  ClipboardList,
  Inbox,
  Loader2,
  Rocket,
  Trash2,
  Users,
} from 'lucide';

type SubServiceOption = {
  id: number;
  name: string;
  orgLabel: string;
  employeesCount: number;
};

@Component({
  selector: 'app-planning-generate',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  templateUrl: './planning-generate.component.html',
  styleUrls: ['./planning-generate.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class PlanningGenerateComponent implements OnInit {
  readonly icons = {
    calendar: Calendar,
    chart: BarChart3,
    warn: AlertTriangle,
    success: CheckCircle,
    rocket: Rocket,
    list: ClipboardList,
    trash: Trash2,
    bot: Bot,
    inbox: Inbox,
    users: Users,
    loader: Loader2,
  };

  subServiceId = 0;
  weekCode = '';
  weekStartDate = '';
  totalEffectif = 0;

  orgDepartments: Department[] = [];
  subServiceOptions: SubServiceOption[] = [];
  serviceEmployeeCount = 0;
  weekDateAdjusted = false;

  plannings: WeeklyPlanningResponse[] = [];
  loading = false;
  generating = false;
  error = '';
  successMsg = '';
  Math = Math;

  currentWeekCode = '';
  currentWeekStart = '';

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
  }

  initCurrentWeek(): void {
    const today = new Date();
    const monday = this.getMondayOfWeek(today);
    this.weekStartDate = this.formatDate(monday);
    this.currentWeekStart = this.weekStartDate;
    this.weekCode = this.getWeekCode(monday);
    this.currentWeekCode = this.weekCode;
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

  loadPlannings(): void {
    if (!this.subServiceId) return;
    this.loading = true;
    this.cdr.detectChanges();

    this.planningService.getBySubService(this.subServiceId).subscribe({
      next: data => {
        this.plannings = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onSubServiceChange(): void {
    const opt = this.subServiceOptions.find((s) => s.id === this.subServiceId);
    this.serviceEmployeeCount = opt?.employeesCount ?? 0;
    if (
      this.serviceEmployeeCount > 0 &&
      this.totalEffectif > this.serviceEmployeeCount
    ) {
      this.totalEffectif = this.serviceEmployeeCount;
    }
    this.loadPlannings();
  }

  onWeekChange(): void {
    if (!this.weekStartDate) return;
    const picked = this.parseDateInput(this.weekStartDate);
    const monday = this.getMondayOfWeek(picked);
    const mondayStr = this.formatDate(monday);
    this.weekDateAdjusted = mondayStr !== this.weekStartDate;
    this.weekStartDate = mondayStr;
    this.weekCode = this.getWeekCode(monday);
    this.cdr.detectChanges();
  }

  onEffectifChange(): void {
    if (this.serviceEmployeeCount > 0 && this.totalEffectif > this.serviceEmployeeCount) {
      this.totalEffectif = this.serviceEmployeeCount;
    }
    if (this.totalEffectif < 1) {
      this.totalEffectif = 1;
    }
  }

  get canGenerate(): boolean {
    return (
      !this.generating &&
      !!this.subServiceId &&
      !!this.weekStartDate &&
      !!this.weekCode &&
      this.totalEffectif > 0 &&
      (this.serviceEmployeeCount === 0 || this.totalEffectif <= this.serviceEmployeeCount)
    );
  }

  generatePlanning(): void {
    this.onWeekChange();

    if (!this.subServiceId || !this.weekCode || !this.weekStartDate || !this.totalEffectif) {
      this.error = 'Veuillez remplir tous les champs.';
      return;
    }

    if (this.serviceEmployeeCount > 0 && this.totalEffectif > this.serviceEmployeeCount) {
      this.error = `L'effectif ne peut pas dépasser ${this.serviceEmployeeCount} employés actifs.`;
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
        this.planningService.generate({
          weeklyPlanningId: planning.id,
          totalEffectif: this.totalEffectif
        }).subscribe({
          next: result => {
            this.generating = false;
            this.successMsg = `Planning ${result.weekCode} généré avec succès !`;
            this.loadPlannings();
            this.cdr.detectChanges();
            setTimeout(() => this.router.navigate(['/planning/view', result.id]), 1500);
          },
          error: err => {
            this.generating = false;
            this.error = `Erreur génération : ${this.extractError(err)}`;
            this.planningService.delete(planning.id).subscribe({
              next: () => this.loadPlannings(),
              error: () => this.loadPlannings(),
            });
            this.cdr.detectChanges();
          }
        });
      },
      error: err => {
        this.generating = false;
        this.error = err.status === 409
          ? `Planning ${this.weekCode} existe déjà pour ce service.`
          : `Erreur création : ${this.extractError(err)}`;
        this.cdr.detectChanges();
      }
    });
  }

  viewPlanning(id: number): void {
    this.router.navigate(['/planning/view', id]);
  }

  deletePlanning(id: number, event: Event): void {
    event.stopPropagation();
    if (!confirm('Supprimer ce planning ? Cette action est irréversible.')) return;

    this.planningService.delete(id).subscribe({
      next: () => {
        this.plannings = this.plannings.filter(p => p.id !== id);
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = `Erreur suppression : ${this.extractError(err)}`;
        this.cdr.detectChanges();
      }
    });
  }

  private extractError(err: { error?: { message?: string }; message?: string }): string {
    return err.error?.message ?? err.message ?? 'Erreur serveur';
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
    return 1 + Math.round(((d.getTime() - week1.getTime()) / 86400000 - 3 +
      (week1.getDay() + 6) % 7) / 7);
  }

  formatDate(d: Date): string {
    return d.toLocaleDateString('en-CA');
  }

  getStatusClass(status: string): string {
    return ({ Draft: 'st-draft', Published: 'st-published', Archived: 'st-archived' } as Record<string, string>)[status] ?? '';
  }

  getStatusLabel(status: string): string {
    return ({ Draft: 'Brouillon', Published: 'Publié', Archived: 'Archivé' } as Record<string, string>)[status] ?? status;
  }
}
