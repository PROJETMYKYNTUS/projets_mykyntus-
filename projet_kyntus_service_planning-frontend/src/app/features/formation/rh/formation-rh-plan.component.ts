import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Search, X } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type {
  ProgramBeneficiaryProgressDto,
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
import { FormationCatalogOutlineComponent } from '../catalog/formation-catalog-outline.component';
import { FormationQuizQuestionsEditorComponent } from '../shared/formation-quiz-questions-editor.component';
import {
  buildQuizQuestionPayload,
  type QuizDraftQuestion,
} from '../shared/formation-quiz-draft.types';
import type { DraftModule } from '../catalog/formation-catalog-draft.types';
import {
  buildCatalogStructureRequest,
  countDraftLessons,
  uploadCatalogPendingFiles,
} from '../catalog/formation-catalog-structure.util';

@Component({
  selector: 'app-formation-rh-plan',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    KyntusPageHeaderComponent,
    LucideIconComponent,
    KyntusAudiencePickerComponent,
    FormationCatalogOutlineComponent,
    FormationQuizQuestionsEditorComponent,
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
  private readonly route = inject(ActivatedRoute);

  readonly icons = { search: Search, remove: X };
  readonly sessions = signal<TrainingSessionDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly assignSessionId = signal<string | null>(null);
  readonly assignMsg = signal<string | null>(null);
  readonly orgReady = signal(false);
  readonly editingProgramId = signal<string | null>(null);
  readonly progressByEmployeeId = signal<Record<string, ProgramBeneficiaryProgressDto>>({});
  readonly lockedEmployeeIds = computed(() =>
    Object.values(this.progressByEmployeeId())
      .filter((p) => p.isComplete)
      .map((p) => p.employeeId),
  );

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

  draftModules: DraftModule[] = [];
  quizQuestions: QuizDraftQuestion[] = [];

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
    contentSource: 'none' as 'none' | 'existing' | 'define',
    quizSource: 'none' as 'none' | 'existing' | 'define',
    quizTitle: '',
    quizPassThreshold: 70,
    quizAllowMultiple: false,
  };

  catalogItems: TrainingCatalogItemDto[] = [];
  quizTemplates: TrainingQuizTemplateListItemDto[] = [];

  ngOnInit(): void {
    const pid = this.route.snapshot.queryParamMap.get('programId');
    this.editingProgramId.set(pid || null);
    this.ensureDefaultSlots();
    void this.reload();
    void this.loadCatalogAndTemplates();
    void this.bootstrapOrg();
  }

  private async bootstrapOrg(): Promise<void> {
    await this.loadOrgAndEmployees();
    const pid = this.editingProgramId();
    if (pid) await this.loadExistingProgram(pid);
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

  private async loadExistingProgram(programId: string): Promise<void> {
    try {
      const detail = await this.api.getProgram(programId);
      this.form.title = detail.title;
      this.form.description = detail.description ?? '';
      this.form.capacity = detail.capacity;
      const modeNum = typeof detail.mode === 'number' ? detail.mode : detail.mode === 'Multiple' ? 1 : 0;
      this.form.mode = modeNum === 1 ? 'Multiple' : 'Single';
      this.form.sessionCount = detail.sessionCount || detail.sessions?.length || 1;
      this.form.sessionSlots = (detail.sessions ?? []).map((s) => ({
        plannedStart: toLocalDateTimeValue(new Date(s.plannedStart)),
        plannedEnd: toLocalDateTimeValue(new Date(s.plannedEnd)),
      }));
      if (!this.form.sessionSlots.length) this.form.sessionSlots = [createDefaultSlot(0)];
      const animNum =
        typeof detail.animatorKind === 'number' ? detail.animatorKind : detail.animatorKind === 'External' ? 1 : 0;
      this.form.animatorKind = animNum === 1 ? 'External' : 'Internal';
      this.form.animatorUserId = detail.animatorUserId ?? '';
      this.form.externalAnimatorName = detail.externalAnimatorName ?? '';
      this.form.externalAnimatorOrganization = detail.externalAnimatorOrganization ?? '';
      this.form.externalAnimatorEmail = detail.externalAnimatorEmail ?? '';
      this.form.externalAnimatorPhone = detail.externalAnimatorPhone ?? '';
      this.form.catalogItemId = detail.catalogItemId ?? '';
      this.form.quizTemplateId = detail.quizTemplateId ?? '';
      this.form.contentSource = this.form.catalogItemId ? 'existing' : 'none';
      this.form.quizSource = this.form.quizTemplateId ? 'existing' : 'none';
      const gate = parseLearningGate(detail.learningGateMode);
      this.form.learningGateMode = gate;

      if (this.form.animatorUserId) {
        const row = this.employeeRows.find((r) => resolveUserGuid(r.user) === this.form.animatorUserId);
        if (row) this.selectAnimator(row);
      }

      const map: Record<string, ProgramBeneficiaryProgressDto> = {};
      for (const p of detail.beneficiaries ?? []) {
        map[p.employeeId] = p;
        map[p.employeeId.toLowerCase()] = p;
      }
      this.progressByEmployeeId.set(map);

      const rows: EmployeePickerRow[] = [];
      for (const p of detail.beneficiaries ?? []) {
        const row = this.employeeRows.find((r) => resolveUserGuid(r.user).toLowerCase() === p.employeeId.toLowerCase());
        if (row) rows.push(row);
      }
      this.beneficiaryList.set(rows);
      this.searchTick.update((n) => n + 1);
      this.wizardStep.set(5);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Impossible de charger le programme');
    }
  }

  readonly wizardStep = signal(1);
  readonly wizardSteps = [
    { id: 1, label: 'Programme' },
    { id: 2, label: 'Contenu' },
    { id: 3, label: 'Quiz' },
    { id: 4, label: 'Présentiel' },
    { id: 5, label: 'Bénéficiaires' },
  ] as const;

  publishedQuizTemplates(): TrainingQuizTemplateListItemDto[] {
    return this.quizTemplates;
  }

  goToStep(step: number): void {
    if (step < 1 || step > 5) return;
    if (step > this.wizardStep()) {
      try {
        for (let i = this.wizardStep(); i < step; i++) this.validateStep(i);
      } catch (e) {
        this.error.set(e instanceof Error ? e.message : 'Complétez cette étape avant de continuer.');
        return;
      }
    }
    this.error.set(null);
    this.wizardStep.set(step);
  }

  nextStep(): void {
    try {
      this.validateStep(this.wizardStep());
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Étape incomplète.');
      return;
    }
    this.error.set(null);
    if (this.wizardStep() < 5) this.wizardStep.update((s) => s + 1);
  }

  prevStep(): void {
    this.error.set(null);
    if (this.wizardStep() > 1) this.wizardStep.update((s) => s - 1);
  }

  private validateStep(step: number): void {
    if (step === 1) {
      if (!this.form.title.trim()) throw new Error('L’intitulé est obligatoire.');
      if (this.form.capacity < 1) throw new Error('La capacité doit être au moins 1.');
      return;
    }
    if (this.editingProgramId() && step < 5) return;
    if (step === 2) {
      if (this.form.contentSource === 'existing' && !this.form.catalogItemId) {
        throw new Error('Sélectionnez un contenu publié.');
      }
      if (this.form.contentSource === 'define' && countDraftLessons(this.draftModules) < 1) {
        throw new Error('Ajoutez au moins une leçon au contenu.');
      }
      return;
    }
    if (step === 3) {
      if (this.form.quizSource === 'existing' && !this.form.quizTemplateId) {
        throw new Error('Sélectionnez un modèle de quiz publié.');
      }
      if (this.form.quizSource === 'define') {
        const questions = buildQuizQuestionPayload(this.quizQuestions);
        if (!questions.length) throw new Error('Ajoutez au moins une question au quiz.');
      }
      return;
    }
    if (step === 4) {
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
      return;
    }
    if (step === 5) {
      const beneficiaries = this.beneficiaryList();
      if (beneficiaries.length === 0) {
        throw new Error('Ajoutez au moins un bénéficiaire.');
      }
      if (beneficiaries.length > this.form.capacity) {
        throw new Error(`Trop de bénéficiaires pour la capacité (${this.form.capacity}).`);
      }
    }
  }

  onContentSourceChange(source: 'none' | 'existing' | 'define'): void {
    this.form.contentSource = source;
    if (source !== 'existing') this.form.catalogItemId = '';
    if (source === 'none' && (this.form.learningGateMode === 'Content' || this.form.learningGateMode === 'Both')) {
      this.form.learningGateMode = 'Attendance';
    }
  }

  onQuizSourceChange(source: 'none' | 'existing' | 'define'): void {
    this.form.quizSource = source;
    if (source !== 'existing') this.form.quizTemplateId = '';
    if (source === 'define' && !this.form.quizTitle.trim()) {
      this.form.quizTitle = this.form.title ? `Quiz — ${this.form.title}` : 'Quiz';
    }
  }

  onDraftModulesChange(next: DraftModule[]): void {
    this.draftModules = next;
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
    if (item?.defaultQuizTemplateId && this.form.quizSource === 'none') {
      const exists = this.quizTemplates.some((t) => t.id === item.defaultQuizTemplateId);
      if (exists) {
        this.form.quizSource = 'existing';
        this.form.quizTemplateId = item.defaultQuizTemplateId;
      }
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

  contentRecapLabel(): string {
    if (this.form.contentSource === 'existing' && this.form.catalogItemId) {
      return `existant : ${this.catalogTitle(this.form.catalogItemId)}`;
    }
    if (this.form.contentSource === 'define') {
      return `créé pour ce programme (${countDraftLessons(this.draftModules)} leçon(s))`;
    }
    return 'Aucun';
  }

  quizRecapLabel(): string {
    if (this.form.quizSource === 'existing' && this.form.quizTemplateId) {
      return `existant : ${this.quizTemplateTitle(this.form.quizTemplateId)}`;
    }
    if (this.form.quizSource === 'define') {
      return `créé pour ce programme (${this.quizQuestions.length} question(s))`;
    }
    return 'Aucun';
  }

  private validateForm(): void {
    for (let i = 1; i <= 5; i++) this.validateStep(i);
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
      const all = await this.api.listSessions();
      const pid = this.editingProgramId();
      this.sessions.set(pid ? all.filter((s) => s.programId === pid) : all);
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
      const pid = session?.programId || this.editingProgramId();
      if (pid) await this.loadExistingProgram(pid);
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
      this.validateForm();
      const beneficiaries = this.beneficiaryList();
      const employees = beneficiaries.map((r) => ({
        employeeId: resolveUserGuid(r.user),
        employeeName: r.displayName,
      }));

      const editId = this.editingProgramId();
      if (editId) {
        await this.api.assignEmployeesToProgram(editId, employees);
        const progress = await this.api.getProgramBeneficiaryProgress(editId);
        const map: Record<string, ProgramBeneficiaryProgressDto> = {};
        for (const p of progress) {
          map[p.employeeId] = p;
          map[p.employeeId.toLowerCase()] = p;
        }
        this.progressByEmployeeId.set(map);
        await this.reload();
        return;
      }

      let catalogItemId = this.form.contentSource === 'existing' ? this.form.catalogItemId : '';
      let quizTemplateId = this.form.quizSource === 'existing' ? this.form.quizTemplateId : '';

      if (this.form.contentSource === 'define') {
        const saved = await this.api.createCatalogItem({
          title: this.form.title.trim(),
          description: this.form.description.trim(),
          category: 'formation-continue',
          defaultGateMode: 1,
          audienceMatchMode: 0,
          selfServiceEnabled: false,
          dueMode: 0,
        });
        const structureBody = buildCatalogStructureRequest(this.draftModules);
        const structureResult = await this.api.replaceCatalogStructure(saved.id, structureBody);
        await uploadCatalogPendingFiles(this.draftModules, structureResult, (lessonId, file, title, type, sortOrder) =>
          this.api.uploadCatalogResource(lessonId, file, title, type, sortOrder),
        );
        await this.api.publishCatalogItem(saved.id);
        catalogItemId = saved.id;
      }

      if (this.form.quizSource === 'define') {
        const questions = buildQuizQuestionPayload(this.quizQuestions);
        const saved = await this.api.createQuizTemplate({
          title: (this.form.quizTitle || `Quiz — ${this.form.title}`).trim(),
          description: '',
          category: 'formation-continue',
          passThreshold: Math.min(100, Math.max(1, Number(this.form.quizPassThreshold) || 70)),
          allowMultipleAttempts: this.form.quizAllowMultiple,
          catalogItemId: catalogItemId || null,
          questions,
        });
        await this.api.publishQuizTemplate(saved.id);
        quizTemplateId = saved.id;
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

      if (!created?.id) {
        throw new Error('Programme créé sans identifiant.');
      }

      await this.api.assignEmployeesToProgram(created.id, employees);

      let sessionList = created.sessions ?? [];
      if (!sessionList.length) {
        const all = await this.api.listSessions();
        sessionList = all.filter((s) => s.programId === created.id);
      }

      if (catalogItemId && sessionList.length) {
        for (const session of sessionList) {
          await this.api.linkSessionCatalog(session.id, {
            catalogItemId,
            learningGateMode: this.form.learningGateMode || null,
            assignAudience: false,
          });
        }
      }

      if (quizTemplateId && sessionList.length) {
        for (const session of sessionList) {
          await this.api.instantiateQuizTemplate(quizTemplateId, {
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
      contentSource: 'none',
      quizSource: 'none',
      quizTitle: '',
      quizPassThreshold: 70,
      quizAllowMultiple: false,
    };
    this.draftModules = [];
    this.quizQuestions = [];
    this.clearAnimator();
    this.beneficiaryList.set([]);
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

function parseLearningGate(value: unknown): '' | 'Attendance' | 'Content' | 'Both' {
  const v = String(value ?? '');
  if (v === 'Attendance' || v === '0') return 'Attendance';
  if (v === 'Content' || v === '1') return 'Content';
  if (v === 'Both' || v === '2') return 'Both';
  return '';
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
