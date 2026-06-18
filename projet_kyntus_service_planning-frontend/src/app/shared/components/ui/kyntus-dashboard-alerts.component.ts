import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AlertTriangle, Info, XCircle } from 'lucide';
import { LucideIconComponent } from '../../lucide-icon.component';
import type { KyntusDashboardAlert } from './kyntus-dashboard.model';

@Component({
  selector: 'app-kyntus-dashboard-alerts',
  standalone: true,
  imports: [RouterLink, LucideIconComponent],
  template: `
    @if (alerts.length > 0) {
      <div class="kyntus-dashboard-alerts">
        @for (alert of alerts; track alert.message) {
          <div class="kyntus-alert-card" [class]="severityClass(alert.severity)">
            <div class="kyntus-alert-title">
              <app-lucide-icon [icon]="iconFor(alert.severity)" className="w-4 h-4" />
              <span>{{ alert.title || defaultTitle(alert.severity) }}</span>
            </div>
            <p class="kyntus-alert-message">{{ alert.message }}</p>
            @if (alert.action) {
              <button type="button" class="kyntus-alert-action" (click)="alert.action()">
                {{ alert.actionLabel || 'Voir' }}
              </button>
            } @else if (alert.route) {
              <a class="kyntus-alert-action" [routerLink]="alert.route" [queryParams]="alert.queryParams">{{ alert.actionLabel || 'Voir' }}</a>
            }
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .kyntus-dashboard-alerts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 1rem;
    }
    .kyntus-alert-card {
      padding: 1rem;
      border-radius: 0.75rem;
      border: 1px solid var(--border-color, #e2e8f0);
      background: var(--bg-card, #fff);
      box-shadow: var(--ky-shadow-sm);
    }
    .kyntus-alert-card.warn {
      border-color: color-mix(in srgb, #f59e0b 45%, var(--border-color));
      background: color-mix(in srgb, #f59e0b 10%, var(--bg-card));
    }
    .kyntus-alert-card.error {
      border-color: color-mix(in srgb, #ef4444 45%, var(--border-color));
      background: color-mix(in srgb, #ef4444 8%, var(--bg-card));
    }
    .kyntus-alert-card.info {
      border-color: color-mix(in srgb, #3b82f6 45%, var(--border-color));
      background: color-mix(in srgb, #3b82f6 8%, var(--bg-card));
    }
    .kyntus-alert-title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.8125rem;
      font-weight: 600;
      margin-bottom: 0.35rem;
      color: var(--text-primary, #f1f5f9);
    }
    .kyntus-alert-message {
      margin: 0;
      font-size: 0.8125rem;
      color: var(--text-muted, #94a3b8);
      line-height: 1.45;
    }
    .kyntus-alert-action {
      display: inline-block;
      margin-top: 0.5rem;
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--ky-accent, #3b82f6);
      background: none;
      border: none;
      padding: 0;
      cursor: pointer;
      text-decoration: none;
    }
    .kyntus-alert-action:hover { text-decoration: underline; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusDashboardAlertsComponent {
  @Input() alerts: KyntusDashboardAlert[] = [];

  readonly icons = { info: Info, warn: AlertTriangle, error: XCircle };

  severityClass(severity: KyntusDashboardAlert['severity']): string {
    return severity;
  }

  iconFor(severity: KyntusDashboardAlert['severity']) {
    return this.icons[severity];
  }

  defaultTitle(severity: KyntusDashboardAlert['severity']): string {
    if (severity === 'error') return 'Attention';
    if (severity === 'warn') return 'À surveiller';
    return 'Information';
  }
}
