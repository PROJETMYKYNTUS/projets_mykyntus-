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

  nextActionRoute(path: InitialTrainingPathDto): string | null {
    switch (path.status) {
      case 'EnCours':
      case 'QuizASaisir':
      case 'AttenteValidationFormateur':
        return '/formations/initiales';
      case 'AttenteValidationRh':
        return '/formations/passage-production';
      default:
        return null;
    }
  }

  nextActionLabel(path: InitialTrainingPathDto): string {
    switch (path.status) {
      case 'EnCours':
      case 'QuizASaisir':
        return 'Saisir quiz';
      case 'AttenteValidationFormateur':
        return 'File formateur';
      case 'AttenteValidationRh':
        return 'Passage production';
      default:
        return '';
    }
  }

  private isInitialStatus(value: string): value is InitialTrainingStatus {
    return value in INITIAL_TRAINING_STATUS_LABELS;
  }

  private isSessionStatus(value: string): value is TrainingSessionStatus {
    return value in TRAINING_SESSION_STATUS_LABELS;
  }
}
