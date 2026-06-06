import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  template: `
    <section class="flex-1 flex items-center justify-center p-8">
      <div class="card-navy p-10 max-w-md text-center space-y-4">
        <h2 class="text-xl font-semibold text-red-200">Accès refusé</h2>
        <p class="text-sm text-muted">{{ message }}</p>
        @if (backLabel) {
          <span class="inline-block text-sm text-soft-blue font-medium">{{ backLabel }}</span>
        }
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessDeniedComponent {
  @Input() message = 'Accès refusé. Cette section est réservée aux rôles Admin et RH.';
  @Input() backLabel = 'Retour au tableau de bord';
}
