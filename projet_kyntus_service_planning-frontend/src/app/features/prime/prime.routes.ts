import { Routes } from '@angular/router';

export const PRIME_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/prime-layout.component').then((m) => m.PrimeLayoutComponent),
  },
];
