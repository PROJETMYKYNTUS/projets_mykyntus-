// features/contract/contract-routing-module.ts
// ✅ VERSION MISE À JOUR — ajoute la route notifications

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  // ── Liste des contrats ──
  {
    path: '',
    loadComponent: () =>
      import('./pages/contract-list/contract-list.component')
        .then(m => m.ContractListComponent)
  },
  // ── 🆕 Liste des notifications ──
  {
    path: 'notifications',
    loadComponent: () =>
      import('./pages/Notification-list/Notification-list.component')
        .then(m => m.NotificationListComponent)
  },
  // ── Nouveau contrat ──
  {
    path: 'new',
    loadComponent: () =>
      import('./pages/contract-form/contract-form.component')
        .then(m => m.ContractFormComponent)
  },
  // ── Détail contrat ──
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/contract-detail/contract-detail.component')
        .then(m => m.ContractDetailComponent)
  },
  // ── Modifier contrat ──
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./pages/contract-form/contract-form.component')
        .then(m => m.ContractFormComponent)
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ContractRoutingModule {}