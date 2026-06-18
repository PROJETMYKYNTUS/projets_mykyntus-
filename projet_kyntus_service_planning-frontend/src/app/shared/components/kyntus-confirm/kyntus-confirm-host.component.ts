import { ChangeDetectionStrategy, Component, HostListener, inject } from '@angular/core';
import { LucideIconComponent } from '../../lucide-icon.component';
import { AlertTriangle, Info, Trash2 } from 'lucide';
import { KyntusConfirmService } from './kyntus-confirm.service';

@Component({
  selector: 'app-kyntus-confirm-host',
  standalone: true,
  imports: [LucideIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (dialog(); as d) {
      <div class="ky-confirm-overlay" (click)="confirm.reject()" role="presentation">
        <div
          class="ky-confirm-box ky-card"
          role="alertdialog"
          aria-modal="true"
          [attr.aria-labelledby]="'ky-confirm-title'"
          (click)="$event.stopPropagation()"
        >
          <div class="ky-confirm-header">
            <div class="ky-confirm-icon" [class]="'ky-confirm-icon--' + d.variant">
              <app-lucide-icon [icon]="iconFor(d.variant)" className="w-6 h-6" />
            </div>
            <h2 id="ky-confirm-title" class="ky-confirm-title">{{ d.title }}</h2>
          </div>

          <div class="ky-confirm-body">
            <p class="ky-confirm-message">{{ d.message }}</p>
          </div>

          <div class="ky-confirm-footer">
            <button type="button" class="ky-btn-secondary" (click)="confirm.reject()">
              {{ d.cancelLabel }}
            </button>
            <button
              type="button"
              class="ky-btn-primary"
              [class.ky-btn-danger]="d.variant === 'danger'"
              (click)="confirm.accept()"
            >
              {{ d.confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .ky-confirm-overlay {
      position: fixed;
      inset: 0;
      z-index: 10000;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
      background: color-mix(in srgb, var(--navy-950, #0f172a) 55%, transparent);
      backdrop-filter: blur(8px);
    }

    .ky-confirm-box {
      width: min(100%, 440px);
      border-radius: 24px;
      overflow: hidden;
      box-shadow: 0 24px 64px color-mix(in srgb, #000 28%, transparent);
      animation: ky-confirm-in 0.18s ease-out;
    }

    @keyframes ky-confirm-in {
      from {
        opacity: 0;
        transform: translateY(12px) scale(0.98);
      }
      to {
        opacity: 1;
        transform: translateY(0) scale(1);
      }
    }

    .ky-confirm-header {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 22px 24px 0;
    }

    .ky-confirm-icon {
      width: 44px;
      height: 44px;
      border-radius: 14px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .ky-confirm-icon--warning {
      color: #f59e0b;
      background: color-mix(in srgb, #f59e0b 14%, transparent);
      border: 1px solid color-mix(in srgb, #f59e0b 28%, transparent);
    }

    .ky-confirm-icon--danger {
      color: #ef4444;
      background: color-mix(in srgb, #ef4444 12%, transparent);
      border: 1px solid color-mix(in srgb, #ef4444 24%, transparent);
    }

    .ky-confirm-icon--default {
      color: var(--accent-primary, #6366f1);
      background: color-mix(in srgb, var(--accent-primary, #6366f1) 12%, transparent);
      border: 1px solid color-mix(in srgb, var(--accent-primary, #6366f1) 24%, transparent);
    }

    .ky-confirm-title {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 800;
      color: var(--text-primary);
      line-height: 1.3;
    }

    .ky-confirm-body {
      padding: 16px 24px 8px;
    }

    .ky-confirm-message {
      margin: 0;
      color: var(--text-secondary, var(--text-muted));
      font-size: 0.95rem;
      line-height: 1.55;
    }

    .ky-confirm-footer {
      display: flex;
      justify-content: flex-end;
      gap: 12px;
      padding: 18px 24px 24px;
    }

    .ky-btn-danger {
      background: color-mix(in srgb, #ef4444 88%, #000) !important;
      border-color: color-mix(in srgb, #ef4444 70%, transparent) !important;
    }

    @media (max-width: 480px) {
      .ky-confirm-footer {
        flex-direction: column-reverse;
      }

      .ky-confirm-footer button {
        width: 100%;
      }
    }
  `],
})
export class KyntusConfirmHostComponent {
  readonly confirm = inject(KyntusConfirmService);
  readonly dialog = this.confirm.state;

  readonly icons = { warning: AlertTriangle, danger: Trash2, default: Info };

  iconFor(variant: 'warning' | 'danger' | 'default') {
    return this.icons[variant];
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.dialog()) {
      this.confirm.reject();
    }
  }
}
