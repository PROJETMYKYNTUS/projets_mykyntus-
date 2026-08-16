import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Search, X } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type {
  TrainingCatalogItemDto,
  TrainingQuizTemplateListItemDto,
  TrainingSessionDto,
} from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
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
import {
  enrichUserOrgPerimeter,
  type UserOrgPerimeterView,
} from '../../../core/org/user-org-perimeter';
import {
  KyntusAudiencePickerComponent,
  type AudiencePickerSelection,
} from '../shared/kyntus-audience-picker.component';

type WizardStep = 1 | 2 | 3 | 'recap';

@Component({
  selector: 'app-formation-rh-plan',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    KyntusPageHeaderComponent,
    LucideIconComponent,
    KyntusAudiencePickerComponent,
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

  readonly icons = { search: Search, remove: X };
  readonly sessions = signal<TrainingSessionDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly assignSessionId = signal<string | null>(null);
  readonly assignMsg = signal<string | null>(null);
  readonly step = signal<WizardStep>(1);
  readonly orgReady = signal(false);

  employeeRows: EmployeePickerRow[] = [];
  operationalDepartments: OperationalDepartmentNode[] = [];

  animatorSearch = '';

  readonly selectedAnimator = signal<EmployeePickerRow | null>(null);
  readonly animatorSessions = signal<TrainingSessionDto[]>([]);
  readonly animatorSessionsLoading = signal(false);

  readonly beneficiaryList = signal<EmployeePickerRow[]>([]);
  readonly assignSelected = signal<EmployeePickerRow[]>([]);
  readonly assignPickerKey = signal(0);

  private readonly searchTick = signal(0);

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
    quizTemplateId: '',
    learningGateMode: '' as '' | 'Attendance' | 'Content' | 'Both',
  };

  catalogItems: TrainingCatalogItemDto[] = [];
  quizTemplates: TrainingQuizTemplateListItemDto[] = [];

  ngOnInit(): void {
    this.ensureDefaultSlots();
    void this.reload();
    void this.loadOrgAndEmployees();
    void this.loadCatalogAndTemplates();
  }

  async loadCatalogAndTemplates(): Promise<void> {
    try {
      const [items, templates] = await Promise.all([
        this.api.listCatalog(false),
        this.api.listQuizTemplates(false),
      ]);
      this.catalogItems = (items ?? []).filter((i) => i.status === 'Published' || i.status === 1);
      this.quizTemplates = (templates ?? []).filter((t) => t.status === 'Published' || t.status === 1);
    } catch {
      this.catalogItems = [];
      this.quizTemplates = [];
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
      const active = (users ?? []).filter((u) => u.isActive && !!resolveUserGuid(u));
      const perimeterById = new Map<number, UserOrgPerimeterView>();
      for (const u of active) {
        perimeterById.set(
          u.id,
          enrichUserOrgPerimeter(u, departments ?? [], overview, subServices ?? []),
        );
      }
      this.employeeRows = buildEmployeePickerRows(active, perimeterById);
      this.orgReady.set(true);
      this.searchTick.update((n) => n + 1);
    } catch {
      this.employeeRows = [];
      this.operationalDepartments = [];
      this.orgReady.set(true);
    }
  }

  publishedQuizTemplates(): TrainingQuizTemplateListItemDto[] {
    return this.quizTemplates;
  }

  onCatalogItemChange(id: string): void {
    this.form.catalogItemId = id;
    if (!id) {
      if (this.form.learningGateMode === 'Content' || this.form.learningGateMode === 'Both') {
        this.form.learningGateMode = 'Attendance';
      }
      return;
    }
    const item = this.catalogItems.find((c) => c.id === id);
    if (item?.defaultQuizTemplateId) {
      const exists = this.quizTemplates.some((t) => t.id === item.defaultQuizTemplateId);
      if (exists) this.form.quizTemplateId = item.defaultQuizTemplateId;
    }
  }

  onBeneficiariesChange(sel: AudiencePickerSelection): void {
    this.beneficiaryList.set([...sel.beneficiaries]);
    if (sel.beneficiaries.length > this.form.capacity) {
      this.form.capacity = sel.beneficiaries.length;
    }
  }

  onAssignBeneficiariesChange(sel: AudiencePickerSelection): void {
    this.assignSelected.set([...sel.beneficiaries]);
  }

  stepLabel(s: WizardStep): string {
    switch (s) {
      case 1:
        return 'Contenu';
      case 2:
        return 'Séances';
      case 3:
        return 'Bénéficiaires';
      case 'recap':
        return 'Récap';
    }
  }

  stepIndex(s: WizardStep | string | number): number {
    if (s === 1 || s === '1') return 1;
    if (s === 2 || s === '2') return 2;
    if (s === 3 || s === '3') return 3;
    return 4;
  }

  goNext(): void {
    this.error.set(null);
    try {
      const current = this.step();
      if (current === 1) {
        this.validateStep1();
        this.step.set(2);
      } else if (current === 2) {
        this.validateStep2();
        this.step.set(3);
      } else if (current === 3) {
        this.validateStep3();
        this.step.set('recap');
      }
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Étape invalide');
    }
  }

  goBack(): void {
    this.error.set(null);
    const current = this.step();
    if (current === 2) this.step.set(1);
    else if (current === 3) this.step.set(2);
    else if (current === 'recap') this.step.set(3);
  }

  private validateStep1(): void {
    if (!this.form.title.trim()) throw new Error('L’intitulé est obligatoire.');
    if (this.form.capacity < 1) throw new Error('La capacité doit être au moins 1.');
  }

  private validateStep2(): void {
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
  }

  private validateStep3(): void {
    const beneficiaries = this.beneficiaryList();
    if (beneficiaries.length === 0) {
      throw new Error('Ajoutez au moins un bénéficiaire.');
    }
    if (beneficiaries.length > this.form.capacity) {
      throw new Error(`Trop de bénéficiaires pour la capacité (${this.form.capacity}).`);
    }
  }

  catalogTitle(id: string): string {
    return this.catalogItems.find((c) => c.id === id)?.title ?? '—';
  }

  quizTemplateTitle(id: string): string {
    return this.quizTemplates.find((t) => t.id === id)?.title ?? '—';
  }

  gateModeLabel(mode: string): string {
    switch (mode) {
      case 'Attendance':
        return 'Être présent à la séance';
      case 'Content':
        return 'Avoir terminé le contenu e-learning';
      case 'Both':
        return 'Les deux (présence + contenu)';
      default:
        return 'Défaut catalogue';
    }
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

  openAssign(sessionId: string): void {
    this.assignSessionId.set(sessionId);
    this.assignSelected.set([]);
    this.assignMsg.set(null);
    this.assignPickerKey.update((n) => n + 1);
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
      this.validateStep1();
      this.validateStep2();
      this.validateStep3();

      const beneficiaries = this.beneficiaryList();

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

      if (!created?.id) {
        throw new Error('Programme créé sans identifiant.');
      }

      // Une seule affectation programme (pas via linkSessionCatalog.assignAudience).
      await this.api.assignEmployeesToProgram(
        created.id,
        beneficiaries.map((r) => ({
          employeeId: resolveUserGuid(r.user),
          employeeName: r.displayName,
        })),
      );

      let sessionList = created.sessions ?? [];
      if (!sessionList.length) {
        const all = await this.api.listSessions();
        sessionList = all.filter((s) => s.programId === created.id);
      }

      if (this.form.catalogItemId && sessionList.length) {
        for (const session of sessionList) {
          await this.api.linkSessionCatalog(session.id, {
            catalogItemId: this.form.catalogItemId,
            learningGateMode: this.form.learningGateMode || null,
            assignAudience: false,
          });
        }
      }

      if (this.form.quizTemplateId && sessionList.length) {
        for (const session of sessionList) {
          await this.api.instantiateQuizTemplate(this.form.quizTemplateId, {
            sessionId: session.id,
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
      quizTemplateId: '',
      learningGateMode: '',
    };
    this.clearAnimator();
    this.beneficiaryList.set([]);
    this.step.set(1);
    this.searchTick.update((n) => n + 1);
  }

  fillRate(s: TrainingSessionDto): number {
    return s.capacity > 0 ? Math.round((s.assignmentCount / s.capacity) * 100) : 0;
  }

  formatSessionDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
  }

  userGuid(user: User): string {
    return resolveUserGuid(user);
  }

  animatorLabel(): string {
    if (this.form.animatorKind === 'External') {
      return this.form.externalAnimatorName || 'Animateur externe';
    }
    return this.selectedAnimator()?.displayName || '—';
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
