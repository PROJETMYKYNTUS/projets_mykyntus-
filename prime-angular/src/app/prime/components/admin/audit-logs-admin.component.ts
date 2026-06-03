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
      <p class="text-slate-400 text-sm mb-4">
        Chaque changement de vue dans le module PRIME enregistre une entrée <span class="text-slate-300">PageView</span>
        (entité <span class="text-slate-300">Route</span>). Filtrez par utilisateur, rôle ou type d’entité.
      </p>

      <div class="flex flex-wrap gap-3 mb-4 items-end">
        <label class="text-xs text-slate-400 flex flex-col gap-1">
          userId
          <input
            type="text"
            class="rounded border border-navy-700 bg-navy-900 px-2 py-1.5 text-sm text-slate-200 w-40"
            [value]="filterUserId()"
            (input)="filterUserId.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-slate-400 flex flex-col gap-1">
          Rôle
          <input
            type="text"
            class="rounded border border-navy-700 bg-navy-900 px-2 py-1.5 text-sm text-slate-200 w-32"
            [value]="filterRole()"
            (input)="filterRole.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-slate-400 flex flex-col gap-1">
          Action
          <input
            type="text"
            class="rounded border border-navy-700 bg-navy-900 px-2 py-1.5 text-sm text-slate-200 w-28"
            placeholder="PageView"
            [value]="filterAction()"
            (input)="filterAction.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-slate-400 flex flex-col gap-1">
          Entité
          <input
            type="text"
            class="rounded border border-navy-700 bg-navy-900 px-2 py-1.5 text-sm text-slate-200 w-28"
            placeholder="Route"
            [value]="filterEntityType()"
            (input)="filterEntityType.set($any($event.target).value)"
          />
        </label>
        <label class="text-xs text-slate-400 flex flex-col gap-1">
          Max lignes
          <input
            type="number"
            min="1"
            max="1000"
            class="rounded border border-navy-700 bg-navy-900 px-2 py-1.5 text-sm text-slate-200 w-24"
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
                <th class="text-left py-3 text-slate-400">Date</th>
                <th class="text-left py-3 text-slate-400">Utilisateur</th>
                <th class="text-left py-3 text-slate-400">Rôle</th>
                <th class="text-left py-3 text-slate-400">Action</th>
                <th class="text-left py-3 text-slate-400">Entité</th>
              </tr>
            </thead>
            <tbody>
              @for (log of logs(); track log.id) {
                <tr class="border-b border-default/60">
                  <td class="py-3 text-slate-400 whitespace-nowrap text-xs">{{ log.at | date: 'short' }}</td>
                  <td class="py-3 text-slate-200">{{ log.userDisplayName }}</td>
                  <td class="py-3 text-slate-300">{{ log.role }}</td>
                  <td class="py-3 text-slate-300">{{ log.action }}</td>
                  <td class="py-3 text-slate-400 text-xs">
                    {{ log.entityType }}
                    @if (log.entityId) {
                      <span class="text-slate-500"> / {{ log.entityId }}</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
          @if (logs().length === 0) {
            <p class="text-slate-500 text-sm py-6 text-center">Aucune entrée pour le moment.</p>
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
