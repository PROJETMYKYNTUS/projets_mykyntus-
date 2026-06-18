import { Routes } from '@angular/router';

export const PARRAINAGE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/parrainage-layout.component').then((m) => m.ParrainageLayoutComponent),
  },
];
