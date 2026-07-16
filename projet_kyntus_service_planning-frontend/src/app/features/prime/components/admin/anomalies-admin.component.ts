import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { PrimeCardComponent } from '../prime-card.component';
import { KyntusSelectSyncDirective } from '@/shared/directives/kyntus-select-sync.directive';
import {
  PrimeAdminService,
  type AnomalyDto,
  type AnomalyStatus,
} from '../../services/prime-admin.service';
import { PrimeNavRequestService } from '../../services/prime-nav-request.service';
import { PrimeSectionService } from '../../state/prime-section.service';
import { PrimeUiPermissionsService } from '../../services/prime-ui-permissions.service';

@Component({
  selector: 'app-anomalies-admin',
  standalone: true,
  imports: [PrimeCardComponent, KyntusSelectSyncDirective],
  template: `
    <app-prime-card title="Gestion des anomalies">
      <p class="text-muted text-sm mb-4">
        Anomalies possibles : écart de calcul (prime / challenge), fiche en double, valeurs hors plage,
        validateur manquant, validation obsolète ou périmètre incohérent.
      </p>
      <div class="flex flex-wrap gap-3 mb-4">
        <button
          type="button"
          [disabled]="recomputing()"
          (click)="recompute()"
          class="px-4 py-2 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white text-sm font-medium disabled:opacity-50"
        >
          Relancer la détection
        </button>
        <button
          type="button"
          (click)="reload()"
          class="px-4 py-2 rounded-lg border border-default bg-card text-primary text-sm font-medium hover:bg-input"
        >
          Rafraîchir la liste
        </button>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-4 gap-3 mb-4">
        <div class="rounded-lg border border-default bg-card p-3">
          <p class="text-[11px] uppercase tracking-wider text-muted">Total</p>
          <p class="text-xl font-semibold text-primary">{{ rows().length }}</p>
        </div>
        <div class="rounded-lg border border-default bg-card p-3">
          <p class="text-[11px] uppercase tracking-wider text-muted">Ouvertes</p>
          <p class="text-xl font-semibold text-primary">{{ counters().open }}</p>
        </div>
        <div class="rounded-lg border border-default bg-card p-3">
          <p class="text-[11px] uppercase tracking-wider text-muted">Critiques/High</p>
          <p class="text-xl font-semibold text-primary">{{ counters().critical }}</p>
        </div>
        <div class="rounded-lg border border-default bg-card p-3">
          <p class="text-[11px] uppercase tracking-wider text-muted">Résolues</p>
          <p class="text-xl font-semibold text-primary">{{ counters().resolved }}</p>
        </div>
      </div>

      <div class="flex flex-wrap gap-3 mb-4">
        <select
          [kyntusSelectSync]="statusFilter()"
          (kyntusSelectSyncChange)="statusFilter.set($event)"
          class="px-3 py-2 rounded-lg border border-default bg-app text-sm text-primary"
        >
          <option value="">Tous statuts</option>
          <option value="Open">Open</option>
          <option value="InReview">InReview</option>
          <option value="Resolved">Resolved</option>
          <option value="Ignored">Ignored</option>
        </select>
        <select
          [kyntusSelectSync]="severityFilter()"
          (kyntusSelectSyncChange)="severityFilter.set($event)"
          class="px-3 py-2 rounded-lg border border-default bg-app text-sm text-primary"
        >
          <option value="">Toutes gravités</option>
          <option value="Critical">Critical</option>
          <option value="High">High</option>
          <option value="Medium">Medium</option>
          <option value="Low">Low</option>
        </select>
      </div>

      @if (selectedRow(); as selected) {
        <div class="mb-4 rounded-xl border border-cyan-500/30 bg-cyan-500/10 p-4">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p class="text-xs uppercase tracking-wider text-muted">Détail de traitement</p>
              <h3 class="mt-1 text-base font-semibold text-primary">{{ anomalyTitle(selected) }}</h3>
              <p class="mt-1 text-sm text-muted">{{ selected.description }}</p>
            </div>
            <button
              type="button"
              (click)="openTarget(selected)"
              class="px-3 py-2 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white text-xs font-semibold disabled:opacity-50"
              [disabled]="!permissions.can('Admin', 'Read', 'Global')"
            >
              Traiter à la source
            </button>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-4 gap-3 mt-4 text-sm">
            <div class="rounded-lg bg-input border border-default p-3">
              <p class="text-[11px] uppercase tracking-wider text-muted">Objet concerné</p>
              <p class="mt-1 text-primary">{{ targetLabel(selected) }}</p>
            </div>
            <div class="rounded-lg bg-input border border-default p-3">
              <p class="text-[11px] uppercase tracking-wider text-muted">Périmètre</p>
              <p class="mt-1 text-primary">{{ scopeLabel(selected) }}</p>
            </div>
            <div class="rounded-lg bg-input border border-default p-3">
              <p class="text-[11px] uppercase tracking-wider text-muted">Impact</p>
              <p class="mt-1 text-primary">{{ impactLabel(selected) }}</p>
            </div>
            <div class="rounded-lg bg-input border border-default p-3">
              <p class="text-[11px] uppercase tracking-wider text-muted">Action recommandée</p>
              <p class="mt-1 text-primary">{{ recommendedAction(selected) }}</p>
            </div>
          </div>
          <label class="block mt-4 text-xs uppercase tracking-wider text-muted">
            Note de résolution
            <textarea
              class="mt-2 w-full rounded-lg border border-default bg-input px-3 py-2 text-sm text-primary"
              rows="2"
              [value]="resolutionNote()"
              (input)="resolutionNote.set($any($event.target).value)"
              placeholder="Expliquer la correction effectuée ou la raison d'ignorance."
            ></textarea>
          </label>
        </div>
      }

      @if (loading()) {
        <div class="py-12 flex justify-center">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-cyan-500"></div>
        </div>
      } @else if (error()) {
        <p class="text-rose-400 text-sm">{{ error() }}</p>
      } @else {
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-default">
                <th class="text-left py-3 text-muted">Type</th>
                <th class="text-left py-3 text-muted">Gravité</th>
                <th class="text-left py-3 text-muted">Objet concerné</th>
                <th class="text-left py-3 text-muted">Description</th>
                <th class="text-left py-3 text-muted">Statut</th>
                <th class="text-right py-3 text-muted">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (row of filteredRows(); track row.id) {
                <tr class="border-b border-default/60">
                  <td class="py-3 text-primary">{{ row.type }}</td>
                  <td class="py-3 text-muted">{{ severityLabel(row.severity) }}</td>
                  <td class="py-3 text-primary">
                    <button
                      type="button"
                      (click)="selectRow(row)"
                      class="text-left hover:text-cyan-600 dark:hover:text-cyan-300"
                    >
                      <span class="block font-medium">{{ targetLabel(row) }}</span>
                      <span class="block text-xs text-muted">{{ scopeLabel(row) }}</span>
                    </button>
                  </td>
                  <td class="py-3 text-muted max-w-md">{{ row.description }}</td>
                  <td class="py-3">
                    <span class="text-xs px-2 py-1 rounded-full" [class]="statusClass(row.status)">
                      {{ statusLabel(row.status) }}
                    </span>
                  </td>
                  <td class="py-3 text-right">
                    @if (row.status === 'Open' || row.status === 'InReview') {
                      <div class="flex justify-end gap-2">
                        <button
                          type="button"
                          [disabled]="busyId() === row.id"
                          (click)="openTarget(row)"
                          class="px-2 py-1 rounded bg-cyan-500/20 text-primary text-xs font-medium border border-cyan-500/30"
                        >
                          Traiter
                        </button>
                        <button
                          type="button"
                          [disabled]="busyId() === row.id"
                          (click)="setStatus(row, 'Resolved')"
                          class="px-2 py-1 rounded bg-emerald-500/20 text-primary text-xs font-medium border border-emerald-500/30"
                        >
                          Résolu
                        </button>
                        <button
                          type="button"
                          [disabled]="busyId() === row.id"
                          (click)="setStatus(row, 'Ignored')"
                          class="px-2 py-1 rounded bg-slate-500/20 text-primary text-xs font-medium border border-default"
                        >
                          Ignorer
                        </button>
                      </div>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
          @if (filteredRows().length === 0) {
            <p class="text-muted text-sm py-6 text-center">Aucune anomalie détectée.</p>
          }
        </div>
      }
    </app-prime-card>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnomaliesAdminComponent implements OnInit {
  private readonly admin = inject(PrimeAdminService);
  private readonly nav = inject(PrimeNavRequestService);
  private readonly router = inject(Router);
  private readonly sections = inject(PrimeSectionService);
  readonly permissions = inject(PrimeUiPermissionsService);

  readonly rows = signal<AnomalyDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);
  readonly recomputing = signal(false);
  readonly statusFilter = signal('');
  readonly severityFilter = signal('');
  readonly selectedId = signal<string | null>(null);
  readonly resolutionNote = signal('');

  readonly selectedRow = computed(() => this.rows().find((row) => row.id === this.selectedId()) ?? null);

  readonly filteredRows = computed(() =>
    this.rows().filter((row) => {
      const statusOk = !this.statusFilter() || row.status === this.statusFilter();
      const severityOk = !this.severityFilter() || row.severity === this.severityFilter();
      return statusOk && severityOk;
    }),
  );

  readonly counters = computed(() => {
    const rows = this.rows();
    return {
      open: rows.filter((r) => r.status === 'Open' || r.status === 'InReview').length,
      critical: rows.filter((r) => r.severity === 'Critical' || r.severity === 'High').length,
      resolved: rows.filter((r) => r.status === 'Resolved').length,
    };
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.admin.listAnomalies().subscribe({
      next: (list) => {
        this.rows.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.error ?? 'Impossible de charger les anomalies.');
        this.rows.set([]);
        this.loading.set(false);
      },
    });
  }

  recompute(): void {
    this.recomputing.set(true);
    this.admin.recomputeAnomalies().subscribe({
      next: () => {
        this.recomputing.set(false);
        this.reload();
      },
      error: (err) => {
        this.error.set(err?.error?.error ?? 'Recalcul impossible.');
        this.recomputing.set(false);
      },
    });
  }

  setStatus(row: AnomalyDto, status: AnomalyStatus): void {
    const note = this.resolutionNote().trim();
    this.busyId.set(row.id);
    this.admin
      .updateAnomalyStatus(row.id, {
        status,
        resolvedByUserId: 'admin-ui',
        resolutionNote:
          note ||
          (status === 'Resolved'
            ? `Traité depuis l'interface Admin: ${this.recommendedAction(row)}`
            : `Ignoré depuis l'interface Admin: ${this.impactLabel(row)}`),
      })
      .subscribe({
        next: (updated) => {
          this.rows.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
          this.resolutionNote.set('');
          this.busyId.set(null);
        },
        error: (err) => {
          this.error.set(err?.error?.error ?? 'Mise à jour impossible.');
          this.busyId.set(null);
        },
      });
  }

  selectRow(row: AnomalyDto): void {
    this.selectedId.set(row.id);
    this.resolutionNote.set(row.resolutionNote ?? '');
  }

  openTarget(row: AnomalyDto): void {
    this.selectRow(row);
    if (row.status === 'Open') {
      this.setStatus(row, 'InReview');
    }
    if (row.type === 'InvalidScope') {
      void this.router.navigateByUrl('/organisation');
      return;
    }
    if (row.type === 'WorkflowBlocked') {
      this.sections.setActiveAdminSection('workflows');
      return;
    }
    const period = row.period?.trim();
    if (period) {
      this.nav.requestViewWithPeriod('/validation', period);
      return;
    }
    this.nav.requestView('/validation');
  }

  anomalyTitle(row: AnomalyDto): string {
    return `${this.severityLabel(row.severity)} - ${this.typeLabel(row.type)}`;
  }

  typeLabel(type: string): string {
    if (type === 'ComputationMismatch') return 'Écart de calcul';
    if (type === 'DuplicateFiche') return 'Fiche en double';
    if (type === 'OutOfRange') return 'Montant hors plage';
    if (type === 'MissingApprover') return 'Validateur manquant';
    if (type === 'StaleValidation') return 'Validation en retard';
    if (type === 'InvalidScope') return 'Périmètre incohérent';
    if (type === 'WorkflowBlocked') return 'Workflow bloqué';
    return type;
  }

  targetLabel(row: AnomalyDto): string {
    const entity = row.targetEntityType ? `${row.targetEntityType}` : 'Fiche PRIME';
    const id = row.targetEntityId ? ` #${row.targetEntityId}` : '';
    const period = row.period ? ` - ${row.period}` : '';
    return `${entity}${id}${period}`;
  }

  scopeLabel(row: AnomalyDto): string {
    const chunks = [
      row.poleId ? `Pôle ${row.poleId}` : '',
      row.celluleId ? `Cellule ${row.celluleId}` : '',
      row.serviceId ? `Service ${row.serviceId}` : '',
    ].filter(Boolean);
    return chunks.length ? chunks.join(' / ') : 'Périmètre à vérifier';
  }

  impactLabel(row: AnomalyDto): string {
    if (row.severity === 'Critical') return 'Bloque la validation ou le paiement tant que non traité.';
    if (row.severity === 'High') return 'Risque de montant incorrect ou de workflow incomplet.';
    if (row.type === 'StaleValidation') return 'Retarde l’avancement du cycle PRIME.';
    return 'À contrôler pour garder les résultats fiables.';
  }

  recommendedAction(row: AnomalyDto): string {
    if (row.type === 'ComputationMismatch') return 'Ouvrir la fiche, contrôler prime/challenge et recalculer.';
    if (row.type === 'DuplicateFiche') return 'Comparer les fiches de la période et conserver la bonne version.';
    if (row.type === 'OutOfRange') return 'Vérifier les montants saisis avant validation.';
    if (row.type === 'MissingApprover') return 'Contrôler l’historique et l’approbateur manquant.';
    if (row.type === 'StaleValidation') return 'Relancer le responsable de l’étape courante.';
    if (row.type === 'InvalidScope') return 'Corriger l’affectation organisationnelle.';
    if (row.type === 'WorkflowBlocked') return 'Vérifier la configuration des transitions workflow.';
    return 'Analyser le détail puis traiter dans l’écran source.';
  }

  statusClass(status: string): string {
    if (status === 'Open') return 'bg-amber-500/20 text-primary border border-amber-500/30';
    if (status === 'InReview') return 'bg-sky-500/20 text-primary border border-sky-500/30';
    if (status === 'Resolved') return 'bg-emerald-500/20 text-primary border border-emerald-500/30';
    return 'bg-slate-500/20 text-primary border border-default';
  }

  statusLabel(status: string): string {
    if (status === 'Open') return 'Ouverte';
    if (status === 'InReview') return 'En revue';
    if (status === 'Resolved') return 'Résolue';
    if (status === 'Ignored') return 'Ignorée';
    return status;
  }

  severityLabel(severity: string): string {
    if (severity === 'Critical') return 'Critique';
    if (severity === 'High') return 'Haute';
    if (severity === 'Medium') return 'Moyenne';
    if (severity === 'Low') return 'Faible';
    return severity;
  }
}
