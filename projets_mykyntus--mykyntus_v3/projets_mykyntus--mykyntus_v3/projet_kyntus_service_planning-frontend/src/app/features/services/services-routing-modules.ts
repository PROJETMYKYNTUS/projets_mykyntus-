import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/service-list/service-list.component').then(m => m.ServiceListComponent) },
  { path: 'create', loadComponent: () => import('./pages/service-form/service-form.component').then(m => m.ServiceFormComponent) },
  { path: 'edit/:id', loadComponent: () => import('./pages/service-form/service-form.component').then(m => m.ServiceFormComponent) },
  { path: ':id', loadComponent: () => import('./pages/service-detail/service-detail.component').then(m => m.ServiceDetailComponent) },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ServicesRoutingModule {}