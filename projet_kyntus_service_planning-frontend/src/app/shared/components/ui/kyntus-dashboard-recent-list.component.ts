import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-kyntus-dashboard-recent-list',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="kyntus-recent-list">
      <div class="kyntus-recent-header">
        <h2 class="kyntus-recent-title">{{ title }}</h2>
        @if (viewAllRoute) {
          <a class="kyntus-recent-view-all" [routerLink]="viewAllRoute">{{ viewAllLabel }}</a>
        }
      </div>
      <div class="kyntus-recent-body">
        <ng-content select="[rows]" />
        @if (empty) {
          <p class="kyntus-recent-empty">{{ emptyMessage }}</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .kyntus-recent-list {
      padding: 1.25rem 1.5rem;
      border-radius: 0.75rem;
      border: 1px solid var(--border-color, #e2e8f0);
      background: var(--bg-card, #fff);
      box-shadow: var(--ky-shadow-sm, 0 1px 2px rgba(0, 0, 0, 0.06));
    }
    .kyntus-recent-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }
    .kyntus-recent-title {
      margin: 0;
      font-size: 0.875rem;
      font-weight: 600;
      color: var(--text-primary, #0f172a);
    }
    .kyntus-recent-view-all {
      font-size: 0.6875rem;
      font-weight: 600;
      color: var(--ky-accent, #3b82f6);
      text-decoration: none;
      white-space: nowrap;
    }
    .kyntus-recent-view-all:hover { text-decoration: underline; }
    .kyntus-recent-body {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .kyntus-recent-empty {
      margin: 0;
      font-size: 0.8125rem;
      color: var(--text-muted, #94a3b8);
    }
    :host ::ng-deep .kyntus-recent-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 0.625rem 0.75rem;
      border-radius: 0.5rem;
      border: 1px solid var(--border-color, #e2e8f0);
      background: var(--ky-surface-raised, var(--bg-input, #f8fafc));
    }
    :host ::ng-deep .kyntus-recent-row-main { min-width: 0; flex: 1; }
    :host ::ng-deep .kyntus-recent-row-title {
      margin: 0;
      font-size: 0.8125rem;
      font-weight: 500;
      color: var(--text-primary, #0f172a);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    :host ::ng-deep .kyntus-recent-row-meta {
      margin: 0.15rem 0 0;
      font-size: 0.6875rem;
      color: var(--text-muted, #94a3b8);
    }
    :host ::ng-deep .kyntus-recent-row-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-shrink: 0;
    }
    :host ::ng-deep .kyntus-recent-link {
      font-size: 0.6875rem;
      font-weight: 600;
      color: var(--ky-accent, #3b82f6);
      background: none;
      border: none;
      padding: 0;
      cursor: pointer;
      text-decoration: none;
      white-space: nowrap;
    }
    :host ::ng-deep .kyntus-recent-link:hover { text-decoration: underline; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusDashboardRecentListComponent {
  @Input({ required: true }) title!: string;
  @Input() emptyMessage = 'Aucun élément récent.';
  @Input() empty = false;
  @Input() viewAllRoute?: string;
  @Input() viewAllLabel = 'Voir tout';
}
