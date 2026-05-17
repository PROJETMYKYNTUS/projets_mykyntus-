import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { cn } from '@/lib/utils';

@Component({
  selector: 'app-prime-card',
  standalone: true,
  /** Permet aux utilitaires grille/flex du contenu (ex. col-span) de s’appliquer au bon élément. */
  host: { class: 'contents' },
  template: `
    <div [class]="cardClass">
      @if (title || description || hasAction) {
        <div class="px-6 py-4 border-b border-default flex justify-between items-center">
          <div>
            @if (title) {
              <h3 class="text-lg font-semibold text-primary">{{ title }}</h3>
            }
            @if (description) {
              <p class="text-sm text-muted mt-1">{{ description }}</p>
            }
          </div>
          @if (hasAction) {
            <div><ng-content select="[primeCardAction]" /></div>
          }
        </div>
      }
      <div class="p-6 text-primary">
        <ng-content />
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeCardComponent {
  @Input() title?: string;
  @Input() description?: string;
  @Input() className = '';
  @Input() hasAction = false;

  get cardClass(): string {
    return cn(
      'bg-card border border-default rounded-xl shadow-sm overflow-hidden min-w-0 w-full',
      this.className,
    );
  }
}
