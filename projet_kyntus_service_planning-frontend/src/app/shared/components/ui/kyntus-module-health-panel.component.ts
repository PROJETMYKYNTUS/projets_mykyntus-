import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { Router } from '@angular/router';
import type { ModuleHealthStatus } from '../../../core/dashboard/global-dashboard.model';

@Component({
  selector: 'app-kyntus-module-health-panel',
  standalone: true,
  template: `
    <div class="kyntus-health-panel">
      <h2 class="kyntus-health-title">{{ title }}</h2>
      @if (items.length === 0) {
        <p class="kyntus-health-empty">{{ emptyMessage }}</p>
      } @else {
        <ul class="kyntus-health-list">
          @for (item of items; track item.moduleId) {
            <li
              class="kyntus-health-item"
              [class.kyntus-health-clickable]="isClickable(item)"
              [attr.tabindex]="isClickable(item) ? 0 : null"
              [attr.role]="isClickable(item) ? 'button' : null"
              (click)="open(item)"
              (keydown.enter)="open(item)"
            >
              <span class="kyntus-health-dot" [class]="item.severity"></span>
              <div class="kyntus-health-body">
                <p class="kyntus-health-label">{{ item.label }}</p>
                <p class="kyntus-health-detail">{{ item.detail }}</p>
              </div>
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .kyntus-health-panel {
      padding: 1.25rem 1.5rem;
      border-radius: 0.75rem;
      border: 1px solid var(--border-color, #e2e8f0);
      background: var(--bg-card, #fff);
      box-shadow: var(--ky-shadow-sm, 0 1px 2px rgba(0, 0, 0, 0.06));
    }
    .kyntus-health-title {
      margin: 0 0 0.75rem;
      font-size: 0.875rem;
      font-weight: 600;
      color: var(--text-primary, #0f172a);
    }
    .kyntus-health-empty {
      margin: 0;
      font-size: 0.8125rem;
      color: var(--text-muted, #94a3b8);
    }
    .kyntus-health-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.625rem;
    }
    .kyntus-health-item {
      display: flex;
      align-items: flex-start;
      gap: 0.625rem;
      padding: 0.5rem 0;
      border-bottom: 1px solid color-mix(in srgb, var(--border-color) 65%, transparent);
    }
    .kyntus-health-item.kyntus-health-clickable {
      cursor: pointer;
      border-radius: 0.375rem;
      margin: 0 -0.35rem;
      padding: 0.5rem 0.35rem;
    }
    .kyntus-health-item.kyntus-health-clickable:hover {
      background: color-mix(in srgb, var(--ky-accent, #3b82f6) 6%, transparent);
    }
    .kyntus-health-item.kyntus-health-clickable:focus-visible {
      outline: 2px solid var(--ky-accent, #3b82f6);
      outline-offset: 1px;
    }
    .kyntus-health-item:last-child {
      border-bottom: none;
      padding-bottom: 0;
    }
    .kyntus-health-dot {
      flex-shrink: 0;
      width: 0.5rem;
      height: 0.5rem;
      border-radius: 9999px;
      margin-top: 0.35rem;
      background: var(--text-muted, #64748b);
    }
    .kyntus-health-dot.ok { background: var(--success, #16a34a); }
    .kyntus-health-dot.warn { background: var(--warning, #d97706); }
    .kyntus-health-dot.error { background: var(--danger, #dc2626); }
    .kyntus-health-dot.neutral { background: var(--text-muted, #64748b); }
    .kyntus-health-body { min-width: 0; }
    .kyntus-health-label {
      margin: 0;
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--text-primary, #0f172a);
    }
    .kyntus-health-detail {
      margin: 0.15rem 0 0;
      font-size: 0.6875rem;
      color: var(--text-muted, #94a3b8);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusModuleHealthPanelComponent {
  private readonly router = inject(Router);

  @Input({ required: true }) title = 'Santé des modules';
  @Input() items: ModuleHealthStatus[] = [];
  @Input() emptyMessage = 'Aucun module à afficher.';

  isClickable(item: ModuleHealthStatus): boolean {
    return !!(item.action || item.route);
  }

  open(item: ModuleHealthStatus): void {
    if (item.action) {
      item.action();
      return;
    }
    if (item.route) {
      void this.router.navigate([item.route], { queryParams: item.queryParams });
    }
  }
}
