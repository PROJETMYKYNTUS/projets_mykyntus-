import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  template: `
    <div class="flex flex-col items-center justify-center h-full p-8 bg-slate-50">
      <div class="max-w-md text-center bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
        <h1 class="text-2xl font-bold text-slate-900 mb-2">
          Accès au module PRIME refusé
        </h1>
        <p class="text-slate-600 mb-4">
          Votre rôle ne permet pas d'accéder au module de gestion des primes.
        </p>
        <p class="text-sm text-slate-500">
          Merci de contacter un administrateur ou le service RH si vous pensez qu'il s'agit d'une erreur.
        </p>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessDeniedComponent {}
