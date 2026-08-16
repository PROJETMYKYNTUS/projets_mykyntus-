// features/planning/pages/planning-generator/planning-generator.component.ts

import { Component, OnInit, OnDestroy, ViewEncapsulation, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { KyntusFormDraftService } from '../../../../core/drafts/kyntus-form-draft.service';
import { KyntusObjectDraftBinder } from '../../../../core/drafts/kyntus-object-draft.binder';
import {
  PlanningService,
  WeeklyPlanningResponse
} from '../../services/planning.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { UserService } from '../../../users/services/user.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import type { SubService } from '../../../sub-services/sub-services-module';
import type { Department } from '../../../prime/models';
import type { OperationalDepartmentNode, OrgPoleNode } from '../../../prime/models/org-tree.types';
import { buildSubServiceOrgLabels } from '../../../../core/org/operational-org-picker';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
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
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './planning-generate.component.html',
  styleUrls: ['./planning-generate.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class PlanningGenerateComponent implements OnInit, OnDestroy {
  private readonly formDrafts = inject(KyntusFormDraftService);
  private readonly confirmService = inject(KyntusConfirmService);
  private draftBinder?: KyntusObjectDraftBinder<{
    subServiceId: number;
    weekStartDate: string;
    weekCode: string;
    totalEffectif: number;
  }>;

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

  operationalDepartments: OperationalDepartmentNode[] = [];
  unassignedPoles: OrgPoleNode[] = [];
  legacyDepartments: Department[] = [];
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
    private userService: UserService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.initCurrentWeek();
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'planning-generate',
      () => ({
        subServiceId: this.subServiceId,
        weekStartDate: this.weekStartDate,
        weekCode: this.weekCode,
        totalEffectif: this.totalEffectif,
      }),
      (s) => {
        this.subServiceId = s.subServiceId ?? this.subServiceId;
        this.weekStartDate = s.weekStartDate || this.weekStartDate;
        this.weekCode = s.weekCode || this.weekCode;
        this.totalEffectif = s.totalEffectif ?? this.totalEffectif;
      },
    );
    this.loadSubServices();
  }

  ngOnDestroy(): void {
    this.draftBinder?.destroy();
  }

  touchDraft(): void {
    this.draftBinder?.touch();
  }

  async resetDraftForm(): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Réinitialiser',
      message: 'Réinitialiser les paramètres de génération et le brouillon ?',
      confirmLabel: 'Réinitialiser',
    });
    if (!ok) return;
    this.draftBinder?.discard();
    const keepSubServiceId = this.subServiceId;
    this.initCurrentWeek();
    this.subServiceId = keepSubServiceId;
    this.totalEffectif = this.serviceEmployeeCount || 0;
    this.error = '';
    this.successMsg = '';
    this.restartDraftBinder();
    this.cdr.detectChanges();
  }

  private restartDraftBinder(): void {
    this.draftBinder?.destroy();
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'planning-generate',
      () => ({
        subServiceId: this.subServiceId,
        weekStartDate: this.weekStartDate,
        weekCode: this.weekCode,
        totalEffectif: this.totalEffectif,
      }),
      (s) => {
        this.subServiceId = s.subServiceId ?? this.subServiceId;
        this.weekStartDate = s.weekStartDate || this.weekStartDate;
        this.weekCode = s.weekCode || this.weekCode;
        this.totalEffectif = s.totalEffectif ?? this.totalEffectif;
      },
    );
    this.draftBinder.start();
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
    this.userService.syncOrgMirrorFromDirectory().pipe(
      catchError(() => of(null)),
      switchMap(() =>
        forkJoin({
          subServices: this.subServiceService.getAllSubServices(),
          departments: this.http.get<Department[]>('/api/prime/departments'),
          overview: this.orgApi.loadOverview(),
        }),
      ),
    ).subscribe({
      next: ({ subServices, departments, overview }) => {
        this.operationalDepartments = overview.operationalDepartments ?? [];
        this.unassignedPoles = overview.unassignedPoles ?? [];
        this.legacyDepartments = departments?.length ? departments : (overview.departments ?? []);
        this.subServiceOptions = this.buildSubServiceOptions(subServices ?? []);
        this.draftBinder?.start();
        if (this.subServiceOptions.length > 0) {
          if (!this.subServiceId || !this.subServiceOptions.some((s) => s.id === this.subServiceId)) {
            this.subServiceId = this.subServiceOptions[0].id;
          }
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
    const labels = buildSubServiceOrgLabels(
      subServices,
      this.operationalDepartments,
      this.unassignedPoles,
      this.legacyDepartments,
    );
    return labels
      .map((l) => ({
        id: l.subServiceId,
        name: l.name,
        orgLabel: [l.operationalDepartment, l.pole, l.cellule, l.service].filter(Boolean).join(' / ') || l.name,
        employeesCount: subServices.find((s) => s.id === l.subServiceId)?.employeesCount ?? 0,
      }))
      .sort((a, b) => a.orgLabel.localeCompare(b.orgLabel, 'fr'));
  }

  loadPlannings(): void {
    const id = Number(this.subServiceId);
    if (!Number.isFinite(id) || id <= 0) return;
    this.loading = true;
    this.cdr.detectChanges();

    this.planningService.getBySubService(id).subscribe({
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
    const id = Number(this.subServiceId);
    this.subServiceId = Number.isFinite(id) && id > 0 ? id : 0;
    const opt = this.subServiceOptions.find((s) => s.id === this.subServiceId);
    this.serviceEmployeeCount = opt?.employeesCount ?? 0;
    if (
      this.serviceEmployeeCount > 0 &&
      this.totalEffectif > this.serviceEmployeeCount
    ) {
      this.totalEffectif = this.serviceEmployeeCount;
    }
    this.draftBinder?.touch();
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
    this.draftBinder?.touch();
    this.cdr.detectChanges();
  }

  onEffectifChange(): void {
    if (this.serviceEmployeeCount > 0 && this.totalEffectif > this.serviceEmployeeCount) {
      this.totalEffectif = this.serviceEmployeeCount;
    }
    if (this.totalEffectif < 1) {
      this.totalEffectif = 1;
    }
    this.draftBinder?.touch();
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
            this.draftBinder?.clear();
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

  async deletePlanning(id: number, event: Event): Promise<void> {
    event.stopPropagation();
    const ok = await this.confirmService.confirm({
      title: 'Supprimer le planning',
      message: 'Supprimer ce planning ? Cette action est irréversible.',
      confirmLabel: 'Supprimer',
      variant: 'danger',
    });
    if (!ok) return;

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
