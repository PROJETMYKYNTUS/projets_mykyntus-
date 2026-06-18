import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { KyntusToastService } from './kyntus-toast.service';

@Component({
  selector: 'app-kyntus-toast-host',
  standalone: true,
  template: `
    @if (toast.active(); as t) {
      <div class="kyntus-toast-wrap" role="status" aria-live="polite">
        <div class="kyntus-toast" [class.success]="t.kind === 'success'" [class.error]="t.kind === 'error'">
          <span class="kyntus-toast-text">{{ t.message }}</span>
          <button type="button" class="kyntus-toast-close" (click)="toast.dismiss()" aria-label="Fermer">×</button>
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
      display: flex;
      align-items: flex-start;
      gap: 0.75rem;
      padding: 0.75rem 1rem;
      border-radius: 0.625rem;
      border: 1px solid color-mix(in srgb, var(--electric-blue, #3b82f6) 35%, transparent);
      background: var(--bg-card, #0f172a);
      color: var(--text-primary, #f1f5f9);
      box-shadow: 0 12px 40px color-mix(in srgb, #000 35%, transparent);
      font-size: 0.875rem;
      animation: kyntus-toast-in 0.2s ease-out;
    }
    .kyntus-toast.success {
      border-color: color-mix(in srgb, #10b981 40%, transparent);
      background: color-mix(in srgb, #10b981 10%, var(--bg-card, #0f172a));
    }
    .kyntus-toast.error {
      border-color: color-mix(in srgb, #ef4444 40%, transparent);
      background: color-mix(in srgb, #ef4444 10%, var(--bg-card, #0f172a));
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
      from { opacity: 0; transform: translateY(-0.5rem); }
      to { opacity: 1; transform: translateY(0); }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusToastHostComponent {
  readonly toast = inject(KyntusToastService);
}
