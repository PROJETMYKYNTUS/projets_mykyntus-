import { Component, OnInit, OnDestroy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { KyntusFormDraftService } from '../../../../core/drafts/kyntus-form-draft.service';
import { KyntusObjectDraftBinder } from '../../../../core/drafts/kyntus-object-draft.binder';
import {
  PlanningService,
  ShiftConfigItem,
  SaveShiftConfigDto,
  WeekShiftConfigResponse,
  ShiftOption,
  ShiftConfigStatusItem,
} from '../../services/planning.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import type { SubService } from '../../../sub-services/sub-services-module';
import type { Department } from '../../../prime/models';
import type {
  OperationalDepartmentNode,
  OrgCelluleNode,
  OrgPoleNode,
  OrgServiceNode,
} from '../../../prime/models/org-tree.types';
import { findOperationalSelectionByServiceId } from '../../../../core/org/operational-org-picker';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import {
  ChevronDown,
  ChevronRight,
  Coffee,
  Info,
  Plus,
  Save,
  Settings,
  Trash2,
  Users,
} from 'lucide';

type ConfigStatus = 'ok' | 'missing' | 'partial' | 'none';

type SubServiceOption = {
  id: number;
  name: string;
  orgLabel: string;
  employeesCount: number;
  primeServiceId: string | null;
  hasTemplate: boolean;
};

@Component({
  selector: 'app-shift-config',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './shift-config.component.html',
  styleUrls: ['./shift-config.component.css'],
})
export class ShiftConfigComponent implements OnInit, OnDestroy {
  private readonly formDrafts = inject(KyntusFormDraftService);
  private readonly confirmService = inject(KyntusConfirmService);
  private draftBinder?: KyntusObjectDraftBinder<{
    subServiceId: number;
    isCriticalCell: boolean;
    minPresencePercent: number;
    shifts: ShiftConfigItem[];
  }>;

  readonly icons = {
    settings: Settings,
    coffee: Coffee,
    users: Users,
    save: Save,
    plus: Plus,
    trash: Trash2,
    info: Info,
    chevDown: ChevronDown,
    chevRight: ChevronRight,
  };

  subServiceId = 0;
  selectedServiceName = '';

  operationalDepartments: OperationalDepartmentNode[] = [];
  unassignedPoles: OrgPoleNode[] = [];
  legacyDepartments: Department[] = [];
  subServiceOptions: SubServiceOption[] = [];
  /** primeServiceId → SubServiceOption */
  private byPrimeId = new Map<string, SubServiceOption>();
  /** subServiceId → status */
  private statusBySubId = new Map<number, ShiftConfigStatusItem>();

  configuredCount = 0;
  totalServiceCount = 0;

  expandedDepts = new Set<string>();
  expandedPoles = new Set<string>();
  expandedCellules = new Set<string>();

  serviceEmployeeCount = 0;

  startOptions: ShiftOption[] = [];
  breakSlotOptions: ShiftOption[] = [];
  savedConfig: WeekShiftConfigResponse | null = null;
  loading = false;
  saving = false;
  error = '';
  successMsg = '';

  /** Extrêmes +3h/+5h si cellule critique ; normal = +4h/+4h30. Plafond +5h. */
  isCriticalCell = false;

  /** Présence min plateau de toute la cellule (0 = désactivée ; défaut 70). */
  minPresencePercent = 70;
  enforceMinPresence = true;

  shifts: ShiftConfigItem[] = [];

  constructor(
    private planningService: PlanningService,
    private subServiceService: SubServiceService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadStructure();
    this.startOptions = this.planningService.getShiftStartOptions();
    this.breakSlotOptions = this.planningService.getBreakSlotOptions();
    this.initShifts();
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'shift-config',
      () => ({
        subServiceId: this.subServiceId,
        isCriticalCell: this.isCriticalCell,
        minPresencePercent: this.minPresencePercent,
        shifts: this.shifts,
      }),
      (s) => {
        if (typeof s.subServiceId === 'number') this.subServiceId = s.subServiceId;
        if (typeof s.isCriticalCell === 'boolean') this.isCriticalCell = s.isCriticalCell;
        if (typeof s.minPresencePercent === 'number') this.minPresencePercent = s.minPresencePercent;
        if (Array.isArray(s.shifts) && s.shifts.length) this.shifts = s.shifts;
      },
    );
    this.draftBinder.start();
  }

  ngOnDestroy(): void {
    this.draftBinder?.destroy();
  }

  /** Persiste le brouillon après édition locale. */
  touchDraft(): void {
    this.draftBinder?.touch();
  }

  async resetDraftForm(): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Réinitialiser',
      message: 'Réinitialiser la configuration de shifts et le brouillon ?',
      confirmLabel: 'Réinitialiser',
    });
    if (!ok) return;
    this.draftBinder?.discard();
    this.error = '';
    this.successMsg = '';
    if (this.subServiceId > 0) {
      this.loadExistingConfig();
    } else {
      this.initShifts();
      this.isCriticalCell = false;
      this.enforceMinPresence = true;
      this.minPresencePercent = 70;
    }
    this.restartDraftBinder();
    this.cdr.detectChanges();
  }

  private restartDraftBinder(): void {
    this.draftBinder?.destroy();
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'shift-config',
      () => ({
        subServiceId: this.subServiceId,
        isCriticalCell: this.isCriticalCell,
        minPresencePercent: this.minPresencePercent,
        shifts: this.shifts,
      }),
      (s) => {
        if (typeof s.subServiceId === 'number') this.subServiceId = s.subServiceId;
        if (typeof s.isCriticalCell === 'boolean') this.isCriticalCell = s.isCriticalCell;
        if (typeof s.minPresencePercent === 'number') this.minPresencePercent = s.minPresencePercent;
        if (Array.isArray(s.shifts) && s.shifts.length) this.shifts = s.shifts;
      },
    );
    this.draftBinder.start();
  }

  initShifts(): void {
    this.shifts = [
      this.createShift('Shift 1', '08:00', 1),
      this.createShift('Shift 2', '09:00', 2),
      this.createShift('Shift 3', '10:00', 3),
      this.createShift('Shift 4', '11:00', 4),
    ];
  }

  createShift(label: string, startTime: string, order: number): ShiftConfigItem {
    return {
      label,
      startTime,
      workHours: 8,
      breakDurationMinutes: 60,
      breakSlots: this.buildAutoBreakSlots(startTime, this.isCriticalCell),
      requiredCount: 0,
      displayOrder: order,
    };
  }

  private parseTimeToMinutes(time: string): number | null {
    if (!time) return null;
    const [h, m] = time.split(':').map(Number);
    if (Number.isNaN(h) || Number.isNaN(m)) return null;
    return h * 60 + m;
  }

  private formatMinutes(min: number): string {
    const h = Math.floor(min / 60);
    const m = min % 60;
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
  }

  /**
   * Défauts relatifs au start, même priorité backend (sans saut) :
   * +4h → +4h30 → (+3h30 si critique).
   */
  buildAutoBreakSlots(startTime: string, isCritical: boolean): string[] {
    const startMin = this.parseTimeToMinutes(startTime);
    if (startMin == null) return [];
    if (isCritical) {
      return [
        this.formatMinutes(startMin + 4 * 60),
        this.formatMinutes(startMin + 4.5 * 60),
        this.formatMinutes(startMin + 3.5 * 60),
      ];
    }
    return [
      this.formatMinutes(startMin + 4 * 60),
      this.formatMinutes(startMin + 4.5 * 60),
    ];
  }

  /** Créneaux autorisés UI : fenêtre progressive (+4 → … → extrêmes selon criticité). */
  getAllowedBreakStarts(shift: ShiftConfigItem): string[] {
    const startMin = this.parseTimeToMinutes(shift.startTime);
    if (startMin == null) return [];
    const early = Math.floor(startMin / 60) < 10;
    const offsets = this.isCriticalCell
      ? (early
          ? [4, 4.5, 3.5, 5, 3] // Opening : max +5h
          : [4, 4.5, 3.5, 3, 5])
      : [4, 4.5];
    return offsets.map(h => this.formatMinutes(startMin + h * 60));
  }

  getBreakEndLabel(start: string, durationMinutes = 60): string {
    const startMin = this.parseTimeToMinutes(start);
    if (startMin == null) return '';
    return this.formatMinutes(startMin + (durationMinutes > 0 ? durationMinutes : 60));
  }

  formatBreakSlotLabel(start: string, durationMinutes = 60): string {
    const end = this.getBreakEndLabel(start, durationMinutes);
    return end ? `${start} → ${end}` : start;
  }

  onStartTimeChange(shift: ShiftConfigItem): void {
    shift.breakSlots = this.buildAutoBreakSlots(shift.startTime, this.isCriticalCell);
    shift.breakDurationMinutes = 60;
  }

  onCriticalCellChange(): void {
    for (const s of this.shifts) {
      s.breakSlots = this.buildAutoBreakSlots(s.startTime, this.isCriticalCell);
      s.breakDurationMinutes = 60;
    }
    this.touchDraft();
  }

  onEnforceMinPresenceChange(enabled: boolean): void {
    this.enforceMinPresence = enabled;
    if (enabled && (this.minPresencePercent == null || this.minPresencePercent < 50)) {
      this.minPresencePercent = 70;
    }
    this.touchDraft();
  }

  isBreakSlotSelected(shift: ShiftConfigItem, slot: string): boolean {
    return (shift.breakSlots ?? []).includes(slot);
  }

  toggleBreakSlot(shift: ShiftConfigItem, slot: string): void {
    const current = [...(shift.breakSlots ?? [])];
    const idx = current.indexOf(slot);
    if (idx >= 0) {
      if (current.length <= 1) return; // au moins 1 créneau
      current.splice(idx, 1);
    } else {
      if (current.length >= 3) return;
      current.push(slot);
      current.sort();
    }
    shift.breakSlots = current;
  }

  resetBreakSlotsAuto(shift: ShiftConfigItem): void {
    shift.breakSlots = this.buildAutoBreakSlots(shift.startTime, this.isCriticalCell);
  }

  loadStructure(): void {
    forkJoin({
      subServices: this.subServiceService.getAllSubServices(),
      departments: this.http.get<Department[]>('/api/prime/departments'),
      overview: this.orgApi.loadOverview(),
      status: this.planningService.getShiftConfigStatus(),
    }).subscribe({
      next: ({ subServices, departments, overview, status }) => {
        this.operationalDepartments = overview.operationalDepartments ?? [];
        this.unassignedPoles = overview.unassignedPoles ?? [];
        this.legacyDepartments = departments?.length ? departments : (overview.departments ?? []);

        this.statusBySubId.clear();
        for (const item of status.items ?? []) {
          this.statusBySubId.set(item.subServiceId, item);
        }
        this.subServiceOptions = (subServices ?? []).map((s) => this.toOption(s));
        this.byPrimeId.clear();
        for (const opt of this.subServiceOptions) {
          if (opt.primeServiceId) this.byPrimeId.set(opt.primeServiceId, opt);
        }

        // Avancement = uniquement les services rattachés à la structure orga
        const inTree = this.subServiceOptions.filter((opt) => this.isLinkedToTree(opt));
        this.totalServiceCount = inTree.length;
        this.configuredCount = inTree.filter((opt) => opt.hasTemplate).length;

        this.expandAllWithMissing();

        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Impossible de charger la structure et les services.';
        this.cdr.detectChanges();
      },
    });
  }

  private toOption(s: SubService): SubServiceOption {
    const st = this.statusBySubId.get(s.id);
    return {
      id: s.id,
      name: s.name,
      orgLabel: s.name,
      employeesCount: st?.activeEmployeeCount ?? s.employeesCount ?? 0,
      primeServiceId: s.primeServiceId?.trim() || null,
      hasTemplate: st?.hasTemplate ?? false,
    };
  }

  private isLinkedToTree(opt: SubServiceOption): boolean {
    if (!opt.primeServiceId) return false;
    return !!findOperationalSelectionByServiceId(
      this.operationalDepartments,
      this.unassignedPoles,
      opt.primeServiceId,
    );
  }

  /** Ouvre les branches qui contiennent au moins un service non configuré. */
  private expandAllWithMissing(): void {
    this.expandedDepts.clear();
    this.expandedPoles.clear();
    this.expandedCellules.clear();

    const markMissing = (primeId: string) => {
      const opt = this.byPrimeId.get(primeId);
      return opt && !opt.hasTemplate;
    };

    for (const md of this.operationalDepartments) {
      let deptOpen = false;
      for (const pole of md.poles) {
        let poleOpen = false;
        for (const cell of pole.cellules) {
          let cellOpen = false;
          for (const svc of cell.services) {
            if (markMissing(svc.id)) {
              cellOpen = true;
              poleOpen = true;
              deptOpen = true;
            }
          }
          if (cellOpen) this.expandedCellules.add(cell.id);
        }
        if (poleOpen) this.expandedPoles.add(pole.id);
      }
      if (deptOpen) this.expandedDepts.add(md.id);
    }

    for (const pole of this.unassignedPoles) {
      let poleOpen = false;
      for (const cell of pole.cellules) {
        let cellOpen = false;
        for (const svc of cell.services) {
          if (markMissing(svc.id)) {
            cellOpen = true;
            poleOpen = true;
          }
        }
        if (cellOpen) this.expandedCellules.add(cell.id);
      }
      if (poleOpen) this.expandedPoles.add(pole.id);
    }

    // Si tout est OK, ouvrir le premier département pour la vision d’ensemble
    if (this.expandedDepts.size === 0 && this.operationalDepartments.length > 0) {
      const md = this.operationalDepartments[0];
      this.expandedDepts.add(md.id);
      if (md.poles[0]) {
        this.expandedPoles.add(md.poles[0].id);
        if (md.poles[0].cellules[0]) {
          this.expandedCellules.add(md.poles[0].cellules[0].id);
        }
      }
    }
  }

  refreshStatusAfterSave(): void {
    this.planningService.getShiftConfigStatus().subscribe({
      next: (status) => {
        this.statusBySubId.clear();
        for (const item of status.items ?? []) {
          this.statusBySubId.set(item.subServiceId, item);
        }
        for (const opt of this.subServiceOptions) {
          opt.hasTemplate = this.statusBySubId.get(opt.id)?.hasTemplate ?? false;
          opt.employeesCount =
            this.statusBySubId.get(opt.id)?.activeEmployeeCount ?? opt.employeesCount;
        }
        const inTree = this.subServiceOptions.filter((opt) => this.isLinkedToTree(opt));
        this.totalServiceCount = inTree.length;
        this.configuredCount = inTree.filter((opt) => opt.hasTemplate).length;
        this.cdr.detectChanges();
      },
    });
  }

  // ── Tree expand / status ───────────────────────────

  toggleDept(id: string, event?: Event): void {
    event?.stopPropagation();
    if (this.expandedDepts.has(id)) this.expandedDepts.delete(id);
    else this.expandedDepts.add(id);
  }

  togglePole(id: string, event?: Event): void {
    event?.stopPropagation();
    if (this.expandedPoles.has(id)) this.expandedPoles.delete(id);
    else this.expandedPoles.add(id);
  }

  toggleCellule(id: string, event?: Event): void {
    event?.stopPropagation();
    if (this.expandedCellules.has(id)) this.expandedCellules.delete(id);
    else this.expandedCellules.add(id);
  }

  deptExpanded(id: string): boolean {
    return this.expandedDepts.has(id);
  }
  poleExpanded(id: string): boolean {
    return this.expandedPoles.has(id);
  }
  celluleExpanded(id: string): boolean {
    return this.expandedCellules.has(id);
  }

  optionForPrime(primeId: string): SubServiceOption | undefined {
    return this.byPrimeId.get(primeId);
  }

  statusForPrime(primeId: string): ConfigStatus {
    const opt = this.byPrimeId.get(primeId);
    if (!opt) return 'none';
    return opt.hasTemplate ? 'ok' : 'missing';
  }

  statusForServices(services: OrgServiceNode[]): ConfigStatus {
    const linked = services
      .map((s) => this.byPrimeId.get(s.id))
      .filter((o): o is SubServiceOption => !!o);
    if (linked.length === 0) return 'none';
    const ok = linked.filter((o) => o.hasTemplate).length;
    if (ok === linked.length) return 'ok';
    if (ok === 0) return 'missing';
    return 'partial';
  }

  statusForCellule(cell: OrgCelluleNode): ConfigStatus {
    return this.statusForServices(cell.services);
  }

  statusForPole(pole: OrgPoleNode): ConfigStatus {
    const all = pole.cellules.flatMap((c) => c.services);
    return this.statusForServices(all);
  }

  statusForDept(md: OperationalDepartmentNode): ConfigStatus {
    const all = md.poles.flatMap((p) => p.cellules.flatMap((c) => c.services));
    return this.statusForServices(all);
  }

  statusTitle(status: ConfigStatus): string {
    switch (status) {
      case 'ok':
        return 'Modèle shift configuré';
      case 'missing':
        return 'Modèle shift manquant';
      case 'partial':
        return 'Configuration partielle';
      default:
        return 'Aucun service planification lié';
    }
  }

  selectOrgService(svc: OrgServiceNode): void {
    const opt = this.byPrimeId.get(svc.id);
    if (!opt) {
      this.error = `« ${svc.name} » n’est pas lié à un service planification.`;
      return;
    }
    this.selectSubService(opt);
  }

  selectSubService(opt: SubServiceOption): void {
    this.error = '';
    this.successMsg = '';
    this.subServiceId = opt.id;
    this.selectedServiceName = opt.name;
    this.serviceEmployeeCount = opt.employeesCount;
    this.savedConfig = null;
    this.touchDraft();
    this.loadExistingConfig();
  }

  isSelected(subServiceId: number): boolean {
    return this.subServiceId === subServiceId;
  }

  isOrgServiceSelected(primeId: string): boolean {
    const opt = this.byPrimeId.get(primeId);
    return !!opt && this.subServiceId === opt.id;
  }

  // ── Shifts table (inchangé) ────────────────────────

  addShift(): void {
    if (this.shifts.length >= 8) return;
    this.shifts.push(
      this.createShift(`Shift ${this.shifts.length + 1}`, '08:00', this.shifts.length + 1),
    );
    this.touchDraft();
  }

  removeShift(index: number): void {
    if (this.shifts.length <= 1) return;
    this.shifts.splice(index, 1);
    this.shifts.forEach((s, i) => (s.displayOrder = i + 1));
    this.touchDraft();
  }

  getEndTime(shift: ShiftConfigItem): string {
    return this.planningService.calculateEndTime(shift.startTime, shift.workHours);
  }

  getBreakRangeAuto(shift: ShiftConfigItem): string {
    const slots = shift.breakSlots?.length
      ? shift.breakSlots
      : this.buildAutoBreakSlots(shift.startTime, this.isCriticalCell);
    if (!slots.length) return '';
    return slots
      .map((s) => this.formatBreakSlotLabel(s, shift.breakDurationMinutes || 60))
      .join(' · ');
  }

  get totalEffectif(): number {
    return this.shifts.reduce((sum, s) => sum + (s.requiredCount || 0), 0);
  }

  get totalPercentageLabel(): string {
    return this.totalEffectif === 0 ? '0%' : '100%';
  }

  get quotasMatchEffectif(): boolean {
    return this.serviceEmployeeCount <= 0 || this.totalEffectif === this.serviceEmployeeCount;
  }

  getPercentage(shift: ShiftConfigItem): number {
    if (this.totalEffectif === 0) return 0;
    return Math.round((shift.requiredCount / this.totalEffectif) * 100);
  }

  onRequiredCountChange(): void {
    if (this.serviceEmployeeCount > 0 && this.totalEffectif > this.serviceEmployeeCount) {
      this.error = `Le total (${this.totalEffectif}) dépasse l'effectif du service (${this.serviceEmployeeCount}).`;
    } else if (
      this.serviceEmployeeCount > 0 &&
      this.totalEffectif < this.serviceEmployeeCount &&
      this.totalEffectif > 0
    ) {
      this.error = `Le total (${this.totalEffectif}) doit égaler l'effectif actif (${this.serviceEmployeeCount}). Utilisez « Répartir ».`;
    } else if (
      this.error.includes("dépasse l'effectif") ||
      this.error.includes("doit égaler l'effectif")
    ) {
      this.error = '';
    }
  }

  distributeEvenly(): void {
    if (this.serviceEmployeeCount <= 0 || this.shifts.length === 0) {
      this.error = 'Aucun effectif actif à répartir pour ce service.';
      return;
    }
    const n = this.shifts.length;
    const base = Math.floor(this.serviceEmployeeCount / n);
    let rest = this.serviceEmployeeCount % n;
    this.shifts.forEach((s) => {
      s.requiredCount = base + (rest > 0 ? 1 : 0);
      if (rest > 0) rest--;
    });
    this.error = '';
    this.successMsg = `Effectif réparti : ${this.serviceEmployeeCount} sur ${n} shift(s).`;
    this.cdr.detectChanges();
  }

  saveConfig(): void {
    if (!this.subServiceId) {
      this.error = 'Veuillez sélectionner un service dans l’arbre.';
      return;
    }
    if (this.totalEffectif === 0) {
      this.error = "Veuillez définir le nombre d'employés pour chaque shift.";
      return;
    }
    if (this.serviceEmployeeCount > 0 && this.totalEffectif !== this.serviceEmployeeCount) {
      this.error = `La somme des NB employés (${this.totalEffectif}) doit égaler l'effectif actif (${this.serviceEmployeeCount}).`;
      return;
    }
    for (const s of this.shifts) {
      if (!(s.breakSlots?.length)) {
        this.error = `Au moins un créneau de pause est requis pour « ${s.label} ».`;
        return;
      }
    }
    const p = this.minPresencePercent ?? 70;
    if (this.enforceMinPresence && (p < 50 || p > 100)) {
      this.error = 'Présence min cellule invalide (50–100 %).';
      return;
    }

    this.saving = true;
    this.error = '';
    this.successMsg = '';

    const dto: SaveShiftConfigDto = {
      subServiceId: this.subServiceId,
      weekCode: null,
      weekStartDate: null,
      isCriticalCell: this.isCriticalCell,
      minPresencePercent: this.enforceMinPresence ? this.minPresencePercent : 0,
      shifts: this.shifts.map((s) => ({
        ...s,
        breakDurationMinutes: 60,
        breakSlots: (s.breakSlots?.length
          ? s.breakSlots
          : this.buildAutoBreakSlots(s.startTime, this.isCriticalCell)
        ).slice(0, 3),
      })),
    };

    this.planningService.saveShiftConfig(dto).subscribe({
      next: (result) => {
        this.savedConfig = result;
        this.saving = false;
        this.draftBinder?.clear();
        this.successMsg = `Modèle sauvegardé — ${result.totalEffectif} employés sur ${result.shifts.length} shifts (toutes les semaines)`;
        const opt = this.subServiceOptions.find((s) => s.id === this.subServiceId);
        if (opt) opt.hasTemplate = true;
        this.refreshStatusAfterSave();
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.saving = false;
        this.error = `Erreur : ${err.error?.message ?? 'Erreur serveur'}`;
        this.cdr.detectChanges();
      },
    });
  }

  loadExistingConfig(): void {
    if (!this.subServiceId) return;
    this.loading = true;

    this.planningService.getShiftTemplate(this.subServiceId).subscribe({
      next: (config) => {
        this.savedConfig = config;
        this.isCriticalCell = !!config.isCriticalCell;
        const rawPresence =
          typeof config.minPresencePercent === 'number'
            ? config.minPresencePercent
            : (config.shifts[0]?.minPresencePercent ?? 70);
        if (rawPresence <= 0) {
          this.enforceMinPresence = false;
          this.minPresencePercent = 70;
        } else {
          this.enforceMinPresence = true;
          this.minPresencePercent = rawPresence;
        }
        this.shifts = config.shifts.map((s) => {
          const slots =
            s.breakSlots?.length
              ? [...s.breakSlots]
              : this.buildAutoBreakSlots(s.startTime, this.isCriticalCell);
          return {
            label: s.label,
            startTime: s.startTime,
            workHours: s.workHours,
            breakDurationMinutes: s.breakDurationMinutes || 60,
            breakSlots: slots,
            requiredCount: s.requiredCount,
            displayOrder: s.displayOrder,
          };
        });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.savedConfig = null;
        this.isCriticalCell = false;
        this.enforceMinPresence = true;
        this.minPresencePercent = 70;
        this.initShifts();
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }
}
