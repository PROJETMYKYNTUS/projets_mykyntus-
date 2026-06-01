import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { PrimeCardComponent } from '../prime-card.component';
import {
  PrimeAdminService,
  type AnomalyDto,
  type AnomalyStatus,
} from '../../services/prime-admin.service';

@Component({
  selector: 'app-anomalies-admin',
  standalone: true,
  imports: [PrimeCardComponent],
  template: `
    <app-prime-card title="Gestion des anomalies">
      <p class="text-slate-400 text-sm mb-4">
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
          class="px-4 py-2 rounded-lg border border-navy-600 text-slate-200 text-sm hover:bg-navy-800"
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
          <p class="text-xl font-semibold text-amber-300">{{ counters().open }}</p>
        </div>
        <div class="rounded-lg border border-default bg-card p-3">
          <p class="text-[11px] uppercase tracking-wider text-muted">Critiques/High</p>
          <p class="text-xl font-semibold text-rose-300">{{ counters().critical }}</p>
        </div>
        <div class="rounded-lg border border-default bg-card p-3">
          <p class="text-[11px] uppercase tracking-wider text-muted">Résolues</p>
          <p class="text-xl font-semibold text-emerald-300">{{ counters().resolved }}</p>
        </div>
      </div>

      <div class="flex flex-wrap gap-3 mb-4">
        <select
          [value]="statusFilter()"
          (change)="statusFilter.set($any($event.target).value)"
          class="px-3 py-2 rounded-lg border border-default bg-app text-sm text-primary"
        >
          <option value="">Tous statuts</option>
          <option value="Open">Open</option>
          <option value="InReview">InReview</option>
          <option value="Resolved">Resolved</option>
          <option value="Ignored">Ignored</option>
        </select>
        <select
          [value]="severityFilter()"
          (change)="severityFilter.set($any($event.target).value)"
          class="px-3 py-2 rounded-lg border border-default bg-app text-sm text-primary"
        >
          <option value="">Toutes gravités</option>
          <option value="Critical">Critical</option>
          <option value="High">High</option>
          <option value="Medium">Medium</option>
          <option value="Low">Low</option>
        </select>
      </div>

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
                <th class="text-left py-3 text-slate-400">Type</th>
                <th class="text-left py-3 text-slate-400">Gravité</th>
                <th class="text-left py-3 text-slate-400">Description</th>
                <th class="text-left py-3 text-slate-400">Statut</th>
                <th class="text-right py-3 text-slate-400">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (row of filteredRows(); track row.id) {
                <tr class="border-b border-default/60">
                  <td class="py-3 text-slate-200">{{ row.type }}</td>
                  <td class="py-3 text-slate-400">{{ severityLabel(row.severity) }}</td>
                  <td class="py-3 text-slate-300 max-w-md">{{ row.description }}</td>
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
                          (click)="setStatus(row, 'Resolved')"
                          class="px-2 py-1 rounded bg-emerald-500/20 text-emerald-300 text-xs"
                        >
                          Résolu
                        </button>
                        <button
                          type="button"
                          [disabled]="busyId() === row.id"
                          (click)="setStatus(row, 'Ignored')"
                          class="px-2 py-1 rounded bg-slate-500/20 text-slate-300 text-xs"
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
            <p class="text-slate-500 text-sm py-6 text-center">Aucune anomalie détectée.</p>
          }
        </div>
      }
    </app-prime-card>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnomaliesAdminComponent implements OnInit {
  private readonly admin = inject(PrimeAdminService);

  readonly rows = signal<AnomalyDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);
  readonly recomputing = signal(false);
  readonly statusFilter = signal('');
  readonly severityFilter = signal('');

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
    this.busyId.set(row.id);
    this.admin
      .updateAnomalyStatus(row.id, {
        status,
        resolvedByUserId: 'admin-ui',
        resolutionNote: status === 'Resolved' ? 'Traité depuis l’interface Admin' : 'Ignoré depuis l’interface Admin',
      })
      .subscribe({
        next: (updated) => {
          this.rows.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
          this.busyId.set(null);
        },
        error: (err) => {
          this.error.set(err?.error?.error ?? 'Mise à jour impossible.');
          this.busyId.set(null);
        },
      });
  }

  statusClass(status: string): string {
    if (status === 'Open') return 'bg-amber-500/20 text-amber-300';
    if (status === 'InReview') return 'bg-sky-500/20 text-sky-300';
    if (status === 'Resolved') return 'bg-emerald-500/20 text-emerald-300';
    return 'bg-slate-500/20 text-slate-300';
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
