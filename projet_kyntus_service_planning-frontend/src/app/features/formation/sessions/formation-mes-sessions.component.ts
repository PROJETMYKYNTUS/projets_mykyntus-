import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  TRAINING_ATTENDANCE_LABELS,
  TRAINING_SESSION_STATUS_LABELS,
  type TrainingAssignmentDto,
  type TrainingAttendance,
  type TrainingSessionDto,
  type TrainingSessionStatus,
} from '../../../core/models/formation-training.models';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { KyntusEmptyStateComponent } from '../../../shared/components/ui/kyntus-empty-state.component';

@Component({
  selector: 'app-formation-mes-sessions',
  standalone: true,
  imports: [CommonModule, KyntusPageHeaderComponent, KyntusEmptyStateComponent],
  template: `
    <section class="ky-page-shell">
      <app-kyntus-page-header
        title="Mes sessions"
        subtitle="Sessions continues où vous êtes animateur — appel et présences"
      />
      <div class="card-navy p-4 space-y-3">
        <p class="text-xs text-muted m-0">
          Ouvrez l’appel pour marquer présents / absents les bénéficiaires affectés par la RH.
        </p>
        @for (s of animated(); track s.id) {
          <div class="text-sm border border-line rounded-lg p-3 space-y-2">
            <div class="flex flex-wrap items-start justify-between gap-2">
              <div>
                <strong class="text-ink-1">{{ s.title }}</strong>
                — {{ s.assignmentCount }}/{{ s.capacity }}
                · {{ sessionStatusLabel(s.status) }}
                <span class="text-muted text-xs block mt-1"
                  >{{ s.plannedStart | date: 'short' }} → {{ s.plannedEnd | date: 'short' }}</span
                >
              </div>
              <button type="button" class="ky-btn-secondary text-xs" (click)="toggleAttendance(s)">
                {{ attendanceSessionId() === s.id ? 'Fermer l’appel' : 'Appel / Présences' }}
              </button>
            </div>

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
                  <p class="text-muted text-xs m-0">Aucun bénéficiaire affecté par la RH pour cette session.</p>
                }
              }
            }
          </div>
        } @empty {
          <app-kyntus-empty-state
            title="Aucune session"
            description="Aucune session où vous êtes animateur."
          />
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

  ngOnInit(): void {
    void this.reload();
  }

  sessionStatusLabel(status: TrainingSessionStatus): string {
    return TRAINING_SESSION_STATUS_LABELS[status] ?? status;
  }

  attendanceLabel(attendance: TrainingAttendance | string): string {
    const key = (attendance || 'Pending') as TrainingAttendance;
    return TRAINING_ATTENDANCE_LABELS[key] ?? String(attendance);
  }

  countAttendance(kind: TrainingAttendance): number {
    return this.assignments().filter((a) => (a.attendance || 'Pending') === kind).length;
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
    const animatorId = this.animatorId();
    if (!animatorId) {
      this.attendanceError.set('Identifiant animateur introuvable (reconnectez-vous).');
      return;
    }
    this.attendanceSessionId.set(s.id);
    await this.loadAssignments(s.id, animatorId);
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
      this.assignments.update((rows) => rows.map((r) => (r.id === updated.id ? updated : r)));
    } catch (e) {
      this.attendanceError.set(e instanceof Error ? e.message : 'Échec du pointage');
    } finally {
      this.attendanceBusy.set(false);
    }
  }
}
