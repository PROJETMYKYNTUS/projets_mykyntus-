import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/sub-service-list/sub-service-list.component').then(m => m.SubServiceListComponent) },
  { path: 'create', loadComponent: () => import('./pages/sub-service-form/sub-service-form.component').then(m => m.SubServiceFormComponent) },
  { path: 'edit/:id', loadComponent: () => import('./pages/sub-service-form/sub-service-form.component').then(m => m.SubServiceFormComponent) },
  { path: ':id', loadComponent: () => import('./pages/sub-service-detail/sub-service-detail.component').then(m => m.SubServiceDetailComponent) },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SubServicesRoutingModule {}