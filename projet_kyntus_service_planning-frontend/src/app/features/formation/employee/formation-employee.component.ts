import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Calendar, Inbox, Loader2 } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  INITIAL_TRAINING_STATUS_LABELS,
  TRAINING_ATTENDANCE_LABELS,
  TRAINING_QUIZ_STATUS_LABELS,
  TRAINING_SESSION_STATUS_LABELS,
  type CatalogEnrollmentStatus,
  type InitialTrainingPathDto,
  type InitialTrainingStatus,
  type MyAssignedTrainingSessionDto,
  type MySelfServiceCatalogItemDto,
  type TrainingAttendance,
  type TrainingQuizStatus,
  type TrainingSessionStatus,
} from '../../../core/models/formation-training.models';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';

@Component({
  selector: 'app-formation-employee',
  standalone: true,
  imports: [CommonModule, RouterLink, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './formation-employee.component.html',
  styleUrls: ['./formation-employee.component.css'],
})
export class FormationEmployeeComponent implements OnInit {
  readonly icons = {
    inbox: Inbox,
    loader: Loader2,
    calendar: Calendar,
  };

  initialPaths: InitialTrainingPathDto[] = [];
  assignedSessions: MyAssignedTrainingSessionDto[] = [];
  selfServiceItems: MySelfServiceCatalogItemDto[] = [];
  initialStatusLabels = INITIAL_TRAINING_STATUS_LABELS;
  sessionStatusLabels = TRAINING_SESSION_STATUS_LABELS;
  attendanceLabels = TRAINING_ATTENDANCE_LABELS;
  loading = false;
  loadError = '';

  constructor(
    private trainingApi: FormationTrainingService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    void this.reload();
  }

  get isEmpty(): boolean {
    return (
      !this.loading &&
      !this.loadError &&
      this.initialPaths.length === 0 &&
      this.assignedSessions.length === 0 &&
      this.selfServiceItems.length === 0
    );
  }

  private async reload(): Promise<void> {
    this.loading = true;
    this.loadError = '';
    this.cdr.detectChanges();
    const results = await Promise.allSettled([
      this.loadInitialPaths(),
      this.loadAssignedSessions(),
      this.loadSelfService(),
    ]);
    const failed = results.filter((r): r is PromiseRejectedResult => r.status === 'rejected');
    const succeeded = results.some((r) => r.status === 'fulfilled');
    // N'afficher l'erreur globale que si tout a échoué (sinon partial data OK).
    if (failed.length > 0 && !succeeded) {
      const reason = failed[0]?.reason;
      this.loadError =
        reason?.error?.error ||
        reason?.message ||
        String(reason || 'Impossible de charger vos formations.');
    } else if (failed.length > 0) {
      console.warn('Chargement partiel Mes formations:', failed.map((f) => f.reason));
    }
    this.loading = false;
    this.cdr.detectChanges();
  }

  private async loadSelfService(): Promise<void> {
    this.selfServiceItems = await this.trainingApi.listMySelfServiceCatalog();
  }

  enrollmentStatusLabel(status: CatalogEnrollmentStatus | string): string {
    switch (status) {
      case 'Completed':
        return 'Terminé';
      case 'InProgress':
        return 'En cours';
      case 'Overdue':
        return 'En retard';
      default:
        return 'À démarrer';
    }
  }

  enrollmentStatusClass(status: CatalogEnrollmentStatus | string): string {
    switch (status) {
      case 'Completed':
        return 'badge-done';
      case 'InProgress':
        return 'badge-progress';
      case 'Overdue':
        return 'badge-reject';
      default:
        return 'badge-pending';
    }
  }

  isDueSoon(item: MySelfServiceCatalogItemDto): boolean {
    if (!item.dueAt || item.status === 'Completed') return false;
    const due = new Date(item.dueAt).getTime();
    const now = Date.now();
    return due >= now && due - now <= 3 * 24 * 60 * 60 * 1000;
  }

  private async loadInitialPaths(): Promise<void> {
    this.initialPaths = await this.trainingApi.listMyInitialPaths();
  }

  private async loadAssignedSessions(): Promise<void> {
    this.assignedSessions = await this.trainingApi.listMyAssignedSessions();
  }

  sessionStatusLabel(status: TrainingSessionStatus | string): string {
    return this.sessionStatusLabels[status as TrainingSessionStatus] ?? String(status);
  }

  attendanceLabel(attendance: TrainingAttendance | string): string {
    const key = (attendance || 'Pending') as TrainingAttendance;
    return this.attendanceLabels[key] ?? String(attendance);
  }

  quizStatusLabel(status: string | null | undefined): string {
    if (!status) return '';
    return TRAINING_QUIZ_STATUS_LABELS[status as TrainingQuizStatus] ?? status;
  }

  attemptHistoryLabel(s: MyAssignedTrainingSessionDto): string {
    if (!s.attemptId) return '';
    if (s.attemptGraded) {
      const score = s.finalScore != null ? `${s.finalScore} %` : '—';
      if (s.passed === true) return `Résultat : ${score} · Valide`;
      if (s.passed === false) return `Résultat : ${score} · Non valide`;
      return `Résultat : ${score}`;
    }
    return 'Quiz soumis · en attente de notation';
  }

  sessionStatusClass(status: TrainingSessionStatus | string): string {
    switch (status) {
      case 'Scheduled':
        return 'badge-scheduled';
      case 'InProgress':
        return 'badge-progress';
      case 'Completed':
        return 'badge-done';
      case 'Cancelled':
        return 'badge-cancel';
      default:
        return 'badge-pending';
    }
  }

  attendanceClass(attendance: TrainingAttendance | string): string {
    switch (attendance) {
      case 'Present':
        return 'badge-present';
      case 'Absent':
        return 'badge-absent';
      default:
        return 'badge-pending';
    }
  }

  initialStatusClass(status: InitialTrainingStatus | string): string {
    switch (status) {
      case 'EnProduction':
        return 'badge-done';
      case 'Rejete':
        return 'badge-reject';
      case 'AttenteValidationRh':
      case 'AttenteValidationFormateur':
        return 'badge-progress';
      case 'EnCours':
      case 'QuizASaisir':
        return 'badge-scheduled';
      default:
        return 'badge-pending';
    }
  }
}
