import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  template: `
    <div class="flex flex-col items-center justify-center h-full p-8 bg-navy-950 min-h-screen">
      <div class="max-w-md text-center bg-card rounded-2xl shadow-sm border border-default p-8">
        <h1 class="text-2xl font-bold text-primary mb-2">
          Accès au module PRIME refusé
        </h1>
        <p class="text-muted mb-4">
          Votre rôle ne permet pas d'accéder au module de gestion des primes.
        </p>
        <p class="text-sm text-muted">
          Merci de contacter un administrateur ou le service RH si vous pensez qu'il s'agit d'une erreur.
        </p>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessDeniedComponent {}
