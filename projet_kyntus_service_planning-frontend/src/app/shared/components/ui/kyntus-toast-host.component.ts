import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { KyntusToastService } from './kyntus-toast.service';

@Component({
  selector: 'app-kyntus-toast-host',
  standalone: true,
  template: `
    @if (toast.active(); as t) {
      <div class="kyntus-toast-wrap" role="status" aria-live="polite">
        <div
          class="kyntus-toast"
          [class.success]="t.kind === 'success'"
          [class.error]="t.kind === 'error'"
          [style.--toast-duration]="t.durationMs + 'ms'"
        >
          <span class="kyntus-toast-text">{{ t.message }}</span>
          <button type="button" class="kyntus-toast-close" (click)="toast.dismiss()" aria-label="Fermer">×</button>
          <span class="kyntus-toast-progress" aria-hidden="true"></span>
        </div>
      </div>
    }
  `,
  styles: [`
    .kyntus-toast-wrap {
      position: fixed;
      top: 1rem;
      right: 1rem;
      z-index: 10000;
      max-width: min(24rem, calc(100vw - 2rem));
    }
    .kyntus-toast {
      position: relative;
      overflow: hidden;
      display: flex;
      align-items: flex-start;
      gap: 0.75rem;
      padding: 0.75rem 1rem;
      border-radius: var(--radius-md, 0.5rem);
      border: 1px solid color-mix(in srgb, var(--electric-blue, #3b82f6) 35%, transparent);
      background: var(--bg-card, #0f172a);
      color: var(--text-primary, #f1f5f9);
      box-shadow: 0 12px 40px color-mix(in srgb, #000 35%, transparent);
      font-size: 0.875rem;
      animation: kyntus-toast-in 0.25s var(--ease-out, ease-out);
    }
    .kyntus-toast-progress {
      position: absolute;
      left: 0;
      right: 0;
      bottom: 0;
      height: 2px;
      background: color-mix(in srgb, var(--electric-blue, #3b82f6) 70%, transparent);
      transform-origin: left;
      animation: kyntus-toast-progress var(--toast-duration, 4s) linear forwards;
    }
    .kyntus-toast.success .kyntus-toast-progress {
      background: color-mix(in srgb, var(--success, #16a34a) 80%, transparent);
    }
    .kyntus-toast.error .kyntus-toast-progress {
      background: color-mix(in srgb, var(--danger, #dc2626) 80%, transparent);
    }
    @keyframes kyntus-toast-progress {
      from { transform: scaleX(1); }
      to { transform: scaleX(0); }
    }
    .kyntus-toast.success {
      border-color: var(--success-border);
      background: var(--success-bg);
    }
    .kyntus-toast.error {
      border-color: var(--danger-border);
      background: var(--danger-bg);
    }
    .kyntus-toast-text { flex: 1; line-height: 1.4; }
    .kyntus-toast-close {
      border: none;
      background: transparent;
      color: var(--text-muted, #94a3b8);
      font-size: 1.25rem;
      line-height: 1;
      cursor: pointer;
      padding: 0;
    }
    @keyframes kyntus-toast-in {
      from { opacity: 0; transform: translateX(1rem); }
      to { opacity: 1; transform: translateX(0); }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusToastHostComponent {
  readonly toast = inject(KyntusToastService);
}
