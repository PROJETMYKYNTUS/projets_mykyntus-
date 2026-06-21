import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-kyntus-page-header',
  standalone: true,
  template: `
    <header class="kyntus-page-header">
      <div class="kyntus-page-header-text">
        <h1 class="ky-page-title">{{ title }}</h1>
        @if (subtitle) {
          <p class="ky-page-subtitle">{{ subtitle }}</p>
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
    }
    .kyntus-page-header-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-shrink: 0;
      flex-wrap: wrap;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusPageHeaderComponent {
  @Input({ required: true }) title!: string;
  @Input() subtitle = '';
}
