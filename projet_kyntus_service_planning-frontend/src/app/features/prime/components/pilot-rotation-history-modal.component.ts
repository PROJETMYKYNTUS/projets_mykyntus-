import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import { catchError, firstValueFrom, of } from 'rxjs';
import {
  DirectoryEmployeeApiService,
  type PilotRotationHistoryEntryDto,
} from '../../../core/directory/directory-employee-api.service';
import { PrimeModalComponent } from './prime-modal.component';

@Component({
  selector: 'app-pilot-rotation-history-modal',
  standalone: true,
  imports: [PrimeModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-prime-modal
      [isOpen]="open"
      [title]="modalTitle"
      className="max-w-xl"
      (onClose)="close.emit()"
    >
      @if (loading()) {
        <p class="text-sm text-muted">Chargement de l'historique…</p>
      } @else if (error()) {
        <p class="text-sm text-rose-400">{{ error() }}</p>
      } @else if (entries().length === 0) {
        <p class="text-sm text-muted">Aucune rotation enregistrée pour cet employé.</p>
      } @else {
        <div class="prh-timeline">
          @for (entry of entries(); track entry.serviceId + entry.effectiveFrom) {
            <article class="prh-entry" [class.prh-entry--override]="entry.isOverride">
              <div class="prh-entry-header">
                <strong>{{ entry.serviceName }}</strong>
                @if (entry.isOverride) {
                  <span class="prh-badge prh-badge--override">Dérogation</span>
                }
                @if (!entry.effectiveTo) {
                  <span class="prh-badge prh-badge--current">Actuel</span>
                }
              </div>
              <dl class="prh-meta">
                <div>
                  <dt>Du</dt>
                  <dd>{{ formatDate(entry.effectiveFrom) }}</dd>
                </div>
                <div>
                  <dt>Au</dt>
                  <dd>{{ formatDate(entry.effectiveTo) }}</dd>
                </div>
                <div>
                  <dt>Durée</dt>
                  <dd>{{ formatDuration(entry.durationDays) }}</dd>
                </div>
              </dl>
              @if (entry.changeReason?.trim()) {
                <p class="prh-reason">{{ entry.changeReason }}</p>
              }
            </article>
          }
        </div>
      }
    </app-prime-modal>
  `,
  styles: `
    .prh-timeline {
      display: flex;
      flex-direction: column;
      gap: 12px;
      border-left: 2px solid color-mix(in srgb, #6366f1 35%, var(--border-color, #334155));
      margin-left: 8px;
      padding-left: 18px;
    }

    .prh-entry {
      position: relative;
      padding: 14px 16px;
      border-radius: 12px;
      border: 1px solid var(--border-color, #334155);
      background: color-mix(in srgb, var(--bg-card, #1e293b) 92%, #6366f1 8%);
    }

    .prh-entry::before {
      content: '';
      position: absolute;
      left: -27px;
      top: 18px;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: #6366f1;
      border: 2px solid var(--bg-card, #1e293b);
    }

    .prh-entry--override {
      border-color: color-mix(in srgb, #f59e0b 40%, var(--border-color, #334155));
    }

    .prh-entry-header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-bottom: 10px;
    }

    .prh-badge {
      display: inline-flex;
      padding: 2px 8px;
      border-radius: 999px;
      font-size: 0.7rem;
      font-weight: 600;
    }

    .prh-badge--current {
      color: #86efac;
      background: color-mix(in srgb, #22c55e 16%, transparent);
    }

    .prh-badge--override {
      color: #fcd34d;
      background: color-mix(in srgb, #f59e0b 16%, transparent);
    }

    .prh-meta {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 8px;
      margin: 0;
    }

    .prh-meta dt {
      margin: 0;
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--text-muted, #94a3b8);
    }

    .prh-meta dd {
      margin: 2px 0 0;
      font-size: 0.875rem;
      color: var(--text-primary, #e2e8f0);
    }

    .prh-reason {
      margin: 10px 0 0;
      font-size: 0.8rem;
      color: var(--text-muted, #94a3b8);
      line-height: 1.4;
    }
  `,
})
export class PilotRotationHistoryModalComponent implements OnChanges {
  private readonly api = inject(DirectoryEmployeeApiService);

  @Input() open = false;
  @Input() employeeId = '';
  @Input() employeeName = '';
  @Output() readonly close = new EventEmitter<void>();

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly entries = signal<PilotRotationHistoryEntryDto[]>([]);

  get modalTitle(): string {
    const name = this.employeeName.trim();
    return name ? `Historique rotation — ${name}` : 'Historique rotation';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['open'] || changes['employeeId']) && this.open && this.employeeId.trim()) {
      void this.load();
    }
    if (changes['open'] && !this.open) {
      this.entries.set([]);
      this.error.set(null);
    }
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('fr-FR');
  }

  formatDuration(days?: number | null): string {
    if (days == null) return '—';
    if (days < 30) return `${days} jour${days > 1 ? 's' : ''}`;
    const months = Math.floor(days / 30);
    const rem = days % 30;
    if (rem === 0) return `${months} mois`;
    return `${months} mois ${rem} j`;
  }

  private async load(): Promise<void> {
    const id = this.employeeId.trim();
    if (!id) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const rows = await firstValueFrom(
        this.api.getPilotRotationHistory(id).pipe(
          catchError(() => {
            this.error.set('Impossible de charger l’historique de rotation.');
            return of([] as PilotRotationHistoryEntryDto[]);
          }),
        ),
      );
      this.entries.set(rows);
    } finally {
      this.loading.set(false);
    }
  }
}
