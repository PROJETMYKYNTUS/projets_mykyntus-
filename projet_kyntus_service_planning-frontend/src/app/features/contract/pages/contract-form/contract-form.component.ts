import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
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
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../../shared/directives/kyntus-select-sync.directive';
import type { IconNode } from 'lucide';
import { ClipboardList, Calendar, GraduationCap, RefreshCw, FileText, Save, Plus, Search } from 'lucide';
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
  imports: [CommonModule, ReactiveFormsModule, FormsModule, LucideIconComponent, KyntusSelectSyncDirective],
  templateUrl: './contract-form.component.html',
  styleUrls: ['./contract-form.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class ContractFormComponent implements OnInit {
  contractForm!: FormGroup;
  isEditMode = false;
  editingId: number | null = null;
  saving = false;

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

  readonly icons = { notes: FileText, save: Save, plus: Plus, search: Search };
  readonly orgPerimeterSummary = orgPerimeterSummary;

  contractTypes: { value: string; label: string; icon: IconNode; desc: string; cssClass: string }[] = [
    { value: 'CDI', label: 'CDI', icon: ClipboardList, desc: 'Durée indéterminée', cssClass: 'type-card--cdi' },
    { value: 'CDD', label: 'CDD', icon: Calendar, desc: 'Durée déterminée', cssClass: 'type-card--cdd' },
    { value: 'Stage', label: 'Stage', icon: GraduationCap, desc: 'Stage de formation', cssClass: 'type-card--stage' },
    { value: 'ANAPEC', label: 'ANAPEC', icon: RefreshCw, desc: 'Mission temporaire', cssClass: 'type-card--anapec' },
  ];

  contractStatuses = [
    { label: "En période d'essai", value: 0 },
    { label: 'Actif', value: 1 },
    { label: 'Expiré', value: 2 },
    { label: 'Résilié', value: 3 },
  ];

  defaultProbation: Record<string, number> = {
    CDI: 90, CDD: 30, Stage: 15, ANAPEC: 0, Interim: 0
  };

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
    this.loadEmployees();

    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEditMode = true;
      this.editingId = +id;
      this.contractForm.get('userId')?.clearValidators();
      this.contractForm.get('userId')?.updateValueAndValidity();
      this.contractForm.get('startDate')?.clearValidators();
      this.contractForm.get('startDate')?.updateValueAndValidity();
      this.contractForm.get('endDate')?.clearValidators();
      this.contractForm.get('endDate')?.updateValueAndValidity();
      this.loadContract(+id);
    }

    this.contractForm.get('type')?.valueChanges.subscribe(type => {
      if (!this.isEditMode) {
        const endCtrl = this.contractForm.get('endDate');
        if (type !== 'CDI') {
          endCtrl?.setValidators(Validators.required);
        } else {
          endCtrl?.clearValidators();
          endCtrl?.setValue('');
        }
        endCtrl?.updateValueAndValidity();
      }
      this.cdr.detectChanges();
    });
  }

  buildForm(): void {
    this.contractForm = this.fb.group({
      userId: ['', Validators.required],
      type: ['CDI', Validators.required],
      startDate: ['', Validators.required],
      endDate: [''],
      probationDays: [null],
      alertThresholdDays: [15],
      status: [0],
      notes: ['']
    });
  }

  get f() { return this.contractForm.controls; }

  selectType(value: string): void {
    this.contractForm.patchValue({ type: value });
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
            this.cdr.detectChanges();
          },
        });
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
        const statusValue = this.contractStatuses.find(s => s.label === c.status)?.value ?? 0;
        this.contractForm.patchValue({
          type: c.type,
          startDate: c.startDate?.substring(0, 10) ?? '',
          endDate: c.endDate?.substring(0, 10) ?? '',
          alertThresholdDays: c.alertThresholdDays,
          status: statusValue,
          notes: c.notes ?? ''
        });
        this.cdr.detectChanges();
      },
      error: err => console.error('Erreur chargement contrat:', err)
    });
  }

  onSubmit(): void {
    if (this.contractForm.invalid) {
      this.contractForm.markAllAsTouched();
      this.cdr.detectChanges();
      return;
    }

    this.saving = true;

    if (this.isEditMode && this.editingId) {
      const dto: UpdateContractDto = {
        status: this.f['status'].value !== null ? this.f['status'].value : undefined,
      };
      const type = this.f['type'].value;
      if (type) dto.type = type;
      const endDate = this.f['endDate'].value;
      if (endDate && endDate !== '') dto.endDate = endDate;
      const probationDays = this.f['probationDays'].value;
      if (probationDays && probationDays > 0) dto.probationDays = probationDays;
      const alertDays = this.f['alertThresholdDays'].value;
      if (alertDays && alertDays > 0) dto.alertThresholdDays = alertDays;
      const notes = this.f['notes'].value;
      if (notes !== null && notes !== undefined) dto.notes = notes;

      this.contractService.update(this.editingId, dto).subscribe({
        next: () => { this.saving = false; this.router.navigate(['/contracts']); },
        error: err => { console.error('Erreur mise à jour:', err); this.saving = false; this.cdr.detectChanges(); }
      });
    } else {
      const dto: CreateContractDto = {
        userId: +this.f['userId'].value,
        type: this.f['type'].value,
        startDate: this.f['startDate'].value,
        endDate: this.f['endDate'].value || undefined,
        probationDays: this.f['probationDays'].value || undefined,
        alertThresholdDays: this.f['alertThresholdDays'].value,
        notes: this.f['notes'].value
      };

      this.contractService.create(dto).subscribe({
        next: () => { this.saving = false; this.router.navigate(['/contracts']); },
        error: err => { console.error('Erreur création:', err); this.saving = false; this.cdr.detectChanges(); }
      });
    }
  }

  getDefaultProbationValue(): number {
    const type = this.contractForm?.get('type')?.value ?? 'CDI';
    return this.defaultProbation[type] ?? 0;
  }

  getDefaultProbationLabel(): string {
    return `Par défaut : ${this.getDefaultProbationValue()} jours`;
  }

  goBack(): void { this.router.navigate(['/contracts']); }
}
