import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { Calendar, CheckCircle, Circle, Inbox, Loader2, Save, Search, Users } from 'lucide';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../../shared/directives/kyntus-select-sync.directive';
import {
  PlanningService,
  SaturdayHistoryResponse,
  SetSaturdayHistoryDto,
} from '../../services/planning.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { UserService } from '../../../users/services/user.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import type { Department } from '../../../prime/models';
import type { SubService } from '../../../sub-services/sub-services-module';
import { buildOrgRhFilterOptions } from '../../../../core/org/org-structure-filter';
import {
  enrichUserOrgPerimeter,
  orgPerimeterSummary,
  type UserOrgPerimeterView,
} from '../../../../core/org/user-org-perimeter';
import {
  findOrgSelectionByPrimeServiceId,
  poleCells,
} from '../../../../core/org/planning-org-picker';

type SubServiceOption = {
  id: number;
  orgLabel: string;
};

type SaturdayEntryView = SaturdayHistoryResponse & {
  perimeter: UserOrgPerimeterView;
  searchText: string;
};

@Component({
  selector: 'app-saturday-history',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusSelectSyncDirective],
  templateUrl: './saturday-history.component.html',
  styleUrls: ['./saturday-history.component.css'],
})
export class SaturdayHistoryComponent implements OnInit {
  readonly icons = {
    calendar: Calendar,
    success: CheckCircle,
    worked: CheckCircle,
    off: Circle,
    save: Save,
    search: Search,
    users: Users,
    inbox: Inbox,
    loader: Loader2,
  };

  orgDepartments: Department[] = [];
  subServices: SubService[] = [];
  subServiceOptions: SubServiceOption[] = [];
  subServiceId = 0;
  weekCode = '';
  weekStartDate = '';
  weekDateAdjusted = false;

  allEntries: SaturdayEntryView[] = [];
  filteredEntries: SaturdayEntryView[] = [];

  searchTerm = '';
  filterPole = '';
  filterCellule = '';
  filterService = '';
  filterStatus: '' | 'worked' | 'off' = '';

  poleOptions: string[] = [];
  celluleOptions: string[] = [];
  serviceOptions: string[] = [];

  loading = false;
  saving = false;
  successMsg = '';
  error = '';

  readonly orgPerimeterSummary = orgPerimeterSummary;

  constructor(
    private planningService: PlanningService,
    private subServiceService: SubServiceService,
    private userService: UserService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.initPreviousWeek();
    this.loadContext();
  }

  private initPreviousWeek(): void {
    const now = new Date();
    const monday = this.getMondayOfWeek(now);
    monday.setDate(monday.getDate() - 7);
    this.weekStartDate = this.formatDate(monday);
    this.weekCode = this.getWeekCode(monday);
  }

  loadContext(): void {
    this.loading = true;
    forkJoin({
      subServices: this.subServiceService.getAllSubServices(),
      departments: this.http.get<Department[]>('/api/prime/departments'),
      overview: this.orgApi.loadOverview(),
    }).subscribe({
      next: ({ subServices, departments, overview }) => {
        this.orgDepartments = departments ?? [];
        this.subServices = subServices ?? [];
        this.subServiceOptions = this.buildSubServiceOptions(this.subServices);
        if (this.subServiceOptions.length > 0) {
          this.subServiceId = this.subServiceOptions[0].id;
          this.load();
        } else {
          this.loading = false;
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.error = 'Impossible de charger la structure organisationnelle.';
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

        return { id: sub.id, orgLabel };
      })
      .sort((a, b) => a.orgLabel.localeCompare(b.orgLabel, 'fr'));
  }

  load(): void {
    if (!this.subServiceId || !this.weekCode) return;
    this.loading = true;
    this.error = '';
    this.successMsg = '';

    forkJoin({
      history: this.planningService.getSaturdayHistory(this.subServiceId, this.weekCode),
      users: this.userService.getAllUsers(),
      overview: this.orgApi.loadOverview(),
    }).subscribe({
      next: ({ history, users, overview }) => {
        const activeUsers = (users ?? []).filter((u) => u.isActive);
        const perimeterById = new Map<number, UserOrgPerimeterView>();
        for (const u of activeUsers) {
          perimeterById.set(
            u.id,
            enrichUserOrgPerimeter(u, this.orgDepartments, overview, this.subServices),
          );
        }

        this.allEntries = (history ?? []).map((entry) => {
          const perimeter = perimeterById.get(entry.userId) ?? { pole: null, cellule: null, service: null };
          const searchText = [
            entry.fullName,
            perimeter.pole,
            perimeter.cellule,
            perimeter.service,
          ]
            .filter(Boolean)
            .join(' ')
            .toLowerCase();
          return { ...entry, perimeter, searchText };
        });

        this.refreshOrgFilterOptions();
        this.applyFilters();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.error = 'Impossible de charger l\'historique.';
        this.cdr.detectChanges();
      },
    });
  }

  refreshOrgFilterOptions(): void {
    const opts = buildOrgRhFilterOptions(this.orgDepartments, {
      pole: this.filterPole || undefined,
      cellule: this.filterCellule || undefined,
    });
    this.poleOptions = opts.poles;
    this.celluleOptions = opts.cellules;
    this.serviceOptions = opts.services;
  }

  patchFilterPole(value: string): void {
    this.filterPole = value;
    this.filterCellule = '';
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyFilters();
  }

  patchFilterCellule(value: string): void {
    this.filterCellule = value;
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyFilters();
  }

  patchFilterService(value: string): void {
    this.filterService = value;
    this.applyFilters();
  }

  applyFilters(): void {
    let rows = [...this.allEntries];
    const q = this.searchTerm.trim().toLowerCase();
    if (q) rows = rows.filter((e) => e.searchText.includes(q));
    if (this.filterPole) rows = rows.filter((e) => e.perimeter.pole === this.filterPole);
    if (this.filterCellule) rows = rows.filter((e) => e.perimeter.cellule === this.filterCellule);
    if (this.filterService) rows = rows.filter((e) => e.perimeter.service === this.filterService);
    if (this.filterStatus === 'worked') rows = rows.filter((e) => e.workedSaturday);
    if (this.filterStatus === 'off') rows = rows.filter((e) => !e.workedSaturday);
    this.filteredEntries = rows;
    this.cdr.detectChanges();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.filterPole = '';
    this.filterCellule = '';
    this.filterService = '';
    this.filterStatus = '';
    this.refreshOrgFilterOptions();
    this.applyFilters();
  }

  onSubServiceChange(): void {
    this.load();
  }

  onWeekChange(): void {
    if (!this.weekStartDate) return;
    const picked = this.parseDateInput(this.weekStartDate);
    const monday = this.getMondayOfWeek(picked);
    const mondayStr = this.formatDate(monday);
    this.weekDateAdjusted = mondayStr !== this.weekStartDate;
    this.weekStartDate = mondayStr;
    this.weekCode = this.getWeekCode(monday);
    this.load();
  }

  setWorked(entry: SaturdayEntryView, worked: boolean): void {
    entry.workedSaturday = worked;
    const source = this.allEntries.find((e) => e.userId === entry.userId);
    if (source) source.workedSaturday = worked;
    this.applyFilters();
  }

  save(): void {
    if (!this.subServiceId || !this.weekCode || this.allEntries.length === 0) return;

    this.saving = true;
    this.error = '';
    this.successMsg = '';

    const dto: SetSaturdayHistoryDto = {
      subServiceId: this.subServiceId,
      weekCode: this.weekCode,
      entries: this.allEntries.map((e) => ({
        userId: e.userId,
        workedSaturday: e.workedSaturday,
      })),
    };

    this.planningService.saveSaturdayHistory(dto).subscribe({
      next: () => {
        this.saving = false;
        this.successMsg = 'Historique sauvegardé !';
        setTimeout(() => {
          this.successMsg = '';
          this.cdr.detectChanges();
        }, 3000);
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.saving = false;
        this.error = err.error?.message ?? 'Erreur lors de la sauvegarde.';
        this.cdr.detectChanges();
      },
    });
  }

  get workedCount(): number {
    return this.allEntries.filter((e) => e.workedSaturday).length;
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
