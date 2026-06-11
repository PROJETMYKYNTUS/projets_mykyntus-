// features/planning/planning-routing-module.ts

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/planning-generate/planning-generate.component')
        .then(m => m.PlanningGenerateComponent)
  },
  {
  path: 'saturday-history',
  loadComponent: () => import('./pages/saturday-history/saturday-history.component')
    .then(m => m.SaturdayHistoryComponent)
},
 {
    path: 'shift-config',                                    // ✅ loadComponent
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
}
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class PlanningRoutingModule {}