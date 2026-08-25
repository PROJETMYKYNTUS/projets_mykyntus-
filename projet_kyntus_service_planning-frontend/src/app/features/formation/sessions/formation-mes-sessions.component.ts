import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  TRAINING_ATTENDANCE_LABELS,
  TRAINING_QUIZ_STATUS_LABELS,
  TRAINING_SESSION_STATUS_LABELS,
  type TrainingAssignmentDto,
  type TrainingAttendance,
  type TrainingQuizStatus,
  type TrainingSessionDto,
  type TrainingSessionStatus,
} from '../../../core/models/formation-training.models';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { KyntusEmptyStateComponent } from '../../../shared/components/ui/kyntus-empty-state.component';

@Component({
  selector: 'app-formation-mes-sessions',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, KyntusPageHeaderComponent, KyntusEmptyStateComponent],
  template: `
    <section class="ky-page-shell">
      <app-kyntus-page-header
        title="Mes sessions"
        subtitle="Présences, compte rendu et quiz pour vos séances"
      />
      <div class="card-navy p-4 space-y-3">
        @for (s of animated(); track s.id) {
          <div class="text-sm border border-line rounded-lg p-3 space-y-2">
            <div class="flex flex-wrap items-start justify-between gap-2">
              <div>
                <strong class="text-ink-1">{{ s.title }}</strong>
                — {{ s.assignmentCount }}/{{ s.capacity }}
                · {{ sessionStatusLabel(s.status) }}
                @if (s.hasReport) {
                  <span class="text-emerald-300 text-xs ml-2">CR déposé</span>
                }
                @if (s.quizStatus) {
                  <span class="text-muted text-xs ml-2">Quiz : {{ quizStatusLabel(s.quizStatus) }}</span>
                }
                <span class="text-muted text-xs block mt-1"
                  >{{ s.plannedStart | date: 'short' }} → {{ s.plannedEnd | date: 'short' }}</span
                >
              </div>
              <div class="flex flex-wrap gap-2">
                <button
                  type="button"
                  class="ky-btn-secondary text-xs"
                  [disabled]="!canOpenAppel(s) && attendanceSessionId() !== s.id"
                  [title]="appelHint(s)"
                  (click)="toggleAttendance(s)"
                >
                  {{ attendanceSessionId() === s.id ? 'Fermer l’appel' : 'Appel' }}
                </button>
                <button
                  type="button"
                  class="ky-btn-secondary text-xs"
                  [disabled]="!canOpenCompteRendu(s) && reportSessionId() !== s.id"
                  [title]="compteRenduHint(s)"
                  (click)="toggleReport(s)"
                >
                  Compte rendu
                </button>
                <a class="ky-btn-secondary text-xs" [routerLink]="['/mes-sessions', s.id, 'quiz']">Quiz</a>
              </div>
            </div>

            @if (reportSessionId() === s.id) {
              <div class="border-t border-line pt-2 space-y-2">
                <input type="file" accept=".pdf,.doc,.docx,application/pdf" (change)="onReportFile($event)" />
                <button
                  type="button"
                  class="ky-btn-primary text-xs"
                  [disabled]="!reportFile() || reportBusy()"
                  (click)="uploadReport(s.id)"
                >
                  Déposer PDF / Word
                </button>
                @if (reportMsg()) {
                  <p class="text-xs m-0" [class.text-rose-300]="reportError()" [class.text-emerald-300]="!reportError()">
                    {{ reportMsg() }}
                  </p>
                }
              </div>
            }

            @if (attendanceSessionId() === s.id) {
              @if (attendanceError()) {
                <p class="text-rose-300 text-xs m-0">{{ attendanceError() }}</p>
              }
              @if (attendanceLoading()) {
                <p class="text-muted text-xs m-0">Chargement des bénéficiaires…</p>
              } @else {
                <p class="text-xs text-muted m-0">
                  Présents {{ countAttendance('Present') }} · Absents {{ countAttendance('Absent') }} · Non
                  pointés {{ countAttendance('Pending') }}
                </p>
                @for (a of assignments(); track a.id) {
                  <div class="flex flex-wrap items-center justify-between gap-2 border-t border-line pt-2">
                    <div>
                      <span class="text-ink-1">{{ a.employeeName }}</span>
                      <span class="text-xs text-muted block">{{ attendanceLabel(a.attendance) }}</span>
                    </div>
                    <div class="flex gap-2">
                      <button
                        type="button"
                        class="ky-btn-primary text-xs"
                        [disabled]="attendanceBusy()"
                        (click)="mark(s.id, a.id, 'Present')"
                      >
                        Présent
                      </button>
                      <button
                        type="button"
                        class="ky-btn-secondary text-xs text-rose-300"
                        [disabled]="attendanceBusy()"
                        (click)="mark(s.id, a.id, 'Absent')"
                      >
                        Absent
                      </button>
                    </div>
                  </div>
                } @empty {
                  <p class="text-muted text-xs m-0">Aucun bénéficiaire affecté.</p>
                }
              }
            }
          </div>
        } @empty {
          <app-kyntus-empty-state title="Aucune session" description="Aucune session où vous êtes animateur." />
        }
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationMesSessionsComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  private readonly session = inject(KyntusSessionService);

  readonly animated = signal<TrainingSessionDto[]>([]);
  readonly assignments = signal<TrainingAssignmentDto[]>([]);
  readonly attendanceSessionId = signal<string | null>(null);
  readonly attendanceLoading = signal(false);
  readonly attendanceBusy = signal(false);
  readonly attendanceError = signal<string | null>(null);

  readonly reportSessionId = signal<string | null>(null);
  readonly reportFile = signal<File | null>(null);
  readonly reportBusy = signal(false);
  readonly reportMsg = signal<string | null>(null);
  readonly reportError = signal(false);

  ngOnInit(): void {
    void this.reload();
  }

  sessionStatusLabel(status: TrainingSessionStatus): string {
    return TRAINING_SESSION_STATUS_LABELS[status] ?? status;
  }

  quizStatusLabel(status: TrainingQuizStatus | string | number | null | undefined): string {
    if (status == null) return '';
    if (typeof status === 'number') {
      const map = ['Draft', 'Published', 'Graded', 'Validated', 'Rejected'] as const;
      const key = map[status];
      return key ? TRAINING_QUIZ_STATUS_LABELS[key] : String(status);
    }
    return TRAINING_QUIZ_STATUS_LABELS[status as TrainingQuizStatus] ?? String(status);
  }

  canOpenAppel(s: TrainingSessionDto): boolean {
    if (typeof s.canMarkAttendance === 'boolean') return s.canMarkAttendance;
    const start = this.parseSessionDate(s.plannedStart);
    return !!start && Date.now() >= start.getTime();
  }

  canOpenCompteRendu(s: TrainingSessionDto): boolean {
    if (typeof s.canUploadReport === 'boolean') return s.canUploadReport;
    const end = this.parseSessionDate(s.plannedEnd);
    return !!end && Date.now() >= end.getTime();
  }

  appelHint(s: TrainingSessionDto): string {
    if (this.canOpenAppel(s)) return 'Faire l’appel des présents / absents';
    if (s.attendanceBlockedReason) return s.attendanceBlockedReason;
    const start = this.parseSessionDate(s.plannedStart);
    return start
      ? `Disponible à partir du ${start.toLocaleString('fr-FR')}`
      : 'Début de séance inconnu';
  }

  compteRenduHint(s: TrainingSessionDto): string {
    if (this.canOpenCompteRendu(s)) return 'Déposer le compte rendu de séance';
    if (s.reportBlockedReason) return s.reportBlockedReason;
    const end = this.parseSessionDate(s.plannedEnd);
    return end
      ? `Disponible après la fin (${end.toLocaleString('fr-FR')})`
      : 'Fin de séance inconnue';
  }

  attendanceLabel(attendance: TrainingAttendance | string): string {
    const key = (attendance || 'Pending') as TrainingAttendance;
    return TRAINING_ATTENDANCE_LABELS[key] ?? String(attendance);
  }

  countAttendance(kind: TrainingAttendance): number {
    return this.assignments().filter((a) => (a.attendance || 'Pending') === kind).length;
  }

  private parseSessionDate(value?: string | null): Date | null {
    if (!value) return null;
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? null : d;
  }

  private animatorId(): string | null {
    const stored = this.session.getStoredUser();
    const id =
      stored?.subjectId ||
      (JSON.parse(localStorage.getItem('user') || '{}')?.guid as string | undefined);
    if (id && String(id).includes('-')) return String(id);
    return null;
  }

  private async reload(): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) {
      this.animated.set([]);
      return;
    }
    try {
      this.animated.set(await this.api.listMyAnimatedSessions(animatorId));
    } catch {
      this.animated.set([]);
    }

    const openId = this.attendanceSessionId();
    if (openId) {
      await this.loadAssignments(openId, animatorId);
    }
  }

  async toggleAttendance(s: TrainingSessionDto): Promise<void> {
    if (this.attendanceSessionId() === s.id) {
      this.attendanceSessionId.set(null);
      this.assignments.set([]);
      this.attendanceError.set(null);
      return;
    }
    if (!this.canOpenAppel(s)) return;
    const animatorId = this.animatorId();
    if (!animatorId) {
      this.attendanceError.set('Identifiant animateur introuvable (reconnectez-vous).');
      return;
    }
    this.attendanceSessionId.set(s.id);
    await this.loadAssignments(s.id, animatorId);
  }

  toggleReport(s: TrainingSessionDto): void {
    if (this.reportSessionId() === s.id) {
      this.reportSessionId.set(null);
      this.reportFile.set(null);
      this.reportMsg.set(null);
      return;
    }
    if (!this.canOpenCompteRendu(s)) return;
    this.reportSessionId.set(s.id);
    this.reportFile.set(null);
    this.reportMsg.set(null);
    this.reportError.set(false);
  }

  onReportFile(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    this.reportFile.set(input.files?.[0] ?? null);
  }

  async uploadReport(sessionId: string): Promise<void> {
    const file = this.reportFile();
    const animatorId = this.animatorId();
    if (!file || !animatorId) return;
    this.reportBusy.set(true);
    this.reportError.set(false);
    try {
      await this.api.uploadSessionReport(sessionId, file, animatorId);
      this.reportMsg.set('Compte rendu déposé.');
      await this.reload();
    } catch (e) {
      this.reportError.set(true);
      this.reportMsg.set(e instanceof Error ? e.message : 'Échec upload');
    } finally {
      this.reportBusy.set(false);
    }
  }

  private async loadAssignments(sessionId: string, animatorId: string): Promise<void> {
    this.attendanceLoading.set(true);
    this.attendanceError.set(null);
    try {
      this.assignments.set(await this.api.listSessionAssignments(sessionId, animatorId));
    } catch (e) {
      this.assignments.set([]);
      this.attendanceError.set(e instanceof Error ? e.message : 'Échec du chargement');
    } finally {
      this.attendanceLoading.set(false);
    }
  }

  async mark(sessionId: string, assignmentId: string, attendance: 'Present' | 'Absent'): Promise<void> {
    const animatorId = this.animatorId();
    if (!animatorId) return;
    this.attendanceBusy.set(true);
    this.attendanceError.set(null);
    try {
      const updated = await this.api.markAttendance(sessionId, assignmentId, attendance, animatorId);
      this.assignments.update((list) => list.map((a) => (a.id === updated.id ? updated : a)));
    } catch (e) {
      this.attendanceError.set(e instanceof Error ? e.message : 'Échec du pointage');
    } finally {
      this.attendanceBusy.set(false);
    }
  }
}
