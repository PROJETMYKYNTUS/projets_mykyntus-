import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { KyntusPageHeaderComponent } from './kyntus-page-header.component';

@Component({
  selector: 'app-kyntus-rules-admin-page',
  standalone: true,
  imports: [KyntusPageHeaderComponent],
  template: `
    <section class="kyntus-rules-admin ky-page-shell">
      <app-kyntus-page-header [title]="title" [subtitle]="subtitle" [eyebrow]="eyebrow">
        <ng-content select="[headerActions]" actions />
      </app-kyntus-page-header>

      <div class="kyntus-rules-grid">
        <div class="kyntus-rules-list">
          <ng-content select="[list]" />
        </div>
        <div class="kyntus-rules-editor">
          <ng-content select="[editor]" />
        </div>
      </div>
    </section>
  `,
  styles: [`
    .kyntus-rules-admin {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }
    .kyntus-rules-grid {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr);
      gap: 1rem;
      align-items: start;
    }
    @media (max-width: 1024px) {
      .kyntus-rules-grid {
        grid-template-columns: 1fr;
      }
    }
    .kyntus-rules-list,
    .kyntus-rules-editor {
      border-radius: 0.75rem;
      border: 1px solid var(--border-default, #1e293b);
      background: var(--bg-card, #0f172a);
      min-height: 12rem;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusRulesAdminPageComponent {
  @Input({ required: true }) title!: string;
  @Input() subtitle = '';
  @Input() eyebrow = '';
}
