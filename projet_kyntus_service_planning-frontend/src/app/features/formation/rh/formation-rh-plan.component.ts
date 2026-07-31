import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Search, UserPlus, X } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type { TrainingSessionDto } from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { KyntusSelectSyncDirective } from '../../../shared/directives/kyntus-select-sync.directive';
import { UserService } from '../../users/services/user.service';
import type { User } from '../../users/users-module';
import { SubServiceService } from '../../sub-services/services/sub-service.service';
import { PrimeOrgApiService } from '../../prime/services/prime-org-api.service';
import type { Department } from '../../prime/models';
import type { OperationalDepartmentNode } from '../../prime/models/org-tree.types';
import {
  buildEmployeePickerRows,
  filterEmployeePickerRows,
  type EmployeePickerRow,
} from '../../contract/lib/contract-employee-filter';
import { resolveUserGuid } from '../../../core/lib/user-guid.util';
import { buildOperationalOrgFilterOptions } from '../../../core/org/org-structure-filter';
import {
  enrichUserOrgPerimeter,
  orgPerimeterSummary,
  type UserOrgPerimeterView,
} from '../../../core/org/user-org-perimeter';

@Component({
  selector: 'app-formation-rh-plan',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    KyntusPageHeaderComponent,
    LucideIconComponent,
    KyntusSelectSyncDirective,
  ],
  templateUrl: './formation-rh-plan.component.html',
  styleUrls: ['./formation-rh-plan.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationRhPlanComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  private readonly usersApi = inject(UserService);
  private readonly http = inject(HttpClient);
  private readonly orgApi = inject(PrimeOrgApiService);
  private readonly subServiceService = inject(SubServiceService);

  readonly icons = { search: Search, add: UserPlus, remove: X };
  readonly sessions = signal<TrainingSessionDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly assignSessionId = signal<string | null>(null);
  readonly assignMsg = signal<string | null>(null);

  private employeeRows: EmployeePickerRow[] = [];

  operationalDepartments: OperationalDepartmentNode[] = [];
  operationalDepartmentOptions: string[] = [];
  poleOptions: string[] = [];
  celluleOptions: string[] = [];
  serviceOptions: string[] = [];

  filterOperationalDepartment = '';
  filterPole = '';
  filterCellule = '';
  filterService = '';

  animatorSearch = '';
  assignSearch = '';
  checklistSearch = '';

  readonly selectedAnimator = signal<EmployeePickerRow | null>(null);
  readonly animatorSessions = signal<TrainingSessionDto[]>([]);
  readonly animatorSessionsLoading = signal(false);

  /** Checklist du périmètre courant (sélection temporaire avant ajout). */
  readonly perimeterChecklist = signal<{ row: EmployeePickerRow; checked: boolean }[]>([]);

  /** Liste cumulée des bénéficiaires (plusieurs périmètres possibles). */
  readonly beneficiaryList = signal<EmployeePickerRow[]>([]);

  readonly assignSelected = signal<EmployeePickerRow[]>([]);

  private readonly searchTick = signal(0);
  private readonly orgTick = signal(0);

  readonly visibleAnimatorRows = computed(() => {
    this.searchTick();
    const selectedGuid = resolveUserGuid(this.selectedAnimator()?.user);
    const { visible } = filterEmployeePickerRows(
      this.employeeRows.filter((r) => {
        const g = resolveUserGuid(r.user);
        return !!g && g !== selectedGuid;
      }),
      { search: this.animatorSearch },
      25,
    );
    return visible;
  });

  readonly visibleAssignRows = computed(() => {
    this.searchTick();
    const selected = new Set(this.assignSelected().map((r) => resolveUserGuid(r.user)));
    const { visible } = filterEmployeePickerRows(
      this.employeeRows.filter((r) => {
        const g = resolveUserGuid(r.user);
        return !!g && !selected.has(g);
      }),
      { search: this.assignSearch },
      25,
    );
    return visible;
  });

  readonly beneficiaryGuidSet = computed(() => {
    const set = new Set<string>();
    for (const row of this.beneficiaryList()) {
      const g = resolveUserGuid(row.user);
      if (g) set.add(g);
    }
    return set;
  });

  /** Checklist du périmètre, filtrée par la barre de recherche (nom, email, rôle, org). */
  readonly visiblePerimeterChecklist = computed(() => {
    this.searchTick();
    const list = this.perimeterChecklist();
    const q = this.normalizeSearch(this.checklistSearch);
    if (!q) return list;
    return list.filter((item) => {
      const haystack = this.normalizeSearch(
        [
          item.row.displayName,
          item.row.user.email,
          item.row.user.roleName,
          orgPerimeterSummary(item.row.perimeter),
        ]
          .filter(Boolean)
          .join(' '),
      );
      return haystack.includes(q);
    });
  });

  /** Cases cochées dans le périmètre courant (hors déjà inscrits). */
  readonly checkedInPerimeter = computed(() =>
    this.perimeterChecklist().filter((c) => {
      if (!c.checked) return false;
      const g = resolveUserGuid(c.row.user);
      return !!g && !this.beneficiaryGuidSet().has(g);
    }),
  );

  readonly hasOrgSelection = computed(() => {
    this.orgTick();
    return !!(
      this.filterOperationalDepartment ||
      this.filterPole ||
      this.filterCellule ||
      this.filterService
    );
  });

  form = {
    title: '',
    description: '',
    capacity: 10,
    mode: 'Single' as 'Single' | 'Multiple',
    sessionCount: 1,
    sessionSlots: [createDefaultSlot(0)] as SessionSlot[],
    animatorKind: 'Internal' as 'Internal' | 'External',
    animatorUserId: '',
    externalAnimatorName: '',
    externalAnimatorOrganization: '',
    externalAnimatorEmail: '',
    externalAnimatorPhone: '',
    catalogItemId: '',
    learningGateMode: '' as '' | 'Attendance' | 'Content' | 'Both',
  };

  catalogItems: { id: string; title: string }[] = [];

  ngOnInit(): void {
    this.ensureDefaultSlots();
    void this.reload();
    void this.loadOrgAndEmployees();
    void this.loadCatalogItems();
  }

  async loadCatalogItems(): Promise<void> {
    try {
      const items = await this.api.listCatalog(false);
      this.catalogItems = items
        .filter((i) => i.status === 'Published' || i.status === 1)
        .map((i) => ({ id: i.id, title: i.title }));
    } catch {
      this.catalogItems = [];
    }
  }

  perimeterLabel(row: EmployeePickerRow): string {
    return orgPerimeterSummary(row.perimeter) || '—';
  }

  refreshOrgFilterOptions(): void {
    const opts = buildOperationalOrgFilterOptions(this.operationalDepartments, {
      operationalDepartment: this.filterOperationalDepartment || undefined,
      pole: this.filterPole || undefined,
      cellule: this.filterCellule || undefined,
    });
    this.operationalDepartmentOptions = opts.operationalDepartments;
    this.poleOptions = opts.poles;
    this.celluleOptions = opts.cellules;
    this.serviceOptions = opts.services;
    this.orgTick.update((n) => n + 1);
  }

  patchFilterOperationalDepartment(dept: string): void {
    this.filterOperationalDepartment = dept;
    this.filterPole = '';
    this.filterCellule = '';
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyPerimeterChecklist();
  }

  patchFilterPole(pole: string): void {
    this.filterPole = pole;
    this.filterCellule = '';
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyPerimeterChecklist();
  }

  patchFilterCellule(cellule: string): void {
    this.filterCellule = cellule;
    this.filterService = '';
    this.refreshOrgFilterOptions();
    this.applyPerimeterChecklist();
  }

  patchFilterService(service: string): void {
    this.filterService = service;
    this.applyPerimeterChecklist();
  }

  clearOrgFilters(): void {
    this.filterOperationalDepartment = '';
    this.filterPole = '';
    this.filterCellule = '';
    this.filterService = '';
    this.checklistSearch = '';
    this.refreshOrgFilterOptions();
    this.perimeterChecklist.set([]);
    this.orgTick.update((n) => n + 1);
    this.searchTick.update((n) => n + 1);
  }

  private applyPerimeterChecklist(): void {
    const hasSelection = !!(
      this.filterOperationalDepartment ||
      this.filterPole ||
      this.filterCellule ||
      this.filterService
    );
    if (!hasSelection) {
      this.checklistSearch = '';
      this.perimeterChecklist.set([]);
      this.orgTick.update((n) => n + 1);
      this.searchTick.update((n) => n + 1);
      return;
    }

    const { visible } = filterEmployeePickerRows(
      this.employeeRows,
      {
        operationalDepartment: this.filterOperationalDepartment || undefined,
        pole: this.filterPole || undefined,
        cellule: this.filterCellule || undefined,
        service: this.filterService || undefined,
      },
      5000,
    );

    const already = this.beneficiaryGuidSet();
    this.checklistSearch = '';
    // Non cochés par défaut : l’utilisateur choisit qui ajouter, puis clique « Ajouter ».
    this.perimeterChecklist.set(
      visible.map((row) => {
        const g = resolveUserGuid(row.user);
        const alreadyIn = !!g && already.has(g);
        return { row, checked: alreadyIn };
      }),
    );
    this.orgTick.update((n) => n + 1);
    this.searchTick.update((n) => n + 1);
  }

  onChecklistSearchChange(value: string): void {
    this.checklistSearch = value;
    this.searchTick.update((n) => n + 1);
  }

  isAlreadyBeneficiary(row: EmployeePickerRow): boolean {
    const g = resolveUserGuid(row.user);
    return !!g && this.beneficiaryGuidSet().has(g);
  }

  toggleChecklist(guid: string, checked: boolean): void {
    if (this.beneficiaryGuidSet().has(guid)) return;
    this.perimeterChecklist.update((list) =>
      list.map((c) => (resolveUserGuid(c.row.user) === guid ? { ...c, checked } : c)),
    );
  }

  selectAllChecklist(checked: boolean): void {
    const already = this.beneficiaryGuidSet();
    const q = this.normalizeSearch(this.checklistSearch);
    if (!q) {
      this.perimeterChecklist.update((list) =>
        list.map((c) => {
          const g = resolveUserGuid(c.row.user);
          if (g && already.has(g)) return c;
          return { ...c, checked };
        }),
      );
      return;
    }
    const visibleGuids = new Set(
      this.visiblePerimeterChecklist()
        .map((c) => resolveUserGuid(c.row.user))
        .filter((g): g is string => !!g && !already.has(g)),
    );
    this.perimeterChecklist.update((list) =>
      list.map((c) => {
        const g = resolveUserGuid(c.row.user);
        return g && visibleGuids.has(g) ? { ...c, checked } : c;
      }),
    );
  }

  addCheckedToBeneficiaries(): void {
    const toAdd = this.checkedInPerimeter().map((c) => c.row);
    if (toAdd.length === 0) return;

    const existing = new Set(this.beneficiaryGuidSet());
    const merged = [...this.beneficiaryList()];
    for (const row of toAdd) {
      const g = resolveUserGuid(row.user);
      if (!g || existing.has(g)) continue;
      existing.add(g);
      merged.push(row);
    }
    this.beneficiaryList.set(merged);
    if (merged.length > this.form.capacity) {
      this.form.capacity = merged.length;
    }

    // Remet les cases du périmètre : déjà inscrits restent cochés/verrouillés.
    this.perimeterChecklist.update((list) =>
      list.map((c) => {
        const g = resolveUserGuid(c.row.user);
        const inList = !!g && existing.has(g);
        return { ...c, checked: inList };
      }),
    );
  }

  removeBeneficiary(row: EmployeePickerRow): void {
    const guid = resolveUserGuid(row.user);
    if (!guid) return;
    this.beneficiaryList.update((list) => list.filter((r) => resolveUserGuid(r.user) !== guid));
    this.perimeterChecklist.update((list) =>
      list.map((c) => (resolveUserGuid(c.row.user) === guid ? { ...c, checked: false } : c)),
    );
  }

  clearBeneficiaryList(): void {
    this.beneficiaryList.set([]);
    this.perimeterChecklist.update((list) => list.map((c) => ({ ...c, checked: false })));
  }

  onModeChange(mode: 'Single' | 'Multiple'): void {
    this.form.mode = mode;
    if (mode === 'Single') {
      this.form.sessionCount = 1;
      const first = this.form.sessionSlots[0] ?? createDefaultSlot(0);
      if (!first.plannedStart) {
        Object.assign(first, createDefaultSlot(0));
      } else {
        const end = parseLocalDateTime(first.plannedEnd);
        const start = parseLocalDateTime(first.plannedStart);
        if (!first.plannedEnd || !end || !start || end <= start) {
          first.plannedEnd = addHoursLocal(first.plannedStart, 1);
        }
      }
      this.form.sessionSlots = [first];
    } else {
      this.form.sessionCount = Math.max(2, this.form.sessionCount);
      this.syncSessionSlots();
    }
  }

  onSessionCountChange(count: number): void {
    this.form.sessionCount = Math.max(2, Math.min(20, Number(count) || 2));
    this.syncSessionSlots();
  }

  /** Début modifié → fin = début + 1 h si vide ou antérieure. */
  onSlotStartChange(index: number, value: string): void {
    const slot = this.form.sessionSlots[index];
    if (!slot) return;
    slot.plannedStart = value;
    if (!value) return;
    const end = parseLocalDateTime(slot.plannedEnd);
    const start = parseLocalDateTime(value);
    if (!slot.plannedEnd || !end || !start || end <= start) {
      slot.plannedEnd = addHoursLocal(value, 1);
    }
  }

  onSlotEndChange(index: number, value: string): void {
    const slot = this.form.sessionSlots[index];
    if (!slot) return;
    slot.plannedEnd = value;
  }

  slotDurationLabel(slot: SessionSlot): string {
    const start = parseLocalDateTime(slot.plannedStart);
    const end = parseLocalDateTime(slot.plannedEnd);
    if (!start || !end || end <= start) return 'Durée à définir';
    const mins = Math.round((end.getTime() - start.getTime()) / 60000);
    if (mins < 60) return `${mins} min`;
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return m ? `${h} h ${m} min` : `${h} h`;
  }

  private ensureDefaultSlots(): void {
    if (!this.form.sessionSlots.length) {
      this.form.sessionSlots = [createDefaultSlot(0)];
      return;
    }
    for (let i = 0; i < this.form.sessionSlots.length; i++) {
      const slot = this.form.sessionSlots[i];
      if (!slot.plannedStart) {
        const base = i === 0 ? createDefaultSlot(0) : createDefaultSlot(i, this.form.sessionSlots[0]?.plannedStart);
        slot.plannedStart = base.plannedStart;
        slot.plannedEnd = base.plannedEnd;
      } else if (!slot.plannedEnd) {
        slot.plannedEnd = addHoursLocal(slot.plannedStart, 1);
      }
    }
  }

  private syncSessionSlots(): void {
    const n = this.form.mode === 'Single' ? 1 : this.form.sessionCount;
    const next = [...this.form.sessionSlots];
    const anchor = next[0]?.plannedStart || createDefaultSlot(0).plannedStart;
    while (next.length < n) {
      next.push(createDefaultSlot(next.length, anchor));
    }
    this.form.sessionSlots = next.slice(0, n);
    this.ensureDefaultSlots();
  }

  onAnimatorSearchChange(value: string): void {
    this.animatorSearch = value;
    this.searchTick.update((n) => n + 1);
  }

  onAssignSearchChange(value: string): void {
    this.assignSearch = value;
    this.searchTick.update((n) => n + 1);
  }

  searchRevision(): number {
    return this.searchTick();
  }

  selectAnimator(row: EmployeePickerRow): void {
    const guid = resolveUserGuid(row.user);
    if (!guid) {
      this.error.set('GUID employé introuvable pour cet animateur.');
      return;
    }
    this.selectedAnimator.set(row);
    this.form.animatorUserId = guid;
    this.animatorSearch = '';
    this.searchTick.update((n) => n + 1);
    void this.loadAnimatorSessions(guid);
  }

  clearAnimator(): void {
    this.selectedAnimator.set(null);
    this.form.animatorUserId = '';
    this.animatorSessions.set([]);
  }

  onAnimatorKindChange(kind: 'Internal' | 'External'): void {
    this.form.animatorKind = kind;
    if (kind === 'External') {
      this.clearAnimator();
    } else {
      this.form.externalAnimatorName = '';
      this.form.externalAnimatorOrganization = '';
      this.form.externalAnimatorEmail = '';
      this.form.externalAnimatorPhone = '';
    }
  }

  addAssignEmployee(row: EmployeePickerRow): void {
    this.assignSelected.update((list) => [...list, row]);
    this.assignSearch = '';
    this.searchTick.update((n) => n + 1);
  }

  removeAssignEmployee(guid: string): void {
    this.assignSelected.update((list) =>
      list.filter((r) => resolveUserGuid(r.user) !== guid),
    );
  }

  private async loadAnimatorSessions(guid: string): Promise<void> {
    this.animatorSessionsLoading.set(true);
    try {
      this.animatorSessions.set(await this.api.listMyAnimatedSessions(guid));
    } catch {
      this.animatorSessions.set([]);
    } finally {
      this.animatorSessionsLoading.set(false);
    }
  }

  private async reload(): Promise<void> {
    try {
      this.sessions.set(await this.api.listSessions());
    } catch {
      this.sessions.set([]);
    }
  }

  private async loadOrgAndEmployees(): Promise<void> {
    try {
      const { users, departments, overview, subServices } = await firstValueFrom(
        forkJoin({
          users: this.usersApi.getAllUsers(),
          departments: this.http.get<Department[]>('/api/prime/departments').pipe(catchError(() => of([]))),
          overview: this.orgApi.loadOverview().pipe(catchError(() => of(null))),
          subServices: this.subServiceService.getAllSubServices().pipe(catchError(() => of([]))),
        }),
      );

      this.operationalDepartments = overview?.operationalDepartments ?? [];
      this.refreshOrgFilterOptions();

      const active = (users ?? []).filter((u) => u.isActive && !!resolveUserGuid(u));
      const perimeterById = new Map<number, UserOrgPerimeterView>();
      for (const u of active) {
        perimeterById.set(
          u.id,
          enrichUserOrgPerimeter(u, departments ?? [], overview, subServices ?? []),
        );
      }
      this.employeeRows = buildEmployeePickerRows(active, perimeterById);
      this.searchTick.update((n) => n + 1);
      this.applyPerimeterChecklist();
    } catch {
      this.employeeRows = [];
      this.operationalDepartments = [];
      this.refreshOrgFilterOptions();
    }
  }

  openAssign(sessionId: string): void {
    this.assignSessionId.set(sessionId);
    this.assignSelected.set([]);
    this.assignSearch = '';
    this.assignMsg.set(null);
    this.searchTick.update((n) => n + 1);
  }

  async confirmAssign(): Promise<void> {
    const sessionId = this.assignSessionId();
    if (!sessionId) return;
    const selected = this.assignSelected();
    if (selected.length === 0) {
      this.assignMsg.set('Sélectionnez au moins un bénéficiaire.');
      return;
    }
    this.busy.set(true);
    this.assignMsg.set(null);
    try {
      const session = this.sessions().find((s) => s.id === sessionId);
      const employees = selected.map((r) => ({
        employeeId: resolveUserGuid(r.user),
        employeeName: r.displayName,
      }));
      if (session?.programId) {
        await this.api.assignEmployeesToProgram(session.programId, employees);
      } else {
        await this.api.assignEmployees(sessionId, employees);
      }
      this.assignMsg.set(`${selected.length} bénéficiaire(s) affecté(s).`);
      this.assignSessionId.set(null);
      await this.reload();
    } catch (e) {
      this.assignMsg.set(e instanceof Error ? e.message : 'Échec de l’affectation');
    } finally {
      this.busy.set(false);
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Draft':
        return 'Brouillon';
      case 'Scheduled':
        return 'Planifiée';
      case 'InProgress':
        return 'En cours';
      case 'Completed':
        return 'Terminée';
      case 'Cancelled':
        return 'Annulée';
      default:
        return status;
    }
  }

  async publish(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      if (!this.form.title.trim()) {
        throw new Error('L’intitulé est obligatoire.');
      }
      this.syncSessionSlots();
      for (let i = 0; i < this.form.sessionSlots.length; i++) {
        const slot = this.form.sessionSlots[i];
        if (!slot.plannedStart || !slot.plannedEnd) {
          throw new Error(`Séance ${i + 1} : dates de début et de fin obligatoires.`);
        }
      }
      if (this.form.animatorKind === 'Internal' && !this.form.animatorUserId) {
        throw new Error('Sélectionnez un animateur interne.');
      }
      if (this.form.animatorKind === 'External') {
        if (!this.form.externalAnimatorName.trim() || !this.form.externalAnimatorEmail.trim()) {
          throw new Error('Nom et email de l’animateur externe sont obligatoires.');
        }
      }
      const beneficiaries = this.beneficiaryList();
      if (beneficiaries.length === 0) {
        throw new Error('Ajoutez au moins un bénéficiaire à la liste (un ou plusieurs périmètres).');
      }
      if (beneficiaries.length > this.form.capacity) {
        throw new Error(`Trop de bénéficiaires pour la capacité (${this.form.capacity}).`);
      }

      const created = await this.api.createProgram({
        title: this.form.title,
        description: this.form.description,
        mode: this.form.mode === 'Single' ? 0 : 1,
        sessionCount: this.form.mode === 'Single' ? 1 : this.form.sessionCount,
        capacity: this.form.capacity,
        sessions: this.form.sessionSlots.map((s) => ({
          plannedStart: toIsoDateTime(s.plannedStart),
          plannedEnd: toIsoDateTime(s.plannedEnd),
        })),
        animatorKind: this.form.animatorKind === 'Internal' ? 0 : 1,
        animatorUserId: this.form.animatorKind === 'Internal' ? this.form.animatorUserId : null,
        externalAnimatorName: this.form.externalAnimatorName,
        externalAnimatorOrganization: this.form.externalAnimatorOrganization,
        externalAnimatorEmail: this.form.externalAnimatorEmail,
        externalAnimatorPhone: this.form.externalAnimatorPhone,
        createdByUserId: 'planner-ui',
        publish: true,
      });

      if (beneficiaries.length > 0 && created?.id) {
        await this.api.assignEmployeesToProgram(
          created.id,
          beneficiaries.map((r) => ({
            employeeId: resolveUserGuid(r.user),
            employeeName: r.displayName,
          })),
        );
      }

      if (this.form.catalogItemId && created?.sessions?.length) {
        for (const session of created.sessions) {
          await this.api.linkSessionCatalog(session.id, {
            catalogItemId: this.form.catalogItemId,
            learningGateMode: this.form.learningGateMode || null,
            assignAudience: false,
          });
        }
      }

      this.resetForm();
      await this.reload();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Échec de la création');
    } finally {
      this.busy.set(false);
    }
  }

  private resetForm(): void {
    this.form = {
      title: '',
      description: '',
      capacity: 10,
      mode: 'Single',
      sessionCount: 1,
      sessionSlots: [createDefaultSlot(0)],
      animatorKind: 'Internal',
      animatorUserId: '',
      externalAnimatorName: '',
      externalAnimatorOrganization: '',
      externalAnimatorEmail: '',
      externalAnimatorPhone: '',
      catalogItemId: '',
      learningGateMode: '',
    };
    this.clearAnimator();
    this.beneficiaryList.set([]);
    this.clearOrgFilters();
    this.searchTick.update((n) => n + 1);
  }

  fillRate(s: TrainingSessionDto): number {
    return s.capacity > 0 ? Math.round((s.assignmentCount / s.capacity) * 100) : 0;
  }

  private normalizeSearch(value: string | null | undefined): string {
    return (value ?? '')
      .trim()
      .normalize('NFD')
      .replace(/\p{M}/gu, '')
      .toLowerCase();
  }

  formatSessionDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
  }

  userGuid(user: User): string {
    return resolveUserGuid(user);
  }
}

function toIsoDateTime(localValue: string): string {
  const raw = localValue?.trim();
  if (!raw) return raw;
  const d = new Date(raw.length === 16 ? `${raw}:00` : raw);
  if (Number.isNaN(d.getTime())) return raw;
  return d.toISOString();
}

type SessionSlot = { plannedStart: string; plannedEnd: string };

/** Valeur `datetime-local` (YYYY-MM-DDTHH:mm) pour maintenant, minutes arrondies à 0. */
function toLocalDateTimeValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function parseLocalDateTime(value: string | undefined | null): Date | null {
  if (!value?.trim()) return null;
  const d = new Date(value.length === 16 ? `${value}:00` : value);
  return Number.isNaN(d.getTime()) ? null : d;
}

function addHoursLocal(localValue: string, hours: number): string {
  const d = parseLocalDateTime(localValue) ?? new Date();
  d.setHours(d.getHours() + hours);
  return toLocalDateTimeValue(d);
}

/** Séance index 0 = aujourd’hui (heure courante) ; index n = +n jours. Fin = début + 1 h. */
function createDefaultSlot(index: number, anchorStart?: string): SessionSlot {
  const base = parseLocalDateTime(anchorStart) ?? new Date();
  base.setSeconds(0, 0);
  base.setMinutes(0);
  const start = new Date(base);
  start.setDate(start.getDate() + index);
  const end = new Date(start);
  end.setHours(end.getHours() + 1);
  return {
    plannedStart: toLocalDateTimeValue(start),
    plannedEnd: toLocalDateTimeValue(end),
  };
}
