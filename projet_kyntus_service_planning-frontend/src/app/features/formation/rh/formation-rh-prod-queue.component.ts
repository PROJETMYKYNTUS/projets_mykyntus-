import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  INITIAL_TRAINING_STATUS_LABELS,
  type InitialTrainingPathDto,
} from '../../../core/models/formation-training.models';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';

@Component({
  selector: 'app-formation-rh-prod-queue',
  standalone: true,
  imports: [CommonModule, KyntusPageHeaderComponent],
  template: `
    <div class="ky-page-shell frpq-page">
      <app-kyntus-page-header
        title="Passage en production"
        subtitle="Validation RH après accord formateur, avec une vue plus lisible des validations en attente."
      />

      <section class="ky-card frpq-panel">
        @for (p of paths(); track p.id) {
          <article class="frpq-row">
            <div class="frpq-copy">
              <strong class="frpq-name">{{ p.employeeName }}</strong>
              <p class="frpq-meta">{{ statusLabels[p.status] }}</p>
              <p class="frpq-meta">Fin formation prévue : {{ p.dateFinPrevue | date:'shortDate' }}</p>
            </div>
            <div class="frpq-actions">
              <button type="button" class="ky-btn-primary" (click)="validate(p)">Valider production</button>
              <button type="button" class="ky-btn-secondary frpq-reject" (click)="reject(p)">Rejeter</button>
            </div>
          </article>
        } @empty {
          <div class="frpq-empty">
            <h3>Aucune validation en attente</h3>
            <p>Aucun employé n’est actuellement en attente de validation RH pour le passage en production.</p>
          </div>
        }
      </section>
    </div>
  `,
  styles: [`
    .frpq-page {
      display: grid;
      gap: 1rem;
    }
    .frpq-panel {
      padding: 1rem;
      display: grid;
      gap: 0.875rem;
      min-height: 12rem;
    }
    .frpq-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 1rem;
      border: 1px solid color-mix(in srgb, var(--border-color) 80%, transparent);
      border-radius: var(--radius-card);
      background: color-mix(in srgb, var(--bg-input) 80%, var(--bg-card));
    }
    .frpq-copy {
      display: grid;
      gap: 0.3rem;
      min-width: 15rem;
    }
    .frpq-name {
      color: var(--text-primary);
      font-size: 0.98rem;
    }
    .frpq-meta {
      margin: 0;
      font-size: 0.78rem;
      color: var(--text-muted);
    }
    .frpq-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
    }
    .frpq-reject {
      color: var(--danger-text);
    }
    .frpq-empty {
      min-height: 10rem;
      display: grid;
      place-items: center;
      text-align: center;
      padding: 2rem 1rem;
      color: var(--text-muted);
    }
    .frpq-empty h3 {
      margin: 0 0 0.4rem;
      color: var(--text-primary);
      font-size: 1rem;
    }
    .frpq-empty p {
      margin: 0;
      max-width: 32rem;
      line-height: 1.5;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationRhProdQueueComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  private readonly session = inject(KyntusSessionService);
  readonly paths = signal<InitialTrainingPathDto[]>([]);
  readonly statusLabels = INITIAL_TRAINING_STATUS_LABELS;

  ngOnInit(): void {
    void this.reload();
  }

  private async reload(): Promise<void> {
    this.paths.set(await this.api.listRhPendingInitial());
  }

  async validate(path: InitialTrainingPathDto): Promise<void> {
    await this.api.rhValidate(path.id);
    await this.reload();
  }

  async reject(path: InitialTrainingPathDto): Promise<void> {
    const reason = prompt(
      'Motif du rejet (entraîne la sortie complète de l’employé : désactivation + date de sortie)',
    );
    if (!reason?.trim()) return;
    if (!confirm(`Confirmer le rejet de ${path.employeeName} ? L’employé sera sorti du système.`)) return;
    await this.api.rhReject(path.id, {
      rejectedBy: this.session.getStoredUser()?.username || 'RH',
      reason: reason.trim(),
    });
    await this.reload();
  }
}
