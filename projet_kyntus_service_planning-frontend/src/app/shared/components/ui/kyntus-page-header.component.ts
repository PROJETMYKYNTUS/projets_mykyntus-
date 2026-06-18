import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-kyntus-page-header',
  standalone: true,
  template: `
    <header class="kyntus-page-header">
      <div class="kyntus-page-header-text">
        @if (eyebrow) {
          <span class="kyntus-page-eyebrow">{{ eyebrow }}</span>
        }
        <h1 class="kyntus-page-title">{{ title }}</h1>
        @if (subtitle) {
          <p class="kyntus-page-subtitle">{{ subtitle }}</p>
        }
      </div>
      <div class="kyntus-page-header-actions">
        <ng-content select="[actions]" />
      </div>
    </header>
  `,
  styles: [`
    .kyntus-page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 1.5rem;
    }
    .kyntus-page-eyebrow {
      display: block;
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--electric-blue, #3b82f6);
      margin-bottom: 0.35rem;
    }
    .kyntus-page-title {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 600;
      color: var(--text-primary, #f8fafc);
      line-height: 1.25;
    }
    .kyntus-page-subtitle {
      margin: 0.35rem 0 0;
      font-size: 0.875rem;
      color: var(--text-muted, #94a3b8);
    }
    .kyntus-page-header-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-shrink: 0;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusPageHeaderComponent {
  @Input({ required: true }) title!: string;
  @Input() subtitle = '';
  @Input() eyebrow = '';
}
