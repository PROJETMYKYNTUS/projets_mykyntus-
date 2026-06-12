import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { CongeService, CongeItem, ABSENCE_TYPES } from '../../services/conge.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { UserService } from '../../../users/services/user.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../../shared/directives/kyntus-select-sync.directive';
import { Inbox, Plus, Search } from 'lucide';
import type { Department } from '../../../prime/models';
import type { SubService } from '../../../sub-services/sub-services-module';
import type { User } from '../../../users/users-module';
import { buildOrgRhFilterOptions } from '../../../../core/org/org-structure-filter';
import {
  enrichUserOrgPerimeter,
  orgPerimeterSummary,
  type UserOrgPerimeterView,
} from '../../../../core/org/user-org-perimeter';
import {
  buildEmployeePickerRows,
  filterEmployeePickerRows,
  type EmployeePickerRow,
} from '../../../contract/lib/contract-employee-filter';
import {
  findOrgSelectionByPrimeServiceId,
  poleCells,
} from '../../../../core/org/planning-org-picker';

type SubServiceOrgView = {
  subServiceId: number;
  name: string;
  pole: string | null;
  cellule: string | null;
  service: string | null;
};

@Component({
  selector: 'app-conge-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusSelectSyncDirective],
  templateUrl: './conge-manager.component.html',
  styleUrls: ['./conge-manager.component.css'],
})
export class CongeManagerComponent implements OnInit {
  readonly icons = { inbox: Inbox, plus: Plus, search: Search };

  orgDepartments: Department[] = [];
  subServiceOrgViews: SubServiceOrgView[] = [];
  allUsers: User[] = [];
  employeeRows: EmployeePickerRow[] = [];
  visibleFormEmployees: EmployeePickerRow[] = [];
  formEmployeeMatchTotal = 0;
  selectedFormEmployee: EmployeePickerRow | null = null;
  private perimeterByUserId = new Map<number, UserOrgPerimeterView>();

  allConges: CongeItem[] = [];
  filteredConges: CongeItem[] = [];

  poleOptions: string[] = [];
  celluleOptions: string[] = [];
  serviceOptions: string[] = [];
  formPoleOptions: string[] = [];
  formCelluleOptions: string[] = [];
  formServiceOptions: string[] = [];

  filterPole = '';
  filterCellule = '';
  filterService = '';
  filterDateDebut = '';
  filterDateFin = '';
  searchTerm = '';

  formEmployeeSearch = '';
  formFilterPole = '';
  formFilterCellule = '';
  formFilterService = '';

  loading = false;
  error = '';
  successMsg = '';

  showForm = false;
  formUserId = 0;
  formStartDate = '';
  formEndDate = '';
  formReason = '';
  saving = false;
  absenceTypes = ABSENCE_TYPES;
  formAbsenceType = 'CongesPayes';

  readonly orgPerimeterSummary = orgPerimeterSummary;

  constructor(
    private congeService: CongeService,
    private subServiceService: SubServiceService,
    private userService: UserService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth(), 1);
    const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    this.filterDateDebut = this.toDateInput(start);
    this.filterDateFin = this.toDateInput(end);
    this.loadOrgContext();
  }

  private toDateInput(d: Date): string {
    return d.toLocaleDateString('en-CA');
  }

  loadOrgContext(): void {
    this.loading = true;
    forkJoin({
      subServices: this.subServiceService.getAllSubServices(),
      departments: this.http.get<Department[]>('/api/prime/departments'),
      users: this.userService.getAllUsers(),
      overview: this.orgApi.loadOverview(),
    }).subscribe({
      next: ({ subServices, departments, users, overview }) => {
        this.orgDepartments = departments ?? [];
        this.allUsers = (users ?? []).filter((u) => u.isActive);
        this.subServiceOrgViews = this.buildSubServiceOrgViews(subServices ?? []);
        this.buildUserPerimeters(this.allUsers, overview, subServices ?? []);
        this.employeeRows = buildEmployeePickerRows(this.allUsers, this.perimeterByUserId);
        this.refreshOrgFilterOptions();
        this.loadConges();
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.error = 'Impossible de charger la structure organisationnelle.';
        this.cdr.detectChanges();
      },
    });
  }

  private buildSubServiceOrgViews(subServices: SubService[]): SubServiceOrgView[] {
    return subServices.map((sub) => {
      const primeId = sub.primeServiceId?.trim() ?? '';
      const sel = primeId ? findOrgSelectionByPrimeServiceId(this.orgDepartments, primeId) : null;
      if (!sel) {
        return {
          subServiceId: sub.id,
          name: sub.name,
          pole: null,
          cellule: null,
          service: sub.name,
        };
      }
      const dept = this.orgDepartments.find((d) => d.id === sel.poleId);
      const cellule = dept?.poles?.find((p) => p.id === sel.celluleId);
      const service = cellule ? poleCells(cellule).find((c) => c.id === sel.serviceId) : undefined;
      return {
        subServiceId: sub.id,
        name: sub.name,
        pole: dept?.name ?? null,
        cellule: cellule?.name ?? null,
        service: service?.name ?? sub.name,
      };
    });
  }

  private buildUserPerimeters(
    users: User[],
    overview: Parameters<typeof enrichUserOrgPerimeter>[2],
    subServices: SubService[],
  ): void {
    this.perimeterByUserId.clear();
    for (const u of users) {
      this.perimeterByUserId.set(
        u.id,
        enrichUserOrgPerimeter(u, this.orgDepartments, overview, subServices),
      );
    }
  }

  userPerimeter(userId: number): UserOrgPerimeterView {
    return this.perimeterByUserId.get(userId) ?? { pole: null, cellule: null, service: null };
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

  refreshFormEmployeeFilters(): void {
    const orgOpts = buildOrgRhFilterOptions(this.orgDepartments, {
      pole: this.formFilterPole || undefined,
      cellule: this.formFilterCellule || undefined,
    });
    this.formPoleOptions = orgOpts.poles;
    this.formCelluleOptions = orgOpts.cellules;
    this.formServiceOptions = orgOpts.services;

    const result = filterEmployeePickerRows(this.employeeRows, {
      search: this.formEmployeeSearch,
      pole: this.formFilterPole || undefined,
      cellule: this.formFilterCellule || undefined,
      service: this.formFilterService || undefined,
    });
    this.visibleFormEmployees = result.visible;
    this.formEmployeeMatchTotal = result.totalMatches;
    this.cdr.detectChanges();
  }

  matchingSubServiceIds(): number[] {
    let views = [...this.subServiceOrgViews];
    if (this.filterPole) views = views.filter((v) => v.pole === this.filterPole);
    if (this.filterCellule) views = views.filter((v) => v.cellule === this.filterCellule);
    if (this.filterService) views = views.filter((v) => v.service === this.filterService);
    return views.map((v) => v.subServiceId);
  }

  patchFilterPole(pole: string): void {
    this.filterPole = pole;
    this.filterCellule = '';
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.loadConges();
  }

  patchFilterCellule(cellule: string): void {
    this.filterCellule = cellule;
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.loadConges();
  }

  patchFilterService(service: string): void {
    this.filterService = service;
    this.loadConges();
  }

  clearFilters(): void {
    this.filterPole = '';
    this.filterCellule = '';
    this.filterService = '';
    this.searchTerm = '';
    const now = new Date();
    this.filterDateDebut = this.toDateInput(new Date(now.getFullYear(), now.getMonth(), 1));
    this.filterDateFin = this.toDateInput(new Date(now.getFullYear(), now.getMonth() + 1, 0));
    this.refreshOrgFilterOptions();
    this.loadConges();
  }

  loadConges(): void {
    const subIds = this.matchingSubServiceIds();
    if (subIds.length === 0) {
      this.allConges = [];
      this.applyFilters();
      this.loading = false;
      return;
    }

    this.loading = true;
    forkJoin(
      subIds.map((id) =>
        this.congeService.getBySubService(id).pipe(catchError(() => of([] as CongeItem[]))),
      ),
    )
      .pipe(map((groups) => groups.flat()))
      .subscribe({
        next: (data) => {
          this.allConges = data;
          this.applyFilters();
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.loading = false;
          this.error = 'Erreur lors du chargement des absences.';
          this.cdr.detectChanges();
        },
      });
  }

  openForm(): void {
    this.showForm = true;
    this.formUserId = 0;
    this.selectedFormEmployee = null;
    this.formEmployeeSearch = '';
    this.formFilterPole = this.filterPole;
    this.formFilterCellule = this.filterCellule;
    this.formFilterService = this.filterService;
    this.formStartDate = '';
    this.formEndDate = '';
    this.formReason = '';
    this.formAbsenceType = 'CongesPayes';
    this.error = '';
    this.refreshFormEmployeeFilters();
  }

  patchFormFilterPole(pole: string): void {
    this.formFilterPole = pole;
    this.formFilterCellule = '';
    this.formFilterService = '';
    this.refreshFormEmployeeFilters();
  }

  patchFormFilterCellule(cellule: string): void {
    this.formFilterCellule = cellule;
    this.formFilterService = '';
    this.refreshFormEmployeeFilters();
  }

  patchFormFilterService(service: string): void {
    this.formFilterService = service;
    this.refreshFormEmployeeFilters();
  }

  clearFormEmployeeFilters(): void {
    this.formEmployeeSearch = '';
    this.formFilterPole = '';
    this.formFilterCellule = '';
    this.formFilterService = '';
    this.selectedFormEmployee = null;
    this.formUserId = 0;
    this.refreshFormEmployeeFilters();
  }

  selectFormEmployee(row: EmployeePickerRow): void {
    this.selectedFormEmployee = row;
    this.formUserId = row.user.id;
    this.cdr.detectChanges();
  }

  isFormEmployeeSelected(row: EmployeePickerRow): boolean {
    return this.formUserId === row.user.id;
  }

  onFormEmployeeSearchChange(value: string): void {
    this.formEmployeeSearch = value;
    this.refreshFormEmployeeFilters();
  }

  applyFilters(): void {
    let rows = [...this.allConges];
    if (this.filterDateDebut) {
      rows = rows.filter((c) => (c.endDate ?? '').substring(0, 10) >= this.filterDateDebut);
    }
    if (this.filterDateFin) {
      rows = rows.filter((c) => (c.startDate ?? '').substring(0, 10) <= this.filterDateFin);
    }
    const term = this.searchTerm.trim().toLowerCase();
    if (term) {
      rows = rows.filter((c) => {
        const p = this.userPerimeter(c.userId);
        const hay = [c.fullName, c.reason, p.pole, p.cellule, p.service]
          .filter(Boolean)
          .join(' ')
          .toLowerCase();
        return hay.includes(term);
      });
    }
    this.filteredConges = rows.sort(
      (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime(),
    );
    this.cdr.detectChanges();
  }

  closeForm(): void {
    this.showForm = false;
  }

  saveConge(): void {
    const userId = Number(this.formUserId);
    if (!userId) {
      this.error = 'Veuillez sélectionner un employé dans la liste.';
      return;
    }
    if (!this.formStartDate || !this.formEndDate || !this.formAbsenceType) {
      this.error = 'Veuillez remplir tous les champs obligatoires.';
      return;
    }
    if (this.formStartDate > this.formEndDate) {
      this.error = 'La date de fin doit être après la date de début.';
      return;
    }
    this.saving = true;
    this.error = '';
    this.congeService
      .create({
        userId,
        startDate: this.formStartDate,
        endDate: this.formEndDate,
        reason: this.formReason,
        absenceType: this.formAbsenceType,
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.showForm = false;
          this.successMsg = 'Absence enregistrée avec succès !';
          this.loadConges();
          setTimeout(() => {
            this.successMsg = '';
            this.cdr.detectChanges();
          }, 3000);
          this.cdr.detectChanges();
        },
        error: (err: { error?: { message?: string } | string; status?: number; message?: string }) => {
          this.saving = false;
          const body = err.error;
          if (typeof body === 'string' && body.trim()) {
            this.error = body;
          } else if (body && typeof body === 'object' && body.message) {
            this.error = body.message;
          } else {
            this.error = err.message ?? `Erreur lors de la création (${err.status ?? ''}).`;
          }
          this.cdr.detectChanges();
        },
      });
  }

  getAbsenceLabel(value: string): string {
    return this.absenceTypes.find((t) => t.value === value)?.label ?? value;
  }

  deleteConge(id: number): void {
    if (!confirm('Supprimer cette absence ?')) return;
    this.congeService.delete(id).subscribe({
      next: () => {
        this.allConges = this.allConges.filter((c) => c.id !== id);
        this.applyFilters();
        this.cdr.detectChanges();
      },
    });
  }

  getDaysCount(start: string, end: string): number {
    return Math.round((new Date(end).getTime() - new Date(start).getTime()) / 86400000) + 1;
  }

  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('fr-FR', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
  }
}
