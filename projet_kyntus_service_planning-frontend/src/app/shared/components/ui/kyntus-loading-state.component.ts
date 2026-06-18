import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Loader2 } from 'lucide';
import { LucideIconComponent } from '../../lucide-icon.component';

@Component({
  selector: 'app-kyntus-loading-state',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="kyntus-loading-state" [class.compact]="compact" role="status" aria-live="polite" aria-busy="true">
      <app-lucide-icon [icon]="spinnerIcon" className="kyntus-loading-icon" />
      <span class="kyntus-loading-text">{{ message }}</span>
    </div>
  `,
  styles: [`
    .kyntus-loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.75rem;
      padding: 2.5rem 1.5rem;
      text-align: center;
    }
    .kyntus-loading-state.compact {
      flex-direction: row;
      padding: 1rem;
      justify-content: flex-start;
    }
    :host ::ng-deep .kyntus-loading-icon {
      width: 1.75rem;
      height: 1.75rem;
      color: var(--electric-blue, #3b82f6);
      animation: kyntus-spin 0.9s linear infinite;
    }
    .kyntus-loading-state.compact :host ::ng-deep .kyntus-loading-icon {
      width: 1.125rem;
      height: 1.125rem;
    }
    .kyntus-loading-text {
      font-size: 0.875rem;
      color: var(--text-muted, #94a3b8);
    }
    @keyframes kyntus-spin {
      to { transform: rotate(360deg); }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusLoadingStateComponent {
  @Input() message = 'Chargement…';
  @Input() compact = false;
  readonly spinnerIcon = Loader2;
}
