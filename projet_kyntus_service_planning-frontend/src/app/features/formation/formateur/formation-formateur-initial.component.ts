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

@Component({
  selector: 'app-formation-formateur-initial',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  template: `
    <app-kyntus-page-header title="Formation initiale" subtitle="File formateur — saisie quiz et validation" />
    <div class="card-navy p-4 space-y-3">
      @for (p of paths(); track p.id) {
        <div class="border border-default/30 rounded-lg p-3 space-y-2">
          <div class="flex justify-between gap-2">
            <strong>{{ p.employeeName }}</strong>
            <span class="text-xs text-muted">{{ statusLabels[p.status] }}</span>
          </div>
          <p class="text-xs text-muted">{{ p.dateDebut | date:'shortDate' }} → {{ p.dateFinPrevue | date:'shortDate' }}</p>
          @if (!p.hasQuizResult) {
            <div class="grid gap-2 md:grid-cols-3">
              <input class="ky-input" type="number" min="0" max="100" placeholder="Note %" [(ngModel)]="quiz[p.id].score" />
              <label class="inline-flex items-center gap-2 text-sm">
                <input type="checkbox" [(ngModel)]="quiz[p.id].passed" /> Réussi
              </label>
              <button type="button" class="ky-btn-secondary" (click)="saveQuiz(p)">Enregistrer quiz</button>
            </div>
          }
          <div class="flex flex-wrap gap-2">
            <button type="button" class="ky-btn-primary" (click)="validate(p)">Valider</button>
            <button type="button" class="ky-btn-secondary" (click)="extend(p)">Prolonger</button>
            <button type="button" class="ky-btn-secondary text-rose-300" (click)="reject(p)">Rejeter</button>
          </div>
        </div>
      } @empty {
        <p class="text-muted text-sm">Aucun nouvel arrivant en formation initiale.</p>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationFormateurInitialComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  private readonly session = inject(KyntusSessionService);
  readonly paths = signal<InitialTrainingPathDto[]>([]);
  readonly statusLabels = INITIAL_TRAINING_STATUS_LABELS;
  quiz: Record<string, { score: number; passed: boolean }> = {};

  ngOnInit(): void {
    void this.reload();
  }

  private async reload(): Promise<void> {
    const rows = await this.api.listFormateurInitial();
    this.paths.set(rows);
    for (const row of rows) {
      this.quiz[row.id] ??= { score: 0, passed: false };
    }
  }

  async saveQuiz(path: InitialTrainingPathDto): Promise<void> {
    const q = this.quiz[path.id];
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
