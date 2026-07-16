import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Inbox } from 'lucide';
import { LucideIconComponent } from '../../lucide-icon.component';

@Component({
  selector: 'app-kyntus-empty-state',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="kyntus-empty-state" role="status">
      <app-lucide-icon [icon]="icon" className="kyntus-empty-icon" />
      <p class="kyntus-empty-title">{{ title }}</p>
      @if (description) {
        <p class="kyntus-empty-desc">{{ description }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: [`
    .kyntus-empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 4rem 1.5rem;
      text-align: center;
      border: 1px dashed color-mix(in srgb, var(--border-color, #334155) 70%, transparent);
      border-radius: 0.75rem;
      background: color-mix(in srgb, var(--bg-card, #0f172a) 50%, transparent);
      animation: kyntus-empty-in 0.3s var(--ease-out, ease-out) both;
    }
    @keyframes kyntus-empty-in {
      from {
        opacity: 0;
        transform: translateY(8px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }
    :host ::ng-deep .kyntus-empty-icon {
      width: 2.5rem;
      height: 2.5rem;
      color: var(--text-muted, #64748b);
      opacity: 0.7;
    }
    .kyntus-empty-title {
      margin: 0;
      font-size: 0.9375rem;
      font-weight: 600;
      color: var(--text-primary, #f1f5f9);
    }
    .kyntus-empty-desc {
      margin: 0;
      font-size: 0.8125rem;
      color: var(--text-muted, #94a3b8);
      max-width: 28rem;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusEmptyStateComponent {
  @Input() title = 'Aucune donnée';
  @Input() description = '';
  @Input() icon = Inbox;
}
