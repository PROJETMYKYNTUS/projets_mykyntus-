import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { BodyPortalDirective } from '@/shared/directives/body-portal.directive';
import { X } from 'lucide';
import { cn } from '@/lib/utils';

@Component({
  selector: 'app-prime-modal',
  standalone: true,
  imports: [LucideIconComponent, BodyPortalDirective],
  template: `
    @if (isOpen) {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-0" appBodyPortal>
        <div
          class="fixed inset-0 bg-card/50 backdrop-blur-sm transition-opacity"
          (click)="onClose.emit()"
        ></div>

        <div [class]="modalClass">
          <div class="flex items-center justify-between px-6 py-4 border-b border-default">
            <h3 class="text-lg font-semibold text-primary">{{ title }}</h3>
            <button
              type="button"
              (click)="onClose.emit()"
              class="p-1.5 text-muted hover:text-primary hover:bg-app rounded-lg transition-colors"
            >
              <app-lucide-icon [icon]="icons.x" className="w-5 h-5" />
            </button>
          </div>

          <div class="p-6 overflow-y-auto text-primary">
            <ng-content />
          </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeModalComponent {
  @Input() isOpen = false;
  @Input() title = '';
  @Input() className = '';
  @Output() onClose = new EventEmitter<void>();

  readonly icons = { x: X };

  get modalClass(): string {
    return cn(
      'relative bg-card border border-default rounded-2xl shadow-xl w-full max-w-lg overflow-hidden flex flex-col max-h-[90vh]',
      this.className,
    );
  }
}
