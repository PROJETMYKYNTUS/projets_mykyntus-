import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { X } from 'lucide';
import { LucideIconComponent } from '../../lucide-icon.component';

export interface KyntusAuditField {
  label: string;
  value: string;
  mono?: boolean;
}

@Component({
  selector: 'app-kyntus-audit-drawer',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    @if (open) {
      <div class="kyntus-audit-drawer-overlay" (click)="close.emit()">
        <aside class="kyntus-audit-drawer" (click)="$event.stopPropagation()">
          <div class="kyntus-audit-drawer-head">
            <h3 class="kyntus-audit-drawer-title">{{ title }}</h3>
            <button type="button" class="kyntus-audit-drawer-close" (click)="close.emit()" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="w-4 h-4" />
            </button>
          </div>
          <div class="kyntus-audit-drawer-body">
            @for (field of fields; track field.label) {
              <div class="kyntus-audit-field">
                <span class="kyntus-audit-field-label">{{ field.label }}</span>
                <p class="kyntus-audit-field-value" [class.mono]="field.mono">{{ field.value }}</p>
              </div>
            }
            <ng-content />
          </div>
          @if (showActions) {
            <div class="kyntus-audit-drawer-actions">
              <ng-content select="[actions]" />
            </div>
          }
        </aside>
      </div>
    }
  `,
  styles: [`
    .kyntus-audit-drawer-overlay {
      position: fixed;
      inset: 0;
      z-index: 50;
      display: flex;
      justify-content: flex-end;
      background: color-mix(in srgb, var(--bg-app, #020617) 55%, transparent);
    }
    .kyntus-audit-drawer {
      width: 100%;
      max-width: 28rem;
      height: 100%;
      display: flex;
      flex-direction: column;
      border-left: 1px solid var(--border-default, #1e293b);
      background: var(--bg-input, #0f172a);
      box-shadow: -8px 0 32px rgba(0, 0, 0, 0.35);
    }
    .kyntus-audit-drawer-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 1.25rem 1.25rem 0.75rem;
    }
    .kyntus-audit-drawer-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 600;
      color: var(--text-primary, #f8fafc);
    }
    .kyntus-audit-drawer-close {
      display: flex;
      padding: 0.35rem;
      border: none;
      border-radius: 0.375rem;
      background: transparent;
      color: var(--text-muted, #94a3b8);
      cursor: pointer;
    }
    .kyntus-audit-drawer-close:hover {
      background: var(--bg-card, #0f172a);
      color: var(--text-primary, #f8fafc);
    }
    .kyntus-audit-drawer-body {
      flex: 1;
      overflow-y: auto;
      padding: 0.75rem 1.25rem 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.85rem;
    }
    .kyntus-audit-field-label {
      display: block;
      font-size: 0.6875rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--text-muted, #94a3b8);
      margin-bottom: 0.2rem;
    }
    .kyntus-audit-field-value {
      margin: 0;
      font-size: 0.875rem;
      color: var(--text-primary, #f8fafc);
      word-break: break-word;
    }
    .kyntus-audit-field-value.mono {
      font-family: ui-monospace, monospace;
      font-size: 0.8125rem;
    }
    .kyntus-audit-drawer-actions {
      padding: 0.75rem 1.25rem 1.25rem;
      border-top: 1px solid var(--border-default, #1e293b);
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .kyntus-audit-drawer-actions:empty {
      display: none;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusAuditDrawerComponent {
  @Input() open = false;
  @Input({ required: true }) title = 'Détail';
  @Input() fields: KyntusAuditField[] = [];
  @Input() showActions = true;
  @Output() close = new EventEmitter<void>();

  readonly xIcon = X;
}
