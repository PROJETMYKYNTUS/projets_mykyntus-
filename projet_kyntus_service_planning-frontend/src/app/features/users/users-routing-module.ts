import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/user-list/user-list.component').then(m => m.UserListComponent) },
  { path: 'create', loadComponent: () => import('./pages/user-form/user-form.component').then(m => m.UserFormComponent) },
  { path: 'edit/:id', loadComponent: () => import('./pages/user-form/user-form.component').then(m => m.UserFormComponent) },
  {
    path: 'import',
    loadComponent: () => import('./pages/user-import/user-import.component')
      .then(m => m.UserImportComponent)
  },
  { path: ':id', loadComponent: () => import('./pages/user-detail/user-detail.component').then(m => m.UserDetailComponent) },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class UsersRoutingModule {}