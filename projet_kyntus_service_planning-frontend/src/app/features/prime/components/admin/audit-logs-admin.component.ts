import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { PrimeCardComponent } from '../prime-card.component';
import { PrimeAdminService, type AuditLogDto, type AuditLogFilters } from '../../services/prime-admin.service';

@Component({
  selector: 'app-audit-logs-admin',
  standalone: true,
  imports: [PrimeCardComponent, DatePipe],
  template: `
    <app-prime-card title="Supervision &amp; logs">
      <p class="text-muted text-sm mb-4">
        Chaque changement de vue dans le module PRIME enregistre une entrée <span class="text-muted">PageView</span>
        (entité <span class="text-muted">Route</span>). Filtrez par utilisateur, rôle ou type d’entité.
      </p>

      <div class="flex flex-wrap gap-3 mb-4 items-end">
        <label class="text-xs text-muted flex flex-col gap-1">
          userId
          <input
            type="text"
            class="rounded border border-default bg-input px-2 py-1.5 text-sm text-primary w-40"
            [value]="filterUserId()"
            (input)="filterUserId.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-muted flex flex-col gap-1">
          Rôle
          <input
            type="text"
            class="rounded border border-default bg-input px-2 py-1.5 text-sm text-primary w-32"
            [value]="filterRole()"
            (input)="filterRole.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-muted flex flex-col gap-1">
          Action
          <input
            type="text"
            class="rounded border border-default bg-input px-2 py-1.5 text-sm text-primary w-28"
            placeholder="PageView"
            [value]="filterAction()"
            (input)="filterAction.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-muted flex flex-col gap-1">
          Entité
          <input
            type="text"
            class="rounded border border-default bg-input px-2 py-1.5 text-sm text-primary w-28"
            placeholder="Route"
            [value]="filterEntityType()"
            (input)="filterEntityType.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-muted flex flex-col gap-1">
          Max lignes
          <input
            type="number"
            min="1"
            max="1000"
            class="rounded border border-default bg-input px-2 py-1.5 text-sm text-primary w-24"
            [value]="filterTake()"
            (input)="filterTake.set(+$any($event.target).value || 300)"
          />
        </label>
        <button
          type="button"
          (click)="reload()"
          [disabled]="loading()"
          class="rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white text-sm px-4 py-2 disabled:opacity-50"
        >
          Appliquer
        </button>
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
                <th class="text-left py-3 text-muted">Date</th>
                <th class="text-left py-3 text-muted">Utilisateur</th>
                <th class="text-left py-3 text-muted">Rôle</th>
                <th class="text-left py-3 text-muted">Action</th>
                <th class="text-left py-3 text-muted">Entité</th>
              </tr>
            </thead>
            <tbody>
              @for (log of logs(); track log.id) {
                <tr class="border-b border-default/60">
                  <td class="py-3 text-muted whitespace-nowrap text-xs">{{ log.at | date: 'short' }}</td>
                  <td class="py-3 text-primary">{{ log.userDisplayName }}</td>
                  <td class="py-3 text-muted">{{ log.role }}</td>
                  <td class="py-3 text-muted">{{ log.action }}</td>
                  <td class="py-3 text-muted text-xs">
                    {{ log.entityType }}
                    @if (log.entityId) {
                      <span class="text-muted"> / {{ log.entityId }}</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
          @if (logs().length === 0) {
            <p class="text-muted text-sm py-6 text-center">Aucune entrée pour le moment.</p>
          }
        </div>
      }
    </app-prime-card>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogsAdminComponent implements OnInit {
  private readonly admin = inject(PrimeAdminService);

  readonly logs = signal<AuditLogDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly filterUserId = signal('');
  readonly filterRole = signal('');
  readonly filterAction = signal('');
  readonly filterEntityType = signal('');
  readonly filterTake = signal(300);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const f: AuditLogFilters = { take: Math.min(1000, Math.max(1, this.filterTake())) };
    const uid = this.filterUserId().trim();
    const role = this.filterRole().trim();
    const action = this.filterAction().trim();
    const entityType = this.filterEntityType().trim();
    if (uid) f.userId = uid;
    if (role) f.role = role;
    if (action) f.action = action;
    if (entityType) f.entityType = entityType;

    this.admin.listAuditLogs(f).subscribe({
      next: (list) => {
        this.logs.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.error ?? 'Impossible de charger les logs.');
        this.logs.set([]);
        this.loading.set(false);
      },
    });
  }
}
