import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { AlertTriangle } from 'lucide';
import { LucideIconComponent } from '../../lucide-icon.component';

@Component({
  selector: 'app-kyntus-error-state',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="kyntus-error-state" role="alert">
      <app-lucide-icon [icon]="alertIcon" className="kyntus-error-icon" />
      <p class="kyntus-error-msg">{{ message }}</p>
      @if (retryLabel) {
        <button type="button" class="kyntus-error-retry" (click)="retry.emit()">{{ retryLabel }}</button>
      }
    </div>
  `,
  styles: [`
    .kyntus-error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      padding: 1.25rem 1.5rem;
      border-radius: 0.75rem;
      border: 1px solid color-mix(in srgb, #ef4444 40%, transparent);
      background: color-mix(in srgb, #ef4444 8%, transparent);
      text-align: center;
    }
    :host ::ng-deep .kyntus-error-icon {
      width: 1.5rem;
      height: 1.5rem;
      color: #f87171;
    }
    .kyntus-error-msg {
      margin: 0;
      font-size: 0.875rem;
      color: #fecaca;
    }
    .kyntus-error-retry {
      border: 1px solid color-mix(in srgb, #ef4444 35%, transparent);
      background: transparent;
      color: #fca5a5;
      font-size: 0.8125rem;
      font-weight: 500;
      padding: 0.375rem 0.875rem;
      border-radius: 0.5rem;
      cursor: pointer;
    }
    .kyntus-error-retry:hover {
      background: color-mix(in srgb, #ef4444 12%, transparent);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusErrorStateComponent {
  @Input({ required: true }) message!: string;
  @Input() retryLabel = '';
  @Output() retry = new EventEmitter<void>();
  readonly alertIcon = AlertTriangle;
}
