import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  ArrowRight,
  Calendar,
  CheckCircle,
  ClipboardList,
  GraduationCap,
  Inbox,
  Loader2,
  Search,
  Users,
} from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  INITIAL_TRAINING_ACTIVE_STATUSES,
  INITIAL_TRAINING_STATUS_LABELS,
  TRAINING_SESSION_STATUS_LABELS,
  type InitialTrainingPathDto,
  type InitialTrainingStatus,
  type TrainingSessionDto,
  type TrainingSessionStatus,
} from '../../../core/models/formation-training.models';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { UserService } from '../../users/services/user.service';
import { resolveUserGuid } from '../../../core/lib/user-guid.util';

type AdminTab = 'initial' | 'continue';

@Component({
  selector: 'app-formation-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './formation-admin.component.html',
  styleUrls: ['./formation-admin.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationAdminComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly session = inject(KyntusSessionService);
  private readonly usersApi = inject(UserService);

  readonly icons = {
    graduation: GraduationCap,
    search: Search,
    inbox: Inbox,
    loader: Loader2,
    users: Users,
    calendar: Calendar,
    validate: CheckCircle,
    clipboard: ClipboardList,
    arrow: ArrowRight,
  };

  readonly statusLabels = INITIAL_TRAINING_STATUS_LABELS;
  readonly sessionStatusLabels = TRAINING_SESSION_STATUS_LABELS;
  readonly initialStatuses: InitialTrainingStatus[] = [
    'EnCours',
    'QuizASaisir',
    'AttenteValidationFormateur',
    'AttenteValidationRh',
    'EnProduction',
    'Rejete',
  ];
  readonly sessionStatuses: TrainingSessionStatus[] = [
    'Draft',
    'Scheduled',
    'InProgress',
    'Completed',
    'Cancelled',
  ];

  paths: InitialTrainingPathDto[] = [];
  sessions: TrainingSessionDto[] = [];
  filteredPaths: InitialTrainingPathDto[] = [];
  filteredSessions: TrainingSessionDto[] = [];

  loading = false;
  tab: AdminTab = 'initial';
  searchTerm = '';
  filterInitialStatut: '' | InitialTrainingStatus = '';
  filterSessionStatut: '' | TrainingSessionStatus = '';

  panelPathId: string | null = null;
  panelMode: 'docs' | 'extend' | 'reject' | null = null;
  panelBusy = false;
  extendDate = '';
  rejectReason = '';
  feedback = '';
  feedbackKind: 'info' | 'error' = 'info';

  ngOnInit(): void {
    const tabParam = this.route.snapshot.queryParamMap.get('tab');
    if (tabParam === 'continue' || tabParam === 'initial') {
      this.tab = tabParam;
    }

    const statutParam = this.route.snapshot.queryParamMap.get('statut');
    if (statutParam) {
      if (this.isInitialStatus(statutParam)) {
        this.tab = 'initial';
        this.filterInitialStatut = statutParam;
      } else if (this.isSessionStatus(statutParam)) {
        this.tab = 'continue';
        this.filterSessionStatut = statutParam;
      } else if (statutParam === 'EnAttente' || statutParam === 'pending') {
        // Ancien filtre catalogue → file RH production
        this.tab = 'initial';
        this.filterInitialStatut = 'AttenteValidationRh';
      }
    }

    void this.load();
  }

  get stats() {
    const active = this.paths.filter((p) =>
      INITIAL_TRAINING_ACTIVE_STATUSES.includes(p.status),
    );
    return {
      activeInitial: active.length,
      quizOrFormateur: this.paths.filter(
        (p) =>
          p.status === 'QuizASaisir' ||
          p.status === 'AttenteValidationFormateur' ||
          p.status === 'EnCours',
      ).length,
      rhPending: this.paths.filter((p) => p.status === 'AttenteValidationRh').length,
      sessionsOpen: this.sessions.filter(
        (s) => s.status === 'Scheduled' || s.status === 'InProgress' || s.status === 'Draft',
      ).length,
    };
  }

  async load(): Promise<void> {
    this.loading = true;
    this.cdr.markForCheck();
    try {
      const [paths, sessions] = await Promise.all([
        this.api.listInitialOverview(),
        this.api.listSessions(),
      ]);
      this.paths = paths ?? [];
      this.sessions = sessions ?? [];
      this.applyFilters();
    } catch {
      this.paths = [];
      this.sessions = [];
      this.applyFilters();
    } finally {
      this.loading = false;
      this.cdr.markForCheck();
    }
  }

  setTab(tab: AdminTab): void {
    this.tab = tab;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab, statut: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.cdr.markForCheck();
  }

  applyFilters(): void {
    const q = this.searchTerm.trim().toLowerCase();

    let paths = [...this.paths];
    if (q) {
      paths = paths.filter(
        (p) =>
          p.employeeName.toLowerCase().includes(q) ||
          p.employeeId.toLowerCase().includes(q),
      );
    }
    if (this.filterInitialStatut) {
      paths = paths.filter((p) => p.status === this.filterInitialStatut);
    }
    this.filteredPaths = paths;

    let sessions = [...this.sessions];
    if (q) {
      sessions = sessions.filter(
        (s) =>
          s.title.toLowerCase().includes(q) ||
          s.description.toLowerCase().includes(q) ||
          (s.externalAnimatorName ?? '').toLowerCase().includes(q),
      );
    }
    if (this.filterSessionStatut) {
      sessions = sessions.filter((s) => s.status === this.filterSessionStatut);
    }
    this.filteredSessions = sessions;
    this.cdr.markForCheck();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.filterInitialStatut = '';
    this.filterSessionStatut = '';
    this.applyFilters();
  }

  getInitialStatusClass(status: InitialTrainingStatus): string {
    const map: Record<InitialTrainingStatus, string> = {
      EnCours: 'badge-active',
      QuizASaisir: 'badge-pending',
      AttenteValidationFormateur: 'badge-pending',
      AttenteValidationRh: 'badge-rh',
      EnProduction: 'badge-valid',
      Rejete: 'badge-cancel',
    };
    return map[status] ?? '';
  }

  getSessionStatusClass(status: TrainingSessionStatus): string {
    const map: Record<TrainingSessionStatus, string> = {
      Draft: 'badge-draft',
      Scheduled: 'badge-valid',
      InProgress: 'badge-active',
      Completed: 'badge-done',
      Cancelled: 'badge-cancel',
    };
    return map[status] ?? '';
  }

  animatorLabel(s: TrainingSessionDto): string {
    if (s.animatorKind === 'External') {
      return s.externalAnimatorName?.trim() || 'Animateur externe';
    }
    return s.animatorUserId ? 'Animateur interne' : '—';
  }

  isPeriodEnded(path: InitialTrainingPathDto): boolean {
    // Fenêtre ouverte dès J-7 avant DateFinPrevue.
    if (path.daysUntilEnd != null) return path.daysUntilEnd <= 7;
    const end = new Date(path.dateFinPrevue);
    if (Number.isNaN(end.getTime())) return false;
    const openFrom = new Date(end);
    openFrom.setHours(0, 0, 0, 0);
    openFrom.setDate(openFrom.getDate() - 7);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return today.getTime() >= openFrom.getTime();
  }

  /** RH Valider : formateur a validé + fenêtre J-7 ouverte. */
  canRhValidate(path: InitialTrainingPathDto): boolean {
    return path.status === 'AttenteValidationRh' && this.isPeriodEnded(path);
  }

  rhValidateHint(path: InitialTrainingPathDto): string {
    if (path.status !== 'AttenteValidationRh') {
      return 'En attente de la validation formateur';
    }
    if (!this.isPeriodEnded(path)) {
      return 'Disponible à partir de J-7 avant la fin prévue (ou prolonger)';
    }
    return 'Ouvrir la fiche employé pour confirmer le passage en production';
  }

  async rhValidateAction(path: InitialTrainingPathDto): Promise<void> {
    if (!this.canRhValidate(path)) return;

    const total = path.documentsTotalCount ?? 0;
    const received = path.documentsReceivedCount ?? 0;
    if (total > 0 && received < total) {
      this.openPanel(path, 'docs');
      return;
    }

    await this.confirmNavigateToEmployee(path);
  }

  isPanelOpen(pathId: string): boolean {
    return this.panelPathId === pathId && this.panelMode != null;
  }

  closePanel(): void {
    this.panelPathId = null;
    this.panelMode = null;
    this.panelBusy = false;
    this.extendDate = '';
    this.rejectReason = '';
    this.feedback = '';
    this.cdr.markForCheck();
  }

  openPanel(path: InitialTrainingPathDto, mode: 'docs' | 'extend' | 'reject'): void {
    this.panelPathId = path.id;
    this.panelMode = mode;
    this.panelBusy = false;
    this.feedback = '';
    this.extendDate = mode === 'extend' ? this.nextDayIso(path.dateFinPrevue) : '';
    this.rejectReason = '';
    this.cdr.markForCheck();
  }

  openReject(path: InitialTrainingPathDto): void {
    this.openPanel(path, 'reject');
  }

  openExtend(path: InitialTrainingPathDto): void {
    this.openPanel(path, 'extend');
  }

  minExtendDate(path: InitialTrainingPathDto): string {
    return this.nextDayIso(path.dateFinPrevue);
  }

  canConfirmExtend(path: InitialTrainingPathDto): boolean {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(this.extendDate)) return false;
    const chosen = new Date(`${this.extendDate}T00:00:00`);
    if (Number.isNaN(chosen.getTime())) return false;
    const end = new Date(path.dateFinPrevue);
    end.setHours(0, 0, 0, 0);
    return chosen.getTime() > end.getTime();
  }

  canConfirmReject(): boolean {
    return this.rejectReason.trim().length >= 3;
  }

  async confirmNavigateToEmployee(path: InitialTrainingPathDto): Promise<void> {
    this.panelBusy = true;
    this.feedback = '';
    this.cdr.markForCheck();
    try {
      const users = await firstValueFrom(this.usersApi.getAllUsers());
      const user = (users ?? []).find((u) => resolveUserGuid(u) === path.employeeId);
      if (!user?.id) {
        this.setFeedback(`Fiche employé introuvable pour ${path.employeeName}.`, 'error');
        return;
      }
      await this.router.navigate(['/users/edit', user.id], {
        queryParams: { passageProduction: path.id },
      });
    } catch (e) {
      this.setFeedback(e instanceof Error ? e.message : 'Impossible d’ouvrir la fiche employé', 'error');
    } finally {
      this.panelBusy = false;
      this.cdr.markForCheck();
    }
  }

  async confirmExtend(path: InitialTrainingPathDto): Promise<void> {
    if (!this.canConfirmExtend(path)) {
      this.setFeedback('Choisissez une date de fin postérieure à la date actuelle.', 'error');
      return;
    }
    this.panelBusy = true;
    this.cdr.markForCheck();
    try {
      await this.api.extendInitial(path.id, this.extendDate);
      this.closePanel();
      await this.load();
    } catch (e) {
      this.setFeedback(e instanceof Error ? e.message : 'Échec de la prolongation', 'error');
    } finally {
      this.panelBusy = false;
      this.cdr.markForCheck();
    }
  }

  async confirmReject(path: InitialTrainingPathDto): Promise<void> {
    if (!this.canConfirmReject()) {
      this.setFeedback('Saisissez un motif de rejet (3 caractères minimum).', 'error');
      return;
    }
    this.panelBusy = true;
    this.cdr.markForCheck();
    try {
      await this.api.rhReject(path.id, {
        rejectedBy: this.session.getStoredUser()?.username || 'RH',
        reason: this.rejectReason.trim(),
      });
      this.closePanel();
      await this.load();
    } catch (e) {
      this.setFeedback(e instanceof Error ? e.message : 'Échec du rejet', 'error');
    } finally {
      this.panelBusy = false;
      this.cdr.markForCheck();
    }
  }

  metricsFor(employeeId: string) {
    return this.api.getAttendanceMetricsStub(employeeId);
  }

  formatMetric(value: number | null | undefined): string {
    return value == null ? '—' : `${value} %`;
  }

  private setFeedback(message: string, kind: 'info' | 'error'): void {
    this.feedback = message;
    this.feedbackKind = kind;
    this.cdr.markForCheck();
  }

  private nextDayIso(dateIso: string): string {
    const d = new Date(dateIso);
    if (Number.isNaN(d.getTime())) {
      const today = new Date();
      today.setDate(today.getDate() + 1);
      return this.toDateInputValue(today);
    }
    d.setHours(0, 0, 0, 0);
    d.setDate(d.getDate() + 1);
    return this.toDateInputValue(d);
  }

  private toDateInputValue(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private isInitialStatus(value: string): value is InitialTrainingStatus {
    return value in INITIAL_TRAINING_STATUS_LABELS;
  }

  private isSessionStatus(value: string): value is TrainingSessionStatus {
    return value in TRAINING_SESSION_STATUS_LABELS;
  }
}
