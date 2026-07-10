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
    <app-kyntus-page-header
      title="Passage en production"
      subtitle="Validation RH après accord formateur (sans affichage des notes quiz)"
    />
    <div class="card-navy p-4 space-y-3">
      @for (p of paths(); track p.id) {
        <div class="border border-default/30 rounded-lg p-3 flex flex-wrap items-center justify-between gap-3">
          <div>
            <strong>{{ p.employeeName }}</strong>
            <p class="text-xs text-muted">{{ statusLabels[p.status] }}</p>
            <p class="text-xs text-muted">Fin formation prévue : {{ p.dateFinPrevue | date:'shortDate' }}</p>
          </div>
          <div class="flex gap-2">
            <button type="button" class="ky-btn-primary" (click)="validate(p)">Valider production</button>
            <button type="button" class="ky-btn-secondary text-rose-300" (click)="reject(p)">Rejeter</button>
          </div>
        </div>
      } @empty {
        <p class="text-muted text-sm">Aucun employé en attente de validation RH.</p>
      }
    </div>
  `,
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
