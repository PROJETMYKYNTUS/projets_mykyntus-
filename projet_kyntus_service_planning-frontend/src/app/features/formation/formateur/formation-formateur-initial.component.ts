import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  INITIAL_TRAINING_STATUS_LABELS,
  type InitialTrainingPathDto,
} from '../../../core/models/formation-training.models';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { KyntusEmptyStateComponent } from '../../../shared/components/ui/kyntus-empty-state.component';

@Component({
  selector: 'app-formation-formateur-initial',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent, KyntusEmptyStateComponent],
  template: `
    <section class="ky-page-shell">
      <app-kyntus-page-header title="Formation initiale" subtitle="File formateur — saisie quiz et validation" />
      <div class="card-navy p-4 space-y-3">
        @for (p of paths(); track p.id) {
          <div class="border border-line rounded-lg p-4 space-y-3">
            <div class="flex justify-between gap-2">
              <strong class="text-ink-1">{{ p.employeeName }}</strong>
              <span class="text-xs text-muted">{{ statusLabels[p.status] }}</span>
            </div>
            <p class="text-xs text-muted">{{ p.dateDebut | date: 'shortDate' }} → {{ p.dateFinPrevue | date: 'shortDate' }}</p>
            @if (!p.hasQuizResult || p.status === 'QuizASaisir') {
              <div class="grid gap-2 md:grid-cols-3">
                <input
                  class="ky-input"
                  type="number"
                  min="0"
                  max="100"
                  placeholder="Note %"
                  [(ngModel)]="quiz[p.id].score"
                  (ngModelChange)="onScoreChange(p.id)"
                />
                <span
                  class="inline-flex items-center text-sm"
                  [class.text-emerald-400]="quiz[p.id].passed"
                  [class.text-rose-300]="!quiz[p.id].passed"
                >
                  {{ quiz[p.id].passed ? 'Réussi (≥ ' + passThreshold + ' %)' : 'Échec (< ' + passThreshold + ' %)' }}
                </span>
                <button type="button" class="ky-btn-secondary" (click)="saveQuiz(p)">Enregistrer quiz</button>
              </div>
            }
            <div class="flex flex-wrap gap-2">
              <button type="button" class="ky-btn-primary" (click)="validate(p)" [disabled]="!canValidate(p)">
                Valider
              </button>
              <button type="button" class="ky-btn-secondary" (click)="extend(p)">Prolonger</button>
              <button type="button" class="ky-btn-secondary text-rose-300" (click)="reject(p)">Rejeter</button>
            </div>
          </div>
        } @empty {
          <app-kyntus-empty-state
            title="Aucun nouvel arrivant"
            description="Aucun nouvel arrivant en formation initiale pour le moment."
          />
        }
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationFormateurInitialComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  private readonly session = inject(KyntusSessionService);
  readonly paths = signal<InitialTrainingPathDto[]>([]);
  readonly statusLabels = INITIAL_TRAINING_STATUS_LABELS;
  readonly passThreshold = 70;
  quiz: Record<string, { score: number; passed: boolean }> = {};

  ngOnInit(): void {
    void this.reload();
  }

  onScoreChange(pathId: string): void {
    const q = this.quiz[pathId];
    if (!q) return;
    q.passed = Number(q.score) >= this.passThreshold;
  }

  canValidate(path: InitialTrainingPathDto): boolean {
    return path.hasQuizResult && path.status === 'AttenteValidationFormateur';
  }

  private async reload(): Promise<void> {
    const rows = await this.api.listFormateurInitial();
    this.paths.set(rows);
    for (const row of rows) {
      this.quiz[row.id] ??= { score: 0, passed: false };
      this.onScoreChange(row.id);
    }
  }

  async saveQuiz(path: InitialTrainingPathDto): Promise<void> {
    const q = this.quiz[path.id];
    this.onScoreChange(path.id);
    await this.api.recordQuiz(path.id, {
      quizScore: q.score,
      quizPassed: q.passed,
      recordedBy: this.session.getStoredUser()?.username || 'Formateur',
    });
    await this.reload();
  }

  async validate(path: InitialTrainingPathDto): Promise<void> {
    await this.api.formateurValidate(path.id);
    await this.reload();
  }

  async extend(path: InitialTrainingPathDto): Promise<void> {
    const next = prompt('Nouvelle date de fin (AAAA-MM-JJ)', path.dateFinPrevue.substring(0, 10));
    if (!next) return;
    await this.api.extendInitial(path.id, next);
    await this.reload();
  }

  async reject(path: InitialTrainingPathDto): Promise<void> {
    const reason = prompt(
      'Motif du rejet (entraîne la sortie complète de l’employé : désactivation + date de sortie)',
    );
    if (!reason?.trim()) return;
    if (!confirm(`Confirmer le rejet de ${path.employeeName} ? L’employé sera sorti du système.`)) return;
    await this.api.formateurReject(path.id, {
      rejectedBy: this.session.getStoredUser()?.username || 'Formateur',
      reason: reason.trim(),
    });
    await this.reload();
  }
}
