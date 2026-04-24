import { Routes } from '@angular/router';
import { AuthCallbackComponent } from './component/pages/auth-callback.component';
import { AuthGuard } from './guard/guards/auth';
import { NewsletterAdminComponent } from './features/Newsletter-admin/newsletter-admin.component';

export const routes: Routes = [

  // ─── AUTH ─────────────────────────────────────────
  {
    path: 'auth-callback',
    component: AuthCallbackComponent
  },

  // ─── DASHBOARDS PAR RÔLE ──────────────────────────
  {
    path: 'dashboard',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./features/dashboard/pages/dashboard-home/dashboard-home.component')
        .then(m => m.DashboardHomeComponent)
  },
  {
    path: 'dashboard-employee',            // ✅ corrigé : dashboard-employee
    canActivate: [AuthGuard],
    data: { roles: ['Employee', 'Manager', 'Coach', 'RP', 'Audit', 'Equipe_Formation'] },
    loadComponent: () =>
      import('./features/dashboard/pages/dashboard-employee/dashboard-employee.component')
        .then(m => m.DashboardEmployeeComponent)
  },
{
    path: 'reclamations',
    canActivate: [AuthGuard],
    data: {
      roles: ['employee','RH','Manager','Coach','RP','Admin','Audit','Equipe_Formation']
    },
    loadComponent: () =>
      import('./features/reclamation/employee/reclamation-employee.component')
        .then(m => m.ReclamationEmployeeComponent)
  },
 
  // Vue admin/gestion (RH, Manager, RP, Admin, Audit)
  {
    path: 'reclamations-admin',
    canActivate: [AuthGuard],
    data: { roles: ['RH','Manager','RP','Admin','Audit'] },
    loadComponent: () =>
      import('./features/reclamation/admin/reclamation-admin.component')
        .then(m => m.ReclamationAdminComponent)
  },
  
  // ─── NEWSLETTER — Admin + RH ──────────────────────
  {
    path: 'newsletter',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },      // ✅ RH ajouté
    component: NewsletterAdminComponent
  },

  // ─── ORGANISATION — Admin + RH ────────────────────
  {
    path: 'floors',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-list/floor-list.component')
        .then(m => m.FloorListComponent)
  },
  {
    path: 'floors/create',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-form/floor-form.component')
        .then(m => m.FloorFormComponent)
  },
  {
    path: 'floors/edit/:id',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-form/floor-form.component')
        .then(m => m.FloorFormComponent)
  },
  {
    path: 'floors/:id',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./features/floors/pages/floor-detail/floor-detail.component')
        .then(m => m.FloorDetailComponent)
  },
  {
    path: 'services',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadChildren: () =>
      import('./features/services/services-routing-modules')
        .then(m => m.ServicesRoutingModule)
  },
  {
    path: 'sub-services',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadChildren: () =>
      import('./features/sub-services/sub-services-routing-module')
        .then(m => m.SubServicesRoutingModule)
  },

  // ─── RH — Employés & Imports ──────────────────────
  {
    path: 'users',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },      // ✅ RH gère les employés
    loadChildren: () =>
      import('./features/users/users-routing-module')
        .then(m => m.UsersRoutingModule)
  },
  {
    path: 'import',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH'] },
    loadComponent: () =>
      import('./features/users/pages/user-import/user-import.component')
        .then(m => m.UserImportComponent)
  },

  // ─── CONTRATS & CONGÉS — Admin + RH + Manager ─────
  {
    path: 'contracts',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH', 'Manager'] },
    loadChildren: () =>
      import('./features/contract/contract-routing-module')
        .then(m => m.ContractRoutingModule)
  },
  {
    path: 'new-employees',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH', 'Manager'] },
    loadComponent: () =>
      import('./features/planning/pages/new-employee-manager/new-employee-manager.component')
        .then(m => m.NewEmployeeManagerComponent)
  },
  {
    path: 'conge',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH', 'Manager'] },
    loadComponent: () =>
      import('./features/planning/pages/conge-manager/conge-manager.component')
        .then(m => m.CongeManagerComponent)
  },

  // ─── PLANNING — tous les rôles ────────────────────
  {
    path: 'planning',
    canActivate: [AuthGuard],
    data: { roles: ['Admin', 'RH', 'Manager', 'Coach', 'RP', 'Pilote', 'Audit', 'Equipe_Formation'] }, // ✅ tous les vrais rôles
    loadChildren: () =>
      import('./features/planning/planning-routing-module')
        .then(m => m.PlanningRoutingModule)
  },

  // ─── AUTRES ───────────────────────────────────────
  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./features/dashboard/pages/unauthorized.component')
        .then(m => m.UnauthorizedComponent)
  },

  { path: '', redirectTo: 'auth-callback', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard-employee' }   // ✅ corrigé
];