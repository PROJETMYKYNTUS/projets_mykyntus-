// features/planning/planning-routing-module.ts

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from '../../guard/guards/auth';

const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/planning-generate/planning-generate.component')
        .then(m => m.PlanningGenerateComponent)
  },
  {
    path: 'validation',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./pages/planning-validation/planning-validation.component')
        .then(m => m.PlanningValidationComponent),
  },
  {
    path: 'saturday-history',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./pages/saturday-history/saturday-history.component')
        .then(m => m.SaturdayHistoryComponent)
  },
  {
    path: 'shift-config',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./pages/shift-config/shift-config.component')
        .then(m => m.ShiftConfigComponent)
  },
  {
    path: 'view/:id',
    loadComponent: () =>
      import('./pages/planning-view/planning-view.component')
        .then(m => m.PlanningViewComponent)
  },
  {
    path: 'conges',
    loadComponent: () =>
      import('./pages/conge-manager/conge-manager.component')
        .then(m => m.CongeManagerComponent)
  },
  {
    path: 'equipe',
    canActivate: [AuthGuard],
    data: { roles: ['Manager', 'Coach', 'Référent technique', 'Superviseur', 'Admin', 'RH'] },
    loadComponent: () =>
      import('./pages/planning-equipe/planning-equipe.component')
        .then(m => m.PlanningEquipeComponent),
  },
  {
    path: 'change-requests',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./pages/planning-change-requests/planning-change-requests.component')
        .then(m => m.PlanningChangeRequestsComponent),
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class PlanningRoutingModule {}
