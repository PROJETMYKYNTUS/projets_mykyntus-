import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ContractService, CreateContractDto, UpdateContractDto } from '../../services/contract.service';
import { UserService } from '../../../users/services/user.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { ContractFieldsComponent } from '../../../../shared/components/contract-fields/contract-fields.component';
import {
  createEmptyContractFields,
  statusLabelToValue,
  type ContractFieldsModel,
} from '../../../../shared/components/contract-fields/contract-fields.model';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../../shared/directives/kyntus-select-sync.directive';
import type { IconNode } from 'lucide';
import { Save, Plus, Search } from 'lucide';
import type { User } from '../../../users/users-module';
import type { Department } from '../../../prime/models';
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
} from '../../lib/contract-employee-filter';

@Component({
  selector: 'app-contract-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, LucideIconComponent, KyntusSelectSyncDirective, KyntusPageHeaderComponent, ContractFieldsComponent],
  templateUrl: './contract-form.component.html',
  styleUrls: ['./contract-form.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class ContractFormComponent implements OnInit {
  contractForm!: FormGroup;
  isEditMode = false;
  editingId: number | null = null;
  saving = false;
  contractFields: ContractFieldsModel = createEmptyContractFields();
  readonly hideEmployeePicker = signal(false);
  private presetUserId: number | null = null;

  employeeRows: EmployeePickerRow[] = [];
  visibleEmployees: EmployeePickerRow[] = [];
  employeeMatchTotal = 0;
  selectedEmployee: EmployeePickerRow | null = null;
  employeeSearch = '';
  filterPole = '';
  filterCellule = '';
  filterService = '';
  orgDepartments: Department[] = [];
  poleOptions: string[] = [];
  celluleOptions: string[] = [];
  serviceOptions: string[] = [];

  readonly icons = { save: Save, plus: Plus, search: Search };
  readonly orgPerimeterSummary = orgPerimeterSummary;

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private contractService: ContractService,
    private userService: UserService,
    private subServiceService: SubServiceService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.buildForm();

    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEditMode = true;
      this.editingId = +id;
      this.contractForm.get('userId')?.clearValidators();
      this.contractForm.get('userId')?.updateValueAndValidity();
      this.loadContract(+id);
    } else {
      const userIdParam = this.route.snapshot.queryParamMap.get('userId');
      const uid = userIdParam ? Number(userIdParam) : NaN;
      if (uid > 0) {
        this.presetUserId = uid;
        this.hideEmployeePicker.set(true);
      }
    }

    this.loadEmployees();
  }

  buildForm(): void {
    this.contractForm = this.fb.group({
      userId: ['', Validators.required],
    });
    this.contractFields = createEmptyContractFields();
  }

  get f() { return this.contractForm.controls; }

  onContractFieldsChange(model: ContractFieldsModel): void {
    this.contractFields = model;
    this.cdr.detectChanges();
  }

  loadEmployees(): void {
    forkJoin({
      users: this.userService.getAllUsers(),
      departments: this.http.get<Department[]>('/api/prime/departments'),
      overview: this.orgApi.loadOverview(),
      subServices: this.subServiceService.getAllSubServices(),
    }).subscribe({
      next: ({ users, departments, overview, subServices }) => {
        this.orgDepartments = departments ?? [];
        const perimeterById = new Map<number, UserOrgPerimeterView>();
        for (const u of users) {
          perimeterById.set(
            u.id,
            enrichUserOrgPerimeter(u, departments ?? [], overview, subServices ?? []),
          );
        }
        this.employeeRows = buildEmployeePickerRows(users, perimeterById);
        this.refreshEmployeeFilters();
        this.applyPresetEmployee();
        this.cdr.detectChanges();
      },
      error: () => {
        this.userService.getAllUsers().subscribe({
          next: (users) => {
            const perimeterById = new Map<number, UserOrgPerimeterView>();
            for (const u of users) {
              perimeterById.set(u.id, enrichUserOrgPerimeter(u, [], null, []));
            }
            this.employeeRows = buildEmployeePickerRows(users, perimeterById);
            this.orgDepartments = [];
            this.refreshEmployeeFilters();
            this.applyPresetEmployee();
            this.cdr.detectChanges();
          },
        });
      },
    });
  }

  private applyPresetEmployee(): void {
    if (!this.presetUserId || this.isEditMode) return;
    const row = this.employeeRows.find((r) => r.user.id === this.presetUserId);
    if (row) {
      this.selectEmployee(row);
      return;
    }
    this.contractForm.patchValue({ userId: this.presetUserId });
    this.contractForm.get('userId')?.markAsTouched();
    this.userService.getUserById(this.presetUserId).subscribe({
      next: (u) => {
        this.selectedEmployee = {
          user: u,
          displayName: `${u.lastName} ${u.firstName}`.trim(),
          perimeter: enrichUserOrgPerimeter(u, this.orgDepartments, null, []),
          searchText: `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase(),
        };
        this.cdr.detectChanges();
      },
    });
  }

  refreshEmployeeFilters(): void {
    const orgOpts = buildOrgRhFilterOptions(this.orgDepartments, {
      pole: this.filterPole || undefined,
      cellule: this.filterCellule || undefined,
    });
    this.poleOptions = orgOpts.poles;
    this.celluleOptions = orgOpts.cellules;
    this.serviceOptions = orgOpts.services;

    const result = filterEmployeePickerRows(this.employeeRows, {
      search: this.employeeSearch,
      pole: this.filterPole || undefined,
      cellule: this.filterCellule || undefined,
      service: this.filterService || undefined,
    });
    this.visibleEmployees = result.visible;
    this.employeeMatchTotal = result.totalMatches;
    this.cdr.detectChanges();
  }

  onEmployeeSearchChange(value: string): void {
    this.employeeSearch = value;
    this.refreshEmployeeFilters();
  }

  patchFilterPole(pole: string): void {
    this.filterPole = pole;
    this.filterCellule = '';
    this.filterService = '';
    this.refreshEmployeeFilters();
  }

  patchFilterCellule(cellule: string): void {
    this.filterCellule = cellule;
    this.filterService = '';
    this.refreshEmployeeFilters();
  }

  patchFilterService(service: string): void {
    this.filterService = service;
    this.refreshEmployeeFilters();
  }

  clearEmployeeFilters(): void {
    this.employeeSearch = '';
    this.filterPole = '';
    this.filterCellule = '';
    this.filterService = '';
    this.refreshEmployeeFilters();
  }

  selectEmployee(row: EmployeePickerRow): void {
    this.selectedEmployee = row;
    this.contractForm.patchValue({ userId: row.user.id });
    this.contractForm.get('userId')?.markAsTouched();
    this.cdr.detectChanges();
  }

  isEmployeeSelected(row: EmployeePickerRow): boolean {
    return Number(this.f['userId'].value) === row.user.id;
  }

  loadContract(id: number): void {
    this.contractService.getById(id).subscribe({
      next: c => {
        this.contractFields = {
          type: c.type,
          startDate: c.startDate?.substring(0, 10) ?? '',
          endDate: c.endDate?.substring(0, 10) ?? '',
          probationDays: null,
          alertThresholdDays: c.alertThresholdDays,
          status: statusLabelToValue(c.status),
          notes: c.notes ?? '',
        };
        this.cdr.detectChanges();
      },
      error: err => console.error('Erreur chargement contrat:', err)
    });
  }

  private validateContractFields(): string | null {
    if (!this.isEditMode && !this.contractFields.startDate.trim()) {
      return 'Date de début requise.';
    }
    if (this.contractFields.type !== 'CDI' && !this.contractFields.endDate.trim()) {
      return 'Date de fin requise pour ce type de contrat.';
    }
    return null;
  }

  onSubmit(): void {
    if (this.contractForm.invalid) {
      this.contractForm.markAllAsTouched();
      this.cdr.detectChanges();
      return;
    }
    const fieldError = this.validateContractFields();
    if (fieldError) {
      console.error(fieldError);
      this.cdr.detectChanges();
      return;
    }

    this.saving = true;

    if (this.isEditMode && this.editingId) {
      const dto: UpdateContractDto = {
        type: this.contractFields.type,
        status: this.contractFields.status,
        endDate: this.contractFields.type !== 'CDI' ? this.contractFields.endDate : undefined,
        probationDays: this.contractFields.probationDays ?? undefined,
        alertThresholdDays: this.contractFields.alertThresholdDays,
        notes: this.contractFields.notes,
      };

      this.contractService.update(this.editingId, dto).subscribe({
        next: () => { this.saving = false; this.router.navigate(['/contracts']); },
        error: err => { console.error('Erreur mise à jour:', err); this.saving = false; this.cdr.detectChanges(); }
      });
    } else {
      const dto: CreateContractDto = {
        userId: +this.f['userId'].value,
        type: this.contractFields.type,
        startDate: this.contractFields.startDate,
        endDate: this.contractFields.endDate || undefined,
        probationDays: this.contractFields.probationDays ?? undefined,
        alertThresholdDays: this.contractFields.alertThresholdDays,
        notes: this.contractFields.notes,
        status: this.contractFields.status,
      };

      this.contractService.create(dto).subscribe({
        next: () => { this.saving = false; this.router.navigate(['/contracts']); },
        error: err => { console.error('Erreur création:', err); this.saving = false; this.cdr.detectChanges(); }
      });
    }
  }

  goBack(): void { this.router.navigate(['/contracts']); }
}
