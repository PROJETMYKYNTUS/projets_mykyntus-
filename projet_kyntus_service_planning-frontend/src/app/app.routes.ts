import { Routes } from '@angular/router';
import { AuthCallbackComponent } from './component/pages/auth-callback.component';
import { AuthGuard } from './guard/guards/auth';

export const routes: Routes = [
  {
    path: 'auth-callback',
    component: AuthCallbackComponent
  },

  // ─── DASHBOARDS PAR RÔLE ───────────────────────────
  {
    path: 'dashboard',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./features/dashboard/pages/dashboard-home/dashboard-home.component')
        .then(m => m.DashboardHomeComponent)
  },
  
  {
    path: 'dashboard/manager',
    canActivate: [AuthGuard],
    data: { roles: ['Manager'] },
    loadComponent: () =>
      import('./features/dashboard/pages/dashboard-manager/dashboard-manager.component')
        .then(m => m.DashboardManagerComponent)
  },
  {
    path: 'dashboard/employee',
    canActivate: [AuthGuard],
    data: { roles: ['Employee'] },
    loadComponent: () =>
      import('./features/dashboard/pages/dashboard-employee/dashboard-employee.component')
        .then(m => m.DashboardEmployeeComponent)
  },

  // ─── ROUTES ADMIN UNIQUEMENT ───────────────────────
  {
    path: 'users',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadChildren: () =>
      import('./features/users/users-routing-module').then(m => m.UsersRoutingModule)
  },
  {
    path: 'import',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./features/users/pages/user-import/user-import.component')
        .then(m => m.UserImportComponent)
  },
  {
    path: 'floors',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-list/floor-list.component')
        .then(m => m.FloorListComponent)
  },
  {
    path: 'floors/create',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-form/floor-form.component')
        .then(m => m.FloorFormComponent)
  },
  {
    path: 'floors/edit/:id',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-form/floor-form.component')
        .then(m => m.FloorFormComponent)
  },
  {
    path: 'floors/:id',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-detail/floor-detail.component')
        .then(m => m.FloorDetailComponent)
  },
  {
    path: 'services',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadChildren: () =>
      import('./features/services/services-routing-modules').then(m => m.ServicesRoutingModule)
  },
  {
    path: 'sub-services',
    canActivate: [AuthGuard],
    data: { roles: ['Admin'] },
    loadChildren: () =>
      import('./features/sub-services/sub-services-routing-module').then(m => m.SubServicesRoutingModule)
  },

  // ─── ROUTES ADMIN + MANAGER ───────────────────────
  {
    path: 'contracts',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'Manager'] },
    loadChildren: () =>
      import('./features/contract/contract-routing-module').then(m => m.ContractRoutingModule)
  },
  {
    path: 'new-employees',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'Manager'] },
    loadComponent: () =>
      import('./features/planning/pages/new-employee-manager/new-employee-manager.component')
        .then(m => m.NewEmployeeManagerComponent)
  },
  {
    path: 'conge',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'Manager'] },
    loadComponent: () =>
      import('./features/planning/pages/conge-manager/conge-manager.component')
        .then(m => m.CongeManagerComponent)
  },

  // ─── ROUTES TOUS LES RÔLES ────────────────────────
  {
    path: 'planning',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'Manager', 'Employee'] },
    loadChildren: () =>
      import('./features/planning/planning-routing-module').then(m => m.PlanningRoutingModule)
  },

{ path: 'unauthorized', loadComponent: () =>
    import('./features/dashboard/pages/unauthorized.component')
      .then(m => m.UnauthorizedComponent)
},
  { path: '', redirectTo: 'auth-callback', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard/employee' }
];