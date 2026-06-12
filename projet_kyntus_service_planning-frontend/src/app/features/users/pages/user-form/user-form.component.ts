import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, defer, of, Observable } from 'rxjs';
import { retry, switchMap, map, catchError } from 'rxjs/operators';
import { UserService } from '../../services/user.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { ServiceService } from '../../../services/services/service';
import { CreateUserDto, UpdateUserDto } from '../../users-module';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../../shared/directives/kyntus-select-sync.directive';
import { LockKeyhole, Search, Sparkles } from 'lucide';
import { NavigationActionsService } from '../../../../core/navigation/navigation-actions.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import type { Department, LegacyCellule, LegacyPole } from '../../../prime/models';
import type { SubService } from '../../../sub-services/sub-services-module';
import type { Service } from '../../../services/services-module';
import {
  celluleFilterOptions,
  filterCellulesBySearch,
  filterDepartmentsBySearch,
  filterFlatServiceOptions,
  filterPolesBySearch,
  orgSelectionSummary,
  poleFilterOptions,
} from '../../../../core/org/org-structure-filter';
import {
  findOrgSelectionByPrimeServiceId,
  flattenOrgServiceOptions,
  poleCells,
  resolvePlanningServiceIdByPrimeCelluleId,
  resolveSubServiceIdByPrimeServiceId,
  type OrgFlatServiceOption,
} from '../../../../core/org/planning-org-picker';
import {
  orgAssignmentHint,
  orgAssignmentIsRequired,
  orgAssignmentRequiresCellule,
  orgAssignmentRequiresPole,
  orgAssignmentRequiresService,
  orgRoleAssignmentDepth,
  needsPrimeStructureAssignment,
  isReferentTechniqueRole,
  isSuperviseurRole,
  isChefDeProjetRole,
  type OrgRoleAssignmentDepth,
} from '../../../../core/org/org-role-assignment';
interface RoleOption { id: number; name: string; }
@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusSelectSyncDirective],
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css']
})
export class UserFormComponent implements OnInit {
  readonly icons = { lock: LockKeyhole, search: Search, detect: Sparkles };
  isEditMode = false;
  userId: number | null = null;
  subServices: SubService[] = [];
  planningServices: Service[] = [];
  orgDepartments: Department[] = [];
  orgLoading = false;
  orgPoleId = '';
  orgCelluleId = '';
  orgServiceId = '';
  orgMirrorWarning: string | null = null;
  orgStructureSearch = '';
  orgFilterPoleId = '';
  orgFilterCelluleId = '';
  loading = false;
  submitting = false;
  error: string | null = null;
  emailError: string | null = null;
  roles: RoleOption[] = [];
  private loadedManagedServiceIds: number[] = [];
  private loadedManagedSubServiceIds: number[] = [];
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
    private http: HttpClient,
    private navActions: NavigationActionsService,
    private orgApi: PrimeOrgApiService,
    private cdr: ChangeDetectorRef
  ) {}
  ngOnInit(): void {
    this.loadOrgAndSubServices();
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
        ];
        this.cdr.detectChanges();
      }
    });
  }
  private resolvedRoleId(): number {
    return Number(this.form.roleId) || 0;
  }
  get selectedRoleName(): string {
    const id = this.resolvedRoleId();
    return this.roles.find((r) => Number(r.id) === id)?.name ?? '';
  }
  get orgAssignmentDepth(): OrgRoleAssignmentDepth {
    return orgRoleAssignmentDepth(this.selectedRoleName);
  }
  get showOrgAssignmentBlock(): boolean {
    return this.orgAssignmentDepth !== 'none';
  }
  get showOrgFlatServiceSelect(): boolean {
    return isReferentTechniqueRole(this.selectedRoleName);
  }
  get showOrgCascade(): boolean {
    return this.showOrgAssignmentBlock && !this.showOrgFlatServiceSelect;
  }
  get orgFlatServiceOptions(): OrgFlatServiceOption[] {
    return flattenOrgServiceOptions(this.orgDepartments);
  }
  get orgPoleFilterOptions(): { id: string; name: string }[] {
    return poleFilterOptions(this.orgDepartments);
  }
  get orgCelluleFilterOptions(): { id: string; name: string }[] {
    return celluleFilterOptions(this.orgDepartments, this.orgFilterPoleId);
  }
  get filteredFlatServices(): { visible: OrgFlatServiceOption[]; totalMatches: number } {
    return filterFlatServiceOptions(this.orgFlatServiceOptions, {
      search: this.orgStructureSearch,
      poleId: this.orgFilterPoleId || undefined,
      celluleId: this.orgFilterCelluleId || undefined,
    });
  }
  get filteredOrgDepartments(): Department[] {
    return filterDepartmentsBySearch(this.orgDepartments, this.orgStructureSearch);
  }
  get filteredOrgCelluleOptions(): LegacyPole[] {
    return filterPolesBySearch(this.orgCelluleOptions, this.orgStructureSearch);
  }
  get filteredOrgServiceOptions(): LegacyCellule[] {
    return filterCellulesBySearch(this.orgServiceOptions, this.orgStructureSearch);
  }
  get roleDetectionSummary(): string {
    const depth = this.orgAssignmentDepth;
    if (depth === 'pole') return 'Affectation automatique : Pôle';
    if (depth === 'cellule') return 'Affectation automatique : Pôle → Cellule';
    if (depth === 'service') {
      return isReferentTechniqueRole(this.selectedRoleName)
        ? 'Affectation automatique : Service (recherche et filtres)'
        : 'Affectation automatique : Pôle → Cellule → Service';
    }
    return '';
  }
  get selectedOrgSummary(): string {
    return orgSelectionSummary(this.orgDepartments, this.orgPoleId, this.orgCelluleId, this.orgServiceId);
  }
  get showOrgFilterBar(): boolean {
    return this.showOrgAssignmentBlock && !this.orgLoading;
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
  get orgCelluleOptions(): LegacyPole[] {
    const dept = this.orgDepartments.find((d) => d.id === this.orgPoleId);
    return dept?.poles ?? [];
  }
  get orgServiceOptions(): LegacyCellule[] {
    const pole = this.orgCelluleOptions.find((p) => p.id === this.orgCelluleId);
    return pole ? poleCells(pole) : [];
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
    forkJoin({
      departments: this.http.get<Department[]>('/api/prime/departments'),
      subServices: this.subServiceService.getAllSubServices(),
      services: this.serviceService.getAllServices(),
    }).subscribe({
      next: ({ departments, subServices, services }) => {
        this.orgDepartments = departments ?? [];
        this.subServices = subServices ?? [];
        this.planningServices = services ?? [];
        this.orgLoading = false;
        if (this.form.subServiceId) {
          this.applyOrgFromSubServiceId(this.form.subServiceId);
        } else if (this.showOrgAssignmentBlock) {
          this.ensureOrgPickerDefaults();
        }
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
    if (this.orgDepartments.length === 0) {
      this.clearOrgAssignment();
      return;
    }
    if (this.showOrgFlatServiceSelect) {
      if (!this.orgFlatServiceOptions.some((o) => o.serviceId === this.orgServiceId)) {
        this.orgServiceId = '';
      }
      this.syncSubServiceFromOrg();
      return;
    }
    const poles = this.filteredOrgDepartments;
    if (!this.orgDepartments.some((d) => d.id === this.orgPoleId)) {
      this.orgPoleId = poles[0]?.id ?? this.orgDepartments[0]?.id ?? '';
    } else if (!this.orgPoleId && poles.length === 1) {
      this.orgPoleId = poles[0].id;
    }
    if (this.showOrgCelluleSelect) {
      const cellules = this.filteredOrgCelluleOptions;
      if (!cellules.some((p) => p.id === this.orgCelluleId)) {
        this.orgCelluleId = cellules[0]?.id ?? '';
      } else if (!this.orgCelluleId && cellules.length === 1) {
        this.orgCelluleId = cellules[0].id;
      }
    } else {
      this.orgCelluleId = '';
    }
    if (this.showOrgServiceSelect) {
      const services = this.filteredOrgServiceOptions;
      if (!services.some((s) => s.id === this.orgServiceId)) {
        this.orgServiceId = services[0]?.id ?? '';
      } else if (!this.orgServiceId && services.length === 1) {
        this.orgServiceId = services[0].id;
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
  selectFlatService(opt: OrgFlatServiceOption): void {
    this.patchOrgFlatService(opt.serviceId);
  }
  isFlatServiceSelected(opt: OrgFlatServiceOption): boolean {
    return this.orgServiceId === opt.serviceId;
  }
  patchOrgPole(poleId: string): void {
    this.orgPoleId = poleId;
    if (this.showOrgCelluleSelect) {
      const poles = this.orgCelluleOptions;
      const curCell = this.orgCelluleId;
      if (!curCell || !poles.some((p) => p.id === curCell)) {
        this.orgCelluleId = poles[0]?.id ?? '';
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
      this.orgPoleId = '';
      this.orgCelluleId = '';
      this.orgServiceId = '';
    } else {
      this.orgPoleId = hit.poleId;
      this.orgCelluleId = hit.celluleId;
      this.orgServiceId = hit.serviceId;
    }
    this.syncSubServiceFromOrg();
    this.cdr.detectChanges();
  }
  clearOrgAssignment(): void {
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
    const sel = findOrgSelectionByPrimeServiceId(this.orgDepartments, primeId);
    if (!sel) {
      this.orgMirrorWarning = 'Structure Organisation RH introuvable pour cet employé.';
      return;
    }
    this.orgPoleId = sel.poleId;
    this.orgCelluleId = sel.celluleId;
    if (this.showOrgServiceSelect || this.showOrgFlatServiceSelect) {
      this.orgServiceId = sel.serviceId;
    }
    this.form.subServiceId = subServiceId;
    this.orgMirrorWarning = null;
  }
  private applyOrgFromManagedServiceId(planningServiceId: number): void {
    const svc = this.planningServices.find((s) => s.id === planningServiceId);
    const cellulePrimeId = svc?.primeCelluleId?.trim();
    if (!cellulePrimeId) return;
    for (const dept of this.orgDepartments) {
      for (const pole of dept.poles ?? []) {
        if (pole.id === cellulePrimeId) {
          this.orgPoleId = dept.id;
          this.orgCelluleId = pole.id;
          this.orgMirrorWarning = null;
          return;
        }
      }
    }
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
        if (this.subServices.length > 0 && this.orgDepartments.length > 0) {
          this.applyOrgFromSubServiceId(user.subServiceId);
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
    if (this.orgAssignmentDepth === 'none') {
      this.clearOrgAssignment();
    } else {
      this.orgServiceId = '';
      this.form.subServiceId = null;
      this.orgMirrorWarning = null;
      if (this.orgAssignmentDepth === 'pole') {
        this.orgCelluleId = '';
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
    const depth = this.orgAssignmentDepth;
    if (!orgAssignmentIsRequired(depth)) return null;
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
  private buildCreateUserDto(): CreateUserDto {
    const depth = this.orgAssignmentDepth;
    const roleName = this.selectedRoleName;
    let subServiceId = this.form.subServiceId ?? undefined;
    let managedServiceIds: number[] = [];
    let managedSubServiceIds: number[] = [];
    if (isChefDeProjetRole(roleName)) {
      subServiceId = undefined;
    } else if (isSuperviseurRole(roleName)) {
      subServiceId = undefined;
      const svcId = resolvePlanningServiceIdByPrimeCelluleId(this.planningServices, this.orgCelluleId);
      if (svcId) managedServiceIds = [svcId];
    } else if (isReferentTechniqueRole(roleName) && subServiceId) {
      managedSubServiceIds = [subServiceId];
    }
    return {
      roleId: this.resolvedRoleId(),
      subServiceId,
      managedSubServiceIds,
      managedServiceIds,
      firstName: this.form.firstName,
      lastName: this.form.lastName,
      email: this.form.email,
      hireDate: this.toISOString(this.form.hireDate),
      level: this.form.level,
    };
  }
  private applyPrimeStructureAssignment(employeeGuid: string, roleName: string): Observable<void> {
    if (!needsPrimeStructureAssignment(roleName)) {
      return of(undefined);
    }
    const depth = orgRoleAssignmentDepth(roleName);
    const call = (): Observable<unknown> => {
      if (depth === 'pole') {
        return this.orgApi.setStructureManager(this.orgPoleId, employeeGuid);
      }
      if (depth === 'cellule') {
        return this.orgApi.setStructureSupervisor(this.orgCelluleId, employeeGuid);
      }
      if (isReferentTechniqueRole(roleName)) {
        return this.orgApi.setStructureCoach(this.orgServiceId, employeeGuid);
      }
      return of(null);
    };
    return defer(() => call()).pipe(
      retry({ count: 10, delay: 800 }),
      map(() => undefined),
      catchError(() => of(undefined)),
    );
  }
  submit(): void {
    if (!this.form.roleId || !this.form.firstName.trim() ||
        !this.form.lastName.trim() || !this.form.email.trim() || !this.form.hireDate) {
      this.error = 'Tous les champs obligatoires doivent être remplis.';
      return;
    }
    if (this.emailError) return;
    const orgError = this.validateOrgAssignment();
    if (orgError) {
      this.error = orgError;
      return;
    }
    this.submitting = true;
    this.error = null;
    const hireDateISO = this.toISOString(this.form.hireDate);
    if (this.isEditMode && this.userId) {
      const dto: UpdateUserDto = {
        roleId: this.resolvedRoleId(),
        subServiceId: this.form.subServiceId ?? undefined,
        managedSubServiceIds: this.loadedManagedSubServiceIds,
        managedServiceIds: this.loadedManagedServiceIds,
        firstName: this.form.firstName,
        lastName: this.form.lastName,
        email: this.form.email,
        hireDate: hireDateISO,
        isActive: this.form.isActive,
        level: this.form.level,
      };
      this.userService.updateUser(this.userId, dto).subscribe({
        next: () => this.router.navigate(['/users', this.userId]),
        error: (err) => {
          this.error = `Erreur: ${err.error?.message || err.status}`;
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      const dto = this.buildCreateUserDto();
      const roleName = this.selectedRoleName;
      this.userService.createUser(dto).pipe(
        switchMap((user) =>
          this.applyPrimeStructureAssignment(user.guid, roleName).pipe(map(() => user)),
        ),
      ).subscribe({
        next: (user) => {
          this.router.navigate(['/users', user.id]);
        },
        error: (err) => {
          this.error = `Erreur: ${err.error?.message || err.status}`;
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
