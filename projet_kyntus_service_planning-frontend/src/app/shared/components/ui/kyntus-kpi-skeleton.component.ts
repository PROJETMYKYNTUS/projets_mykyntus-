import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-kyntus-kpi-skeleton',
  standalone: true,
  template: `
    <div class="kyntus-kpi-skeleton-grid" [style.--cols]="columns">
      @for (_ of slots; track $index) {
        <div class="kyntus-kpi-skeleton-card" aria-hidden="true">
          <div class="kyntus-kpi-skeleton-label"></div>
          <div class="kyntus-kpi-skeleton-value"></div>
        </div>
      }
    </div>
  `,
  styles: [`
    .kyntus-kpi-skeleton-grid {
      display: grid;
      grid-template-columns: repeat(var(--cols, 3), minmax(0, 1fr));
      gap: 1rem;
    }
    @media (max-width: 1280px) {
      .kyntus-kpi-skeleton-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 640px) {
      .kyntus-kpi-skeleton-grid { grid-template-columns: 1fr; }
    }
    .kyntus-kpi-skeleton-card {
      padding: 1rem 1.25rem;
      border-radius: 0.75rem;
      border: 1px solid var(--border-color, #e2e8f0);
      background: var(--bg-card, #fff);
    }
    .kyntus-kpi-skeleton-label,
    .kyntus-kpi-skeleton-value {
      border-radius: 0.375rem;
      background: linear-gradient(
        90deg,
        color-mix(in srgb, var(--border-color) 40%, var(--bg-card)) 0%,
        color-mix(in srgb, var(--border-color) 20%, var(--bg-card)) 50%,
        color-mix(in srgb, var(--border-color) 40%, var(--bg-card)) 100%
      );
      background-size: 200% 100%;
      animation: kyntus-shimmer 1.2s ease-in-out infinite;
    }
    .kyntus-kpi-skeleton-label {
      height: 0.625rem;
      width: 55%;
      margin-bottom: 0.5rem;
    }
    .kyntus-kpi-skeleton-value {
      height: 1.5rem;
      width: 35%;
    }
    @keyframes kyntus-shimmer {
      0% { background-position: 100% 0; }
      100% { background-position: -100% 0; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusKpiSkeletonComponent {
  @Input() columns = 3;
  @Input() count = 6;

  get slots(): number[] {
    return Array.from({ length: this.count }, (_, i) => i);
  }
}
