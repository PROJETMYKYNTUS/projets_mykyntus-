import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, defer, of, Observable, throwError } from 'rxjs';
import { retry, switchMap, map, catchError } from 'rxjs/operators';
import { formatHttpErrorMessage } from '../../../../core/lib/http-error-message.util';
import { resolveUserGuid } from '../../../../core/lib/user-guid.util';
import { UserService } from '../../services/user.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { ServiceService } from '../../../services/services/service';
import { CreateUserDto, UpdateUserDto } from '../../users-module';
import { EmployeeFieldService } from '../../services/employee-field.service';
import type { EmployeeImportFieldConfig } from '../../services/employee-import.service';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../../shared/directives/kyntus-select-sync.directive';
import { LockKeyhole, Search, Sparkles } from 'lucide';
import { NavigationActionsService } from '../../../../core/navigation/navigation-actions.service';
import {
  PrimeOrgApiService,
  type OrgAssignmentsOverview,
  type StructuralRoleAssignmentResult,
} from '../../../prime/services/prime-org-api.service';
import type {
  OperationalDepartmentNode,
  OrgCelluleNode,
  OrgPoleNode,
  OrgServiceNode,
} from '../../../prime/models/org-tree.types';
import type { SubService } from '../../../sub-services/sub-services-module';
import type { Service } from '../../../services/services-module';
import {
  cellulesForPole,
  filterOperationalCellulesBySearch,
  filterOperationalDepartmentsBySearch,
  filterOperationalPolesBySearch,
  filterOperationalServicesBySearch,
  findOperationalSelectionByCelluleId,
  findOperationalSelectionByPoleId,
  findOperationalSelectionByServiceId,
  flattenOperationalServiceOptions,
  isUnassignedPole,
  operationalSelectionSummary,
  polesForOperationalDept,
  servicesForCellule,
  type OperationalFlatServiceOption,
} from '../../../../core/org/operational-org-picker';
import { normalizeOrgSearch } from '../../../../core/org/org-structure-filter';
import {
  resolvePlanningServiceIdByPrimeCelluleId,
  resolveSubServiceIdByPrimeServiceId,
} from '../../../../core/org/planning-org-picker';
import {
  orgAssignmentHint,
  orgAssignmentIsRequired,
  orgAssignmentRequiresCellule,
  orgAssignmentRequiresOperationalDept,
  orgAssignmentRequiresPole,
  orgAssignmentRequiresService,
  orgRoleAssignmentDepth,
  needsPrimeStructureAssignment,
  isReferentTechniqueRole,
  isSuperviseurRole,
  isSupportManagerRole,
  isChefDeProjetRole,
  isPiloteRole,
  type OrgRoleAssignmentDepth,
} from '../../../../core/org/org-role-assignment';
import {
  buildCrossRoleOverwriteMessage,
  buildStructureOverwriteMessage,
  employeeDisplayName,
  findEmployeeStructuralRole,
  findStructureIncumbent,
  shouldConfirmOverwrite,
} from '../../../../core/org/org-structure-incumbent.util';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';

interface RoleOption { id: number; name: string; }
interface SupportDepartmentOption { id: string; code: string; name: string; kind: string; isActive?: boolean; }
type OrgAssignmentMode = 'operational' | 'support';

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusSelectSyncDirective, KyntusPageHeaderComponent],
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css']
})
export class UserFormComponent implements OnInit {
  readonly icons = { lock: LockKeyhole, search: Search, detect: Sparkles };
  isEditMode = false;
  userId: number | null = null;
  subServices: SubService[] = [];
  planningServices: Service[] = [];
  operationalDepartments: OperationalDepartmentNode[] = [];
  unassignedPoles: OrgPoleNode[] = [];
  orgOverview: OrgAssignmentsOverview | null = null;
  orgLoading = false;
  orgOperationalDeptId = '';
  orgPoleId = '';
  orgCelluleId = '';
  orgServiceId = '';
  orgMirrorWarning: string | null = null;
  orgStructureSearch = '';
  orgFilterPoleId = '';
  orgFilterCelluleId = '';
  orgMode: OrgAssignmentMode = 'operational';
  supportDepartmentId = '';
  supportDepartments: SupportDepartmentOption[] = [];
  operationalBusinessDepartmentId = '';
  operationalBusinessDepartments: SupportDepartmentOption[] = [];
  supportDeptLoading = false;
  loading = false;
  submitting = false;
  error: string | null = null;
  emailError: string | null = null;
  roles: RoleOption[] = [];
  private loadedManagedServiceIds: number[] = [];
  private loadedManagedSubServiceIds: number[] = [];
  private loadedUserGuid = '';
  customEmployeeFields: EmployeeImportFieldConfig[] = [];
  customFieldValues: Record<string, string> = {};
  form = {
    roleId: 0,
    subServiceId: null as number | null,
    firstName: '',
    lastName: '',
    email: '',
    hireDate: this.toDateInputValue(new Date()),
    isActive: true,
    level: 1,
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private userService: UserService,
    private subServiceService: SubServiceService,
    private serviceService: ServiceService,
    private navActions: NavigationActionsService,
    private orgApi: PrimeOrgApiService,
    private confirmService: KyntusConfirmService,
    private toastService: KyntusToastService,
    private fieldService: EmployeeFieldService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.fieldService.getFields(true).subscribe({
      next: (fields) => {
        this.customEmployeeFields = fields.filter((f) => f.isSystemField === false);
        for (const field of this.customEmployeeFields) {
          this.customFieldValues[field.fieldKey] ??= '';
        }
        this.cdr.detectChanges();
      },
    });
    this.loadOrgAndSubServices();
    this.loadBusinessDepartments();
    this.loadRoles();
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.userId = Number(id);
      this.loadUser(this.userId);
    }
  }

  loadRoles(): void {
    this.userService.getRoles().subscribe({
      next: (roles) => {
        this.roles = (roles ?? []).map((r) => ({
          id: Number((r as RoleOption & { Id?: number }).id ?? (r as { Id?: number }).Id),
          name: String((r as RoleOption & { Name?: string }).name ?? (r as { Name?: string }).Name ?? ''),
        }));
        this.cdr.detectChanges();
      },
      error: () => {
        this.roles = [
          { id: 1, name: 'Pilote' },
          { id: 2, name: 'RH' },
          { id: 3, name: 'Superviseur' },
          { id: 4, name: 'Référent technique' },
          { id: 5, name: 'Chef de projet' },
          { id: 6, name: 'Admin' },
          { id: 7, name: 'Audit' },
          { id: 8, name: 'EquipeFormation' },
          { id: 9, name: 'Manager' },
        ];
        this.cdr.detectChanges();
      }
    });
  }

  loadBusinessDepartments(): void {
    this.supportDeptLoading = true;
    this.http.get<SupportDepartmentOption[]>('/api/directory/business-departments').subscribe({
      next: (depts) => {
        const active = (depts ?? []).filter((d) => d.isActive !== false);
        this.supportDepartments = active.filter(
          (d) => String(d.kind).toLowerCase() === 'support',
        );
        this.operationalBusinessDepartments = active.filter(
          (d) => String(d.kind).toLowerCase() === 'operational',
        );
        this.supportDeptLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.supportDepartments = [];
        this.operationalBusinessDepartments = [];
        this.supportDeptLoading = false;
        this.cdr.detectChanges();
      },
    });
  }

  setOrgMode(mode: OrgAssignmentMode): void {
    if (this.orgMode === mode) return;
    this.orgMode = mode;
    if (!this.isEditMode) {
      this.form.roleId = 0;
    }
    this.supportDepartmentId = '';
    this.operationalBusinessDepartmentId = '';
    this.clearOrgAssignment();
    this.cdr.detectChanges();
  }

  patchSupportDepartment(deptId: string): void {
    this.supportDepartmentId = deptId;
    this.cdr.detectChanges();
  }

  patchOperationalBusinessDepartment(deptId: string): void {
    this.operationalBusinessDepartmentId = deptId;
    this.cdr.detectChanges();
  }

  private resolvedRoleId(): number {
    return Number(this.form.roleId) || 0;
  }

  get selectedRoleName(): string {
    const id = this.resolvedRoleId();
    return this.roles.find((r) => Number(r.id) === id)?.name ?? '';
  }

  get isSupportMode(): boolean {
    return this.orgMode === 'support';
  }

  get filteredRoles(): RoleOption[] {
    const operationalStructureRoles = (name: string) =>
      isChefDeProjetRole(name) || isSuperviseurRole(name) || isReferentTechniqueRole(name);
    if (this.isSupportMode) {
      return this.roles.filter((r) => !operationalStructureRoles(r.name));
    }
    return this.roles;
  }

  get orgAssignmentDepth(): OrgRoleAssignmentDepth {
    return orgRoleAssignmentDepth(this.selectedRoleName);
  }

  get showOrgAssignmentBlock(): boolean {
    return !this.isSupportMode && this.orgAssignmentDepth !== 'none';
  }

  get showSupportAssignmentBlock(): boolean {
    return this.isSupportMode;
  }

  get showOperationalManagerBlock(): boolean {
    return !this.isSupportMode && isSupportManagerRole(this.selectedRoleName);
  }

  get showOrgFlatServiceSelect(): boolean {
    return false;
  }

  get showOrgCascade(): boolean {
    return this.showOrgAssignmentBlock && !this.showOrgFlatServiceSelect;
  }

  get orgFlatServiceOptions(): OperationalFlatServiceOption[] {
    return flattenOperationalServiceOptions(this.operationalDepartments, this.unassignedPoles);
  }

  get orgPoleFilterOptions(): { id: string; name: string }[] {
    const poles: { id: string; name: string }[] = [];
    for (const md of this.operationalDepartments) {
      for (const p of md.poles) poles.push({ id: p.id, name: p.name });
    }
    for (const p of this.unassignedPoles) poles.push({ id: p.id, name: p.name });
    return poles;
  }

  get orgCelluleFilterOptions(): { id: string; name: string }[] {
    if (!this.orgFilterPoleId) return [];
    return cellulesForPole(this.operationalDepartments, this.unassignedPoles, this.orgFilterPoleId).map(
      (c) => ({ id: c.id, name: c.name }),
    );
  }

  get filteredFlatServices(): { visible: OperationalFlatServiceOption[]; totalMatches: number } {
    let matched = [...this.orgFlatServiceOptions];
    const q = normalizeOrgSearch(this.orgStructureSearch);
    if (q) matched = matched.filter((o) => o.label.toLowerCase().includes(q));
    if (this.orgFilterPoleId) matched = matched.filter((o) => o.poleId === this.orgFilterPoleId);
    if (this.orgFilterCelluleId) matched = matched.filter((o) => o.celluleId === this.orgFilterCelluleId);
    return { visible: matched.slice(0, 40), totalMatches: matched.length };
  }

  get filteredOperationalDepartments(): OperationalDepartmentNode[] {
    return filterOperationalDepartmentsBySearch(this.operationalDepartments, this.orgStructureSearch);
  }

  get filteredOrgPoleOptions(): OrgPoleNode[] {
    const fromDept = this.orgOperationalDeptId
      ? polesForOperationalDept(this.operationalDepartments, this.orgOperationalDeptId)
      : [];
    const filtered = filterOperationalPolesBySearch(fromDept, this.orgStructureSearch);
    if (!this.orgOperationalDeptId && this.unassignedPoles.length) {
      return filterOperationalPolesBySearch(this.unassignedPoles, this.orgStructureSearch);
    }
    return filtered;
  }

  get filteredOrgCelluleOptions(): OrgCelluleNode[] {
    return filterOperationalCellulesBySearch(this.orgCelluleOptions, this.orgStructureSearch);
  }

  get filteredOrgServiceOptions(): OrgServiceNode[] {
    return filterOperationalServicesBySearch(this.orgServiceOptions, this.orgStructureSearch);
  }

  get roleDetectionSummary(): string {
    const depth = this.orgAssignmentDepth;
    if (depth === 'pole') return 'Affectation automatique : Département → Pôle';
    if (depth === 'cellule') return 'Affectation automatique : Département → Pôle → Cellule';
    if (depth === 'service') {
      return 'Affectation automatique : Département → Pôle → Cellule → Service';
    }
    return '';
  }

  get selectedOrgSummary(): string {
    return operationalSelectionSummary(
      this.operationalDepartments,
      this.unassignedPoles,
      this.orgOperationalDeptId,
      this.orgPoleId,
      this.orgCelluleId,
      this.orgServiceId,
    );
  }

  get showOrgFilterBar(): boolean {
    return this.showOrgAssignmentBlock && !this.orgLoading;
  }

  get showOrgOperationalDeptSelect(): boolean {
    return this.showOrgCascade && orgAssignmentRequiresOperationalDept(this.orgAssignmentDepth);
  }

  get showOrgPoleSelect(): boolean {
    return this.showOrgCascade && orgAssignmentRequiresPole(this.orgAssignmentDepth);
  }

  get showOrgCelluleSelect(): boolean {
    return this.showOrgCascade && orgAssignmentRequiresCellule(this.orgAssignmentDepth);
  }

  get showOrgServiceSelect(): boolean {
    return this.showOrgCascade && orgAssignmentRequiresService(this.orgAssignmentDepth);
  }

  get orgAssignmentRequired(): boolean {
    return orgAssignmentIsRequired(this.orgAssignmentDepth);
  }

  get orgAssignmentHintText(): string {
    return orgAssignmentHint(this.selectedRoleName, this.orgAssignmentDepth);
  }

  get orgCelluleOptions(): OrgCelluleNode[] {
    return cellulesForPole(this.operationalDepartments, this.unassignedPoles, this.orgPoleId);
  }

  get orgServiceOptions(): OrgServiceNode[] {
    return servicesForCellule(this.operationalDepartments, this.unassignedPoles, this.orgCelluleId);
  }

  get hasUnassignedPoles(): boolean {
    return this.unassignedPoles.length > 0;
  }

  private toDateInputValue(date: Date): string {
    return date.toLocaleDateString('en-CA');
  }

  private toISOString(dateStr: string): string {
    if (!dateStr) return new Date().toISOString();
    const [year, month, day] = dateStr.split('-').map(Number);
    return new Date(Date.UTC(year, month - 1, day, 12, 0, 0)).toISOString();
  }

  loadOrgAndSubServices(): void {
    this.orgLoading = true;
    this.userService.syncOrgMirrorFromDirectory().pipe(
      catchError((err) => {
        console.warn('syncOrgMirrorFromDirectory failed', err);
        this.orgMirrorWarning =
          'Synchronisation Planning ↔ Organisation RH échouée. Rechargez la page ou réessayez dans quelques secondes.';
        return of(null);
      }),
      switchMap(() =>
        forkJoin({
          overview: this.orgApi.loadOverview(),
          subServices: this.subServiceService.getAllSubServices(),
          services: this.serviceService.getAllServices(),
        }),
      ),
    ).subscribe({
      next: ({ overview, subServices, services }) => {
        this.orgOverview = overview;
        this.operationalDepartments = overview.operationalDepartments ?? [];
        this.unassignedPoles = overview.unassignedPoles ?? [];
        this.subServices = subServices ?? [];
        this.planningServices = services ?? [];
        this.orgLoading = false;
        this.reconcileOrgPickerAfterLoad();
        this.cdr.detectChanges();
      },
      error: () => {
        this.orgLoading = false;
        this.error = 'Impossible de charger la structure organisationnelle (Organisation RH).';
        this.cdr.detectChanges();
      },
    });
  }

  private ensureOrgPickerDefaults(): void {
    if (this.operationalDepartments.length === 0 && this.unassignedPoles.length === 0) {
      this.clearOrgAssignment();
      return;
    }

    const depts = this.filteredOperationalDepartments;
    if (this.showOrgOperationalDeptSelect) {
      if (this.orgOperationalDeptId && !this.operationalDepartments.some((d) => d.id === this.orgOperationalDeptId)) {
        this.orgOperationalDeptId = '';
      }
      if (!this.orgOperationalDeptId && !isUnassignedPole(this.unassignedPoles, this.orgPoleId)) {
        this.orgOperationalDeptId = depts[0]?.id ?? '';
      }
    }

    if (this.showOrgPoleSelect) {
      const poles = this.filteredOrgPoleOptions;
      if (!this.orgPoleId || !poles.some((p) => p.id === this.orgPoleId)) {
        this.orgPoleId = poles[0]?.id ?? '';
      }
      if (this.orgPoleId && isUnassignedPole(this.unassignedPoles, this.orgPoleId)) {
        this.orgOperationalDeptId = '';
      }
    } else {
      this.orgPoleId = '';
    }

    if (this.showOrgCelluleSelect) {
      const cellules = this.filteredOrgCelluleOptions;
      if (!this.orgCelluleId || !cellules.some((c) => c.id === this.orgCelluleId)) {
        this.orgCelluleId = cellules[0]?.id ?? '';
      }
    } else {
      this.orgCelluleId = '';
    }

    if (this.showOrgServiceSelect) {
      const services = this.filteredOrgServiceOptions;
      if (!this.orgServiceId || !services.some((s) => s.id === this.orgServiceId)) {
        this.orgServiceId = services[0]?.id ?? '';
      }
      this.syncSubServiceFromOrg();
    } else {
      this.orgServiceId = '';
      if (this.orgAssignmentDepth !== 'service') {
        this.form.subServiceId = null;
        this.orgMirrorWarning = null;
      }
    }
  }

  onOrgSearchChange(value: string): void {
    this.orgStructureSearch = value;
    this.cdr.detectChanges();
  }

  patchOrgFilterPole(poleId: string): void {
    this.orgFilterPoleId = poleId;
    this.orgFilterCelluleId = '';
    this.cdr.detectChanges();
  }

  patchOrgFilterCellule(celluleId: string): void {
    this.orgFilterCelluleId = celluleId;
    this.cdr.detectChanges();
  }

  clearOrgFilters(): void {
    this.orgStructureSearch = '';
    this.orgFilterPoleId = '';
    this.orgFilterCelluleId = '';
    this.cdr.detectChanges();
  }

  selectFlatService(opt: OperationalFlatServiceOption): void {
    this.patchOrgFlatService(opt.serviceId);
  }

  isFlatServiceSelected(opt: OperationalFlatServiceOption): boolean {
    return this.orgServiceId === opt.serviceId;
  }

  patchOrgOperationalDept(deptId: string): void {
    this.orgOperationalDeptId = deptId;
    this.orgPoleId = '';
    this.orgCelluleId = '';
    this.orgServiceId = '';
    this.form.subServiceId = null;
    this.orgMirrorWarning = null;
    this.ensureOrgPickerDefaults();
    this.cdr.detectChanges();
  }

  patchOrgPole(poleId: string): void {
    this.orgPoleId = poleId;
    if (isUnassignedPole(this.unassignedPoles, poleId)) {
      this.orgOperationalDeptId = '';
    } else {
      const sel = findOperationalSelectionByPoleId(this.operationalDepartments, this.unassignedPoles, poleId);
      if (sel?.operationalDeptId) {
        this.orgOperationalDeptId = sel.operationalDeptId;
      }
    }
    if (this.showOrgCelluleSelect) {
      const cellules = this.orgCelluleOptions;
      const curCell = this.orgCelluleId;
      if (!curCell || !cellules.some((c) => c.id === curCell)) {
        this.orgCelluleId = cellules[0]?.id ?? '';
      }
    } else {
      this.orgCelluleId = '';
    }
    if (this.showOrgServiceSelect) {
      const services = this.orgServiceOptions;
      const curSvc = this.orgServiceId;
      if (!curSvc || !services.some((s) => s.id === curSvc)) {
        this.orgServiceId = services[0]?.id ?? '';
      }
      this.syncSubServiceFromOrg();
    } else {
      this.orgServiceId = '';
      this.form.subServiceId = null;
      this.orgMirrorWarning = null;
    }
    this.cdr.detectChanges();
  }

  patchOrgCellule(celluleId: string): void {
    this.orgCelluleId = celluleId;
    if (this.showOrgServiceSelect) {
      const services = this.orgServiceOptions;
      const curSvc = this.orgServiceId;
      if (!curSvc || !services.some((s) => s.id === curSvc)) {
        this.orgServiceId = services[0]?.id ?? '';
      }
      this.syncSubServiceFromOrg();
    } else {
      this.orgServiceId = '';
      this.form.subServiceId = null;
      this.orgMirrorWarning = this.superviseurMirrorWarning();
    }
    this.cdr.detectChanges();
  }

  patchOrgService(serviceId: string): void {
    this.orgServiceId = serviceId;
    this.syncSubServiceFromOrg();
    this.cdr.detectChanges();
  }

  patchOrgFlatService(serviceId: string): void {
    const hit = this.orgFlatServiceOptions.find((o) => o.serviceId === serviceId);
    if (!hit) {
      this.orgOperationalDeptId = '';
      this.orgPoleId = '';
      this.orgCelluleId = '';
      this.orgServiceId = '';
    } else {
      this.orgOperationalDeptId = hit.operationalDeptId;
      this.orgPoleId = hit.poleId;
      this.orgCelluleId = hit.celluleId;
      this.orgServiceId = hit.serviceId;
    }
    this.syncSubServiceFromOrg();
    this.cdr.detectChanges();
  }

  clearOrgAssignment(): void {
    this.orgOperationalDeptId = '';
    this.orgPoleId = '';
    this.orgCelluleId = '';
    this.orgServiceId = '';
    this.form.subServiceId = null;
    this.orgMirrorWarning = null;
    this.cdr.detectChanges();
  }

  private superviseurMirrorWarning(): string | null {
    if (!isSuperviseurRole(this.selectedRoleName) || !this.orgCelluleId) return null;
    const svcId = resolvePlanningServiceIdByPrimeCelluleId(this.planningServices, this.orgCelluleId);
    return svcId
      ? null
      : 'Cette cellule existe dans Organisation RH mais n’est pas encore synchronisée dans Planning.';
  }

  private syncSubServiceFromOrg(): void {
    const needsService = this.showOrgFlatServiceSelect || this.showOrgServiceSelect;
    if (!needsService) {
      this.form.subServiceId = null;
      if (isSuperviseurRole(this.selectedRoleName)) {
        this.orgMirrorWarning = this.superviseurMirrorWarning();
      } else {
        this.orgMirrorWarning = null;
      }
      return;
    }
    const primeId = this.orgServiceId.trim();
    if (!primeId) {
      this.form.subServiceId = null;
      this.orgMirrorWarning = null;
      return;
    }
    const subId = resolveSubServiceIdByPrimeServiceId(this.subServices, primeId);
    this.form.subServiceId = subId;
    this.orgMirrorWarning = subId
      ? null
      : 'Ce service existe dans Organisation RH mais n’est pas encore synchronisé dans Planning. Attendez quelques secondes ou lancez la réconciliation.';
  }

  private applyOrgSelection(sel: {
    operationalDeptId: string;
    poleId: string;
    celluleId: string;
    serviceId: string;
  }): void {
    this.orgOperationalDeptId = sel.operationalDeptId;
    this.orgPoleId = sel.poleId;
    this.orgCelluleId = sel.celluleId;
    if (this.showOrgServiceSelect || this.showOrgFlatServiceSelect) {
      this.orgServiceId = sel.serviceId;
    } else {
      this.orgServiceId = '';
    }
    this.orgMirrorWarning = null;
  }

  private applyOrgFromSubServiceId(subServiceId: number | null | undefined): void {
    if (!subServiceId) {
      if (this.orgAssignmentDepth === 'cellule' && this.loadedManagedServiceIds.length) {
        this.applyOrgFromManagedServiceId(this.loadedManagedServiceIds[0]);
        return;
      }
      if (this.showOrgAssignmentBlock) {
        this.ensureOrgPickerDefaults();
      }
      return;
    }
    const sub = this.subServices.find((s) => s.id === subServiceId);
    const primeId = sub?.primeServiceId?.trim();
    if (!primeId) {
      this.orgMirrorWarning =
        'Affectation Planning sans lien Organisation RH — choisissez la structure ci-dessous.';
      return;
    }
    const sel = findOperationalSelectionByServiceId(
      this.operationalDepartments,
      this.unassignedPoles,
      primeId,
    );
    if (!sel) {
      this.orgMirrorWarning = 'Structure Organisation RH introuvable pour cet employé.';
      return;
    }
    this.applyOrgSelection(sel);
    this.form.subServiceId = subServiceId;
    this.orgMirrorWarning = null;
  }

  private applyOrgFromManagedServiceId(planningServiceId: number): void {
    const svc = this.planningServices.find((s) => s.id === planningServiceId);
    const cellulePrimeId = svc?.primeCelluleId?.trim();
    if (!cellulePrimeId) return;
    const sel = findOperationalSelectionByCelluleId(
      this.operationalDepartments,
      this.unassignedPoles,
      cellulePrimeId,
    );
    if (!sel) return;
    this.orgOperationalDeptId = sel.operationalDeptId;
    this.orgPoleId = sel.poleId;
    this.orgCelluleId = sel.celluleId;
    this.orgServiceId = '';
    this.orgMirrorWarning = null;
  }

  private reconcileOrgPickerAfterLoad(): void {
    const guid = this.loadedUserGuid.trim();
    const roleName = this.selectedRoleName;
    if (guid && (needsPrimeStructureAssignment(roleName) || isPiloteRole(roleName))) {
      if (this.applyOrgFromPrimeOverview(guid, roleName)) {
        return;
      }
    }
    if (this.form.subServiceId) {
      this.applyOrgFromSubServiceId(this.form.subServiceId);
    } else if (this.showOrgAssignmentBlock) {
      this.ensureOrgPickerDefaults();
    }
  }

  private applyOrgFromPrimeOverview(guid: string, roleName: string): boolean {
    const overview = this.orgOverview;
    if (!overview || !guid.trim()) return false;

    if (isChefDeProjetRole(roleName)) {
      const mgr = overview.managerEtage?.find((a) => a.userId === guid);
      if (!mgr?.etageId) return false;
      const sel = findOperationalSelectionByPoleId(
        this.operationalDepartments,
        this.unassignedPoles,
        mgr.etageId,
      );
      if (!sel) return false;
      this.orgOperationalDeptId = sel.operationalDeptId;
      this.orgPoleId = sel.poleId;
      this.orgCelluleId = '';
      this.orgServiceId = '';
      this.orgMirrorWarning = null;
      return true;
    }

    if (isSuperviseurRole(roleName)) {
      const sup = overview.supervisorService?.find((a) => a.userId === guid);
      if (!sup) return false;
      const celluleId = (sup.celluleId ?? sup.serviceId ?? '').trim();
      if (!celluleId) return false;
      const sel = findOperationalSelectionByCelluleId(
        this.operationalDepartments,
        this.unassignedPoles,
        celluleId,
      );
      if (!sel) return false;
      this.orgOperationalDeptId = sel.operationalDeptId;
      this.orgPoleId = sel.poleId;
      this.orgCelluleId = sel.celluleId;
      this.orgServiceId = '';
      this.orgMirrorWarning = null;
      return true;
    }

    if (isReferentTechniqueRole(roleName) || isPiloteRole(roleName)) {
      const coach = overview.coachSousService?.find((a) => a.userId === guid);
      const emp = overview.employees?.find((e) => e.id === guid);
      const svcId = (
        coach?.serviceId ??
        coach?.sousServiceId ??
        emp?.serviceId ??
        ''
      ).trim();
      if (!svcId) return false;
      const sel = findOperationalSelectionByServiceId(
        this.operationalDepartments,
        this.unassignedPoles,
        svcId,
      );
      if (!sel) return false;
      this.applyOrgSelection(sel);
      this.form.subServiceId = resolveSubServiceIdByPrimeServiceId(this.subServices, svcId);
      this.orgMirrorWarning = null;
      return true;
    }

    return false;
  }

  private loadDirectoryEmployeeContext(guid: string): void {
    if (!guid.trim()) return;
    this.http.get<{ businessDepartmentId?: string; businessDepartmentKind?: string }>(
      `/api/directory/employees/${encodeURIComponent(guid)}`,
    ).subscribe({
      next: (emp) => {
        const kind = String(emp.businessDepartmentKind ?? '').toLowerCase();
        if (kind === 'support' && emp.businessDepartmentId) {
          this.orgMode = 'support';
          this.supportDepartmentId = emp.businessDepartmentId;
          this.cdr.detectChanges();
        } else if (kind === 'operational' && emp.businessDepartmentId && isSupportManagerRole(this.selectedRoleName)) {
          this.orgMode = 'operational';
          this.operationalBusinessDepartmentId = emp.businessDepartmentId;
          this.cdr.detectChanges();
        }
      },
    });
  }

  loadUser(id: number): void {
    this.loading = true;
    this.userService.getUserById(id).subscribe({
      next: (user) => {
        this.form = {
          roleId: user.roleId,
          subServiceId: user.subServiceId ?? null,
          firstName: user.firstName,
          lastName: user.lastName,
          email: user.email,
          hireDate: user.hireDate
            ? this.toDateInputValue(new Date(user.hireDate))
            : this.toDateInputValue(new Date()),
          isActive: user.isActive,
          level: user.level ?? 1,
        };
        this.loadedManagedServiceIds = user.managedServices?.map(s => s.id) ?? [];
        this.loadedManagedSubServiceIds = user.managedSubServices?.map(s => s.id) ?? [];
        this.loadedUserGuid = resolveUserGuid(user);
        if (user.customFields) {
          for (const [key, value] of Object.entries(user.customFields)) {
            this.customFieldValues[key] = value ?? '';
          }
        }
        this.loadDirectoryEmployeeContext(this.loadedUserGuid);
        if (this.operationalDepartments.length > 0 || this.unassignedPoles.length > 0) {
          this.reconcileOrgPickerAfterLoad();
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = `Erreur: ${err.status}`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onRoleChange(roleId: number): void {
    this.form.roleId = Number(roleId);
    this.clearOrgFilters();
    if (!isSupportManagerRole(this.selectedRoleName)) {
      this.operationalBusinessDepartmentId = '';
    }
    if (this.orgAssignmentDepth === 'none') {
      this.clearOrgAssignment();
    } else {
      const depth = this.orgAssignmentDepth;
      this.orgServiceId = '';
      this.form.subServiceId = null;
      this.orgMirrorWarning = null;
      if (depth === 'pole') {
        this.orgCelluleId = '';
      } else if (depth === 'cellule') {
        this.orgServiceId = '';
      }
      this.ensureOrgPickerDefaults();
    }
    this.cdr.detectChanges();
  }

  setLevel(level: 1 | 2 | 3): void {
    this.form.level = level;
    this.cdr.detectChanges();
  }

  goToOrganisationRh(): void {
    void this.navActions.openOrganisationRh();
  }

  checkEmail(): void {
    if (!this.form.email.trim()) return;
    this.userService.checkEmailUnique(this.form.email, this.userId ?? undefined).subscribe({
      next: (res) => {
        this.emailError = res.isUnique ? null : 'Cet email est déjà utilisé.';
        this.cdr.detectChanges();
      }
    });
  }

  private validateOrgAssignment(): string | null {
    if (this.isSupportMode) return null;
    const depth = this.orgAssignmentDepth;
    if (!orgAssignmentIsRequired(depth)) return null;
    if (
      orgAssignmentRequiresOperationalDept(depth) &&
      !this.orgOperationalDeptId.trim() &&
      !isUnassignedPole(this.unassignedPoles, this.orgPoleId)
    ) {
      return 'Sélectionnez un département opérationnel (Organisation RH).';
    }
    if (orgAssignmentRequiresPole(depth) && !this.orgPoleId.trim()) {
      return 'Sélectionnez un pôle (Organisation RH).';
    }
    if (orgAssignmentRequiresCellule(depth) && !this.orgCelluleId.trim()) {
      return 'Sélectionnez une cellule (Organisation RH).';
    }
    if (orgAssignmentRequiresService(depth) && !this.orgServiceId.trim()) {
      return 'Sélectionnez un service (Organisation RH).';
    }
    if (this.orgMirrorWarning) return this.orgMirrorWarning;
    if (orgAssignmentRequiresService(depth) && !this.form.subServiceId) {
      return 'Le service choisi n’est pas encore disponible dans Planning.';
    }
    if (isSuperviseurRole(this.selectedRoleName)) {
      const svcId = resolvePlanningServiceIdByPrimeCelluleId(this.planningServices, this.orgCelluleId);
      if (!svcId) {
        return 'La cellule choisie n’est pas encore synchronisée dans Planning.';
      }
    }
    return null;
  }

  private validateOperationalManagerAssignment(): string | null {
    if (!this.showOperationalManagerBlock) return null;
    if (!this.operationalBusinessDepartmentId.trim()) {
      return 'Sélectionnez un département opérationnel.';
    }
    if (this.operationalBusinessDepartments.length === 0) {
      return 'Aucun département opérationnel disponible — créez-en un dans Organisation RH.';
    }
    return null;
  }

  private validateSupportAssignment(): string | null {
    if (!this.isSupportMode) return null;
    if (!this.supportDepartmentId.trim()) {
      return 'Sélectionnez un département Support.';
    }
    if (this.supportDepartments.length === 0) {
      return 'Aucun département Support disponible — créez-en un dans Organisation support.';
    }
    return null;
  }

  private buildUserMutationDto(): Pick<
    CreateUserDto,
    'roleId' | 'subServiceId' | 'managedSubServiceIds' | 'managedServiceIds' | 'firstName' | 'lastName' | 'email' | 'level'
  > {
    const roleName = this.selectedRoleName;
    let subServiceId = this.form.subServiceId ?? undefined;
    const managedServiceIds: number[] = [];
    const managedSubServiceIds: number[] = [];
    if (this.isSupportMode) {
      subServiceId = undefined;
    } else if (isChefDeProjetRole(roleName) || isSuperviseurRole(roleName)) {
      subServiceId = undefined;
    }
    return {
      roleId: this.resolvedRoleId(),
      subServiceId,
      managedSubServiceIds,
      managedServiceIds,
      firstName: this.form.firstName,
      lastName: this.form.lastName,
      email: this.form.email,
      level: this.form.level,
    };
  }

  private buildCustomFieldsPayload(): Record<string, string | null> {
    const payload: Record<string, string | null> = {};
    for (const field of this.customEmployeeFields) {
      const raw = (this.customFieldValues[field.fieldKey] ?? '').trim();
      payload[field.fieldKey] = raw.length ? raw : null;
    }
    return payload;
  }

  private buildCreateUserDto(): CreateUserDto {
    return {
      ...this.buildUserMutationDto(),
      hireDate: this.toISOString(this.form.hireDate),
      customFields: this.buildCustomFieldsPayload(),
    };
  }

  private formatRevokedLog(result: unknown): string[] {
    if (!result || typeof result !== 'object') return [];
    const revoked = (result as StructuralRoleAssignmentResult).revoked;
    if (!Array.isArray(revoked) || revoked.length === 0) return [];
    return revoked.map((v) => {
      const where = v.nodeLabel ?? v.departmentCode ?? v.nodeId;
      return `Ancien rôle retiré : ${v.role}${where ? ` (${where})` : ''}`;
    });
  }

  private showRevokedToasts(result: unknown): void {
    for (const msg of this.formatRevokedLog(result)) {
      this.toastService.show(msg, 'info', 6000);
    }
  }

  private applyPrimeStructureAssignment(
    employeeGuid: string,
    roleName: string,
    strict = false,
  ): Observable<StructuralRoleAssignmentResult | void> {
    if (!needsPrimeStructureAssignment(roleName)) {
      return of(undefined);
    }
    const depth = orgRoleAssignmentDepth(roleName);
    const call = (): Observable<StructuralRoleAssignmentResult> => {
      if (depth === 'pole') {
        return this.orgApi.setStructureManager(this.orgPoleId, employeeGuid);
      }
      if (depth === 'cellule') {
        return this.orgApi.setStructureSupervisor(this.orgCelluleId, employeeGuid);
      }
      if (isReferentTechniqueRole(roleName)) {
        return this.orgApi.setStructureCoach(this.orgServiceId, employeeGuid);
      }
      return of({ revoked: [] });
    };
    return defer(() => call()).pipe(
      retry({ count: 10, delay: 800 }),
      catchError((err) => (strict ? throwError(() => err) : of(undefined))),
    );
  }

  private syncPiloteDirectoryAssignment(employeeGuid: string): Observable<void> {
    const serviceId = this.orgServiceId.trim();
    if (!serviceId) {
      return throwError(() => new Error('Service Pilote manquant.'));
    }
    return this.orgApi.addStructurePilot(serviceId, employeeGuid).pipe(
      retry({ count: 5, delay: 800 }),
      map((result) => {
        this.showRevokedToasts(result);
        return undefined;
      }),
    );
  }

  private buildEnsureEmployeeDto(employeeGuid: string, roleName: string) {
    const primeServiceId = this.orgServiceId.trim() || null;
    return {
      employeeId: employeeGuid,
      firstName: this.form.firstName.trim(),
      lastName: this.form.lastName.trim(),
      email: this.form.email.trim(),
      role: roleName,
      primeServiceId,
    };
  }

  private ensureEmployeeInDirectory(employeeGuid: string, roleName: string): Observable<void> {
    return this.orgApi.ensureEmployeeFromPlanning(this.buildEnsureEmployeeDto(employeeGuid, roleName)).pipe(
      map(() => undefined),
      catchError((ensureErr) =>
        this.orgApi.waitForEmployee(employeeGuid, 3000).pipe(
          map(() => undefined),
          catchError(() => throwError(() => ensureErr)),
        ),
      ),
    );
  }

  private syncPrimeStructureAssignment(employeeGuid: string, roleName: string): Observable<void> {
    if (isPiloteRole(roleName)) {
      return this.ensureEmployeeInDirectory(employeeGuid, roleName).pipe(
        switchMap(() => this.syncPiloteDirectoryAssignment(employeeGuid)),
      );
    }
    if (!needsPrimeStructureAssignment(roleName)) {
      return of(undefined);
    }
    return this.ensureEmployeeInDirectory(employeeGuid, roleName).pipe(
      switchMap(() => this.applyPrimeStructureAssignment(employeeGuid, roleName, true)),
      map((result) => {
        this.showRevokedToasts(result);
        return undefined;
      }),
    );
  }

  private syncOperationalManagerAssignment(employeeGuid: string, roleName: string): Observable<void> {
    const deptId = this.operationalBusinessDepartmentId.trim();
    if (!deptId) {
      return throwError(() => new Error('Département opérationnel manquant.'));
    }
    return this.ensureEmployeeInDirectory(employeeGuid, roleName).pipe(
      switchMap(() =>
        defer(() =>
          this.http.post<StructuralRoleAssignmentResult>(
            `/api/directory/business-departments/${deptId}/manager`,
            { employeeId: employeeGuid },
          ),
        ).pipe(
          map((result) => {
            this.showRevokedToasts(result);
            return undefined;
          }),
        ),
      ),
      retry({ count: 5, delay: 800 }),
    );
  }

  private resolveDirectorySync$(employeeGuid: string, roleName: string): Observable<void> {
    if (this.isSupportMode) {
      return this.syncSupportDirectoryAssignment(employeeGuid, roleName);
    }
    if (this.showOperationalManagerBlock) {
      return this.syncOperationalManagerAssignment(employeeGuid, roleName);
    }
    return this.syncPrimeStructureAssignment(employeeGuid, roleName);
  }

  private syncSupportDirectoryAssignment(employeeGuid: string, roleName: string): Observable<void> {
    const deptId = this.supportDepartmentId.trim();
    if (!deptId) {
      return throwError(() => new Error('Département Support manquant.'));
    }
    const hireDateISO = this.toISOString(this.form.hireDate);
    const apply = (): Observable<void> => {
      if (isSupportManagerRole(roleName)) {
        return defer(() =>
          this.http.post<StructuralRoleAssignmentResult>(
            `/api/directory/business-departments/${deptId}/manager`,
            { employeeId: employeeGuid },
          ),
        ).pipe(
          map((result) => {
            this.showRevokedToasts(result);
            return undefined;
          }),
        );
      }
      return defer(() =>
        this.http.put(`/api/directory/employees/${employeeGuid}`, {
          firstName: this.form.firstName.trim(),
          lastName: this.form.lastName.trim(),
          email: this.form.email.trim(),
          role: roleName,
          serviceId: null,
          parentId: null,
          isActive: true,
          hireDate: hireDateISO,
          businessDepartmentId: deptId,
        }),
      ).pipe(map(() => undefined));
    };
    return this.ensureEmployeeInDirectory(employeeGuid, roleName).pipe(
      switchMap(() => apply()),
      retry({ count: 5, delay: 800 }),
    );
  }

  private async confirmCrossRoleAssignment(employeeId: string): Promise<boolean> {
    const overview = this.orgOverview;
    if (!overview || !employeeId.trim()) return true;
    const existing = findEmployeeStructuralRole(overview, employeeId);
    if (!existing) return true;
    const name = employeeDisplayName(overview.employees, employeeId);
    return this.confirmService.confirm({
      title: 'Remplacer le rôle actuel',
      message: buildCrossRoleOverwriteMessage(name, existing),
      confirmLabel: 'Remplacer et continuer',
      cancelLabel: 'Annuler',
      variant: 'warning',
    });
  }

  private async confirmStructureAssignmentIfNeeded(roleName: string): Promise<boolean> {
    if (this.isSupportMode || this.showOperationalManagerBlock) {
      return true;
    }
    const needsStructure = needsPrimeStructureAssignment(roleName) || isPiloteRole(roleName);
    if (!needsStructure) {
      return true;
    }
    const overview = this.orgOverview;
    if (!overview) {
      this.error = 'Structure organisationnelle non chargée — réessayez dans quelques secondes.';
      return false;
    }

    if (needsPrimeStructureAssignment(roleName)) {
      const incumbent = findStructureIncumbent(overview, roleName, {
        orgPoleId: this.orgPoleId,
        orgCelluleId: this.orgCelluleId,
        orgServiceId: this.orgServiceId,
      });
      if (incumbent) {
        const assigneeGuid = this.isEditMode ? this.loadedUserGuid : null;
        if (shouldConfirmOverwrite(incumbent.userId, assigneeGuid)) {
          const ok = await this.confirmService.confirm({
            title: 'Remplacer le titulaire actuel',
            message: buildStructureOverwriteMessage(incumbent, roleName),
            confirmLabel: 'Écraser et continuer',
            cancelLabel: 'Annuler',
            variant: 'warning',
          });
          if (!ok) return false;
        }
      }
    }

    if (this.isEditMode && this.loadedUserGuid.trim()) {
      return this.confirmCrossRoleAssignment(this.loadedUserGuid);
    }
    return true;
  }

  submit(): void {
    void this.submitAsync();
  }

  private async submitAsync(): Promise<void> {
    if (!this.form.roleId || !this.form.firstName.trim() ||
        !this.form.lastName.trim() || !this.form.email.trim() || !this.form.hireDate) {
      this.error = 'Tous les champs obligatoires doivent être remplis.';
      return;
    }
    if (this.emailError) return;
    const supportError = this.validateSupportAssignment();
    if (supportError) {
      this.error = supportError;
      return;
    }
    const operationalManagerError = this.validateOperationalManagerAssignment();
    if (operationalManagerError) {
      this.error = operationalManagerError;
      return;
    }
    const orgError = this.validateOrgAssignment();
    if (orgError) {
      this.error = orgError;
      return;
    }
    const roleName = this.selectedRoleName;
    if (!(await this.confirmStructureAssignmentIfNeeded(roleName))) {
      return;
    }
    this.submitting = true;
    this.error = null;
    const hireDateISO = this.toISOString(this.form.hireDate);
    if (this.isEditMode && this.userId) {
      const dto: UpdateUserDto = {
        ...this.buildUserMutationDto(),
        hireDate: hireDateISO,
        isActive: this.form.isActive,
        customFields: this.buildCustomFieldsPayload(),
      };
      const sync$ = this.resolveDirectorySync$(this.loadedUserGuid.trim(), roleName);
      this.userService.updateUser(this.userId, dto).pipe(
        switchMap(() => {
          const guid = this.loadedUserGuid.trim();
          if (!guid) {
            return throwError(
              () => new Error('Identifiant employé manquant — rechargez la fiche puis réessayez.'),
            );
          }
          return sync$;
        }),
      ).subscribe({
        next: () => this.router.navigate(['/users', this.userId]),
        error: (err) => {
          this.error = formatHttpErrorMessage(err, 'Échec de la synchronisation Organisation RH.');
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      const dto = this.buildCreateUserDto();
      this.userService.createUser(dto).pipe(
        switchMap((user) => {
          const guid = resolveUserGuid(user);
          if (!guid) {
            return throwError(
              () => new Error('Réponse serveur invalide : identifiant employé (guid) manquant.'),
            );
          }
          return this.resolveDirectorySync$(guid, roleName).pipe(map(() => user));
        }),
      ).subscribe({
        next: (user) => {
          this.router.navigate(['/users', user.id]);
        },
        error: (err) => {
          this.error = formatHttpErrorMessage(err);
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  goBack(): void {
    this.isEditMode
      ? this.router.navigate(['/users', this.userId])
      : this.router.navigate(['/users']);
  }
}
