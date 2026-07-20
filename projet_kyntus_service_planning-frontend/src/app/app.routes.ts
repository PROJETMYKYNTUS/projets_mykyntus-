import { Routes } from '@angular/router';
import { AuthCallbackComponent } from './component/pages/auth-callback.component';
import { AuthGuard } from './guard/guards/auth';
import { NewsletterAdminComponent } from './features/Newsletter-admin/newsletter-admin.component';
import { ShellLayoutComponent } from './features/shell/shell-layout.component';

export const routes: Routes = [

  // ─── HORS SHELL (auth / erreurs) ──────────────────
  {
    path: 'auth-callback',
    component: AuthCallbackComponent,
  },
  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./features/dashboard/pages/unauthorized.component')
        .then(m => m.UnauthorizedComponent),
  },

  // ─── ANCIENS DASHBOARDS (layout propre, accès direct conservé) ──
  { path: 'dashboard', redirectTo: 'home', pathMatch: 'full' },
  { path: 'dashboard-employee', redirectTo: 'home', pathMatch: 'full' },

  // ─── SHELL UNIFIÉ (menu latéral global persistant) ──
  {
    path: '',
    component: ShellLayoutComponent,
    canActivate: [AuthGuard],
    children: [

      // Accueil — lanceur de microservices
      {
        path: 'home',
        loadComponent: () =>
          import('./features/dashboard/pages/unified-dashboard/unified-dashboard.component')
            .then(m => m.UnifiedDashboardComponent),
      },

      // Notifications et paramètres — centre unifié plateforme
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/shell/pages/notifications-center/notifications-center.component')
            .then(m => m.NotificationsCenterComponent),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/shell/pages/global-settings/global-settings.component')
            .then(m => m.GlobalSettingsComponent),
      },

      // ─── QUALITÉ & AMÉLIORATION ──────────────────
      {
        path: 'reclamations',
        canActivate: [AuthGuard],
        data: { roles: ['employee', 'RH', 'Manager', 'Coach', 'RP', 'Admin', 'Audit', 'Equipe_Formation', 'Superviseur'] },
        loadComponent: () =>
          import('./features/reclamation/employee/reclamation-employee.component')
            .then(m => m.ReclamationEmployeeComponent),
      },
      {
        path: 'reclamations-admin',
        canActivate: [AuthGuard],
        data: { roles: ['RH', 'Manager', 'RP', 'Admin', 'Audit'] },
        loadComponent: () =>
          import('./features/reclamation/admin/reclamation-admin.component')
            .then(m => m.ReclamationAdminComponent),
      },

      // ─── FORMATION ───────────────────────────────
      {
        path: 'formations',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Formateur', 'Equipe_Formation', 'Equipe formation'] },
        loadComponent: () =>
          import('./features/formation/admin/formation-admin.component')
            .then(m => m.FormationAdminComponent),
      },
      {
        path: 'formations/planifier',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        loadComponent: () =>
          import('./features/formation/rh/formation-rh-plan.component')
            .then(m => m.FormationRhPlanComponent),
      },
      {
        path: 'formations/initiales',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Formateur', 'Equipe_Formation', 'Equipe formation'] },
        loadComponent: () =>
          import('./features/formation/formateur/formation-formateur-initial.component')
            .then(m => m.FormationFormateurInitialComponent),
      },
      {
        path: 'formations/passage-production',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        loadComponent: () =>
          import('./features/formation/rh/formation-rh-prod-queue.component')
            .then(m => m.FormationRhProdQueueComponent),
      },
      {
        path: 'mes-sessions',
        canActivate: [AuthGuard],
        data: {
          roles: [
            'Employee',
            'Manager',
            'Coach',
            'RP',
            'Audit',
            'Formateur',
            'Equipe_Formation',
            'Equipe formation',
            'Superviseur',
            'Admin',
            'RH',
          ],
        },
        loadComponent: () =>
          import('./features/formation/sessions/formation-mes-sessions.component')
            .then(m => m.FormationMesSessionsComponent),
      },
      {
        path: 'mes-formations',
        canActivate: [AuthGuard],
        data: {
          roles: [
            'Employee',
            'Manager',
            'Coach',
            'RP',
            'Audit',
            'Formateur',
            'Equipe_Formation',
            'Equipe formation',
            'Superviseur',
          ],
        },
        loadComponent: () =>
          import('./features/formation/employee/formation-employee.component')
            .then(m => m.FormationEmployeeComponent),
      },

      // ─── NEWSLETTER ──────────────────────────────
      {
        path: 'newsletter',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        component: NewsletterAdminComponent,
      },
      {
        path: 'mes-newsletters',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Manager', 'Coach', 'RP', 'Pilote', 'Audit', 'Equipe_Formation', 'Employee', 'Superviseur'] },
        loadComponent: () =>
          import('./features/newsletter-inbox/my-newsletters-page.component')
            .then(m => m.MyNewslettersPageComponent),
      },

      // ─── ORGANISATION ────────────────────────────
      {
        path: 'organisation',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        loadComponent: () =>
          import('./features/prime/pages/organisation-management.component')
            .then((m) => m.OrganisationManagementComponent),
      },
      {
        path: 'departements-metier',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        loadComponent: () =>
          import('./features/prime/pages/allowances/business-departments-page.component')
            .then((m) => m.BusinessDepartmentsPageComponent),
      },
      {
        path: 'floors',
        redirectTo: '/organisation?tab=departments',
        pathMatch: 'prefix',
      },
      {
        path: 'services',
        redirectTo: '/organisation?tab=poles',
        pathMatch: 'prefix',
      },
      {
        path: 'sub-services',
        redirectTo: '/organisation?tab=cellules',
        pathMatch: 'prefix',
      },

      // ─── RH — Employés & Imports ─────────────────
      {
        path: 'pilotage-rh',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        loadComponent: () =>
          import('./features/rh/pilotage-rh/pilotage-rh.component')
            .then(m => m.PilotageRhComponent),
      },
      {
        path: 'users',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        loadChildren: () =>
          import('./features/users/users-routing-module')
            .then(m => m.UsersRoutingModule),
      },
      {
        path: 'import',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH'] },
        loadComponent: () =>
          import('./features/users/pages/employee-import-guided/employee-import-guided.component')
            .then(m => m.EmployeeImportGuidedComponent),
      },

      // ─── CONTRATS & CONGÉS ───────────────────────
      {
        path: 'contracts',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Manager'] },
        loadChildren: () =>
          import('./features/contract/contract-routing-module')
            .then(m => m.ContractRoutingModule),
      },
      {
        path: 'new-employees',
        redirectTo: 'users',
        pathMatch: 'full',
      },
      {
        path: 'conge',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Manager'] },
        loadComponent: () =>
          import('./features/planning/pages/conge-manager/conge-manager.component')
            .then(m => m.CongeManagerComponent),
      },
      {
        path: 'conge-gestion',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Manager'] },
        loadComponent: () =>
          import('./features/conge/pages/conge-manager/conge-manager.component')
            .then(m => m.CongeManagerComponent),
      },
      {
        path: 'conge-historique',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Manager'] },
        loadComponent: () =>
          import('./features/conge/pages/conge-historique/conge-historique.component')
            .then(m => m.CongeHistoriqueComponent),
      },
      {
        path: 'mes-conges',
        canActivate: [AuthGuard],
        data: { roles: ['Employee', 'Manager', 'Coach', 'RP', 'Audit', 'Equipe_Formation', 'Superviseur'] },
        loadComponent: () =>
          import('./features/conge/pages/conge-employe/conge-employe.component')
            .then(m => m.CongeEmployeComponent),
      },

      // ─── PLANNING ────────────────────────────────
      {
        path: 'mes-plannings',
        canActivate: [AuthGuard],
        data: {
          roles: [
            'Employee',
            'Pilote',
            'Manager',
            'Coach',
            'Référent technique',
            'RP',
            'Audit',
            'Equipe_Formation',
            'Superviseur',
          ],
        },
        loadComponent: () =>
          import('./features/planning/pages/mes-plannings/mes-plannings.component')
            .then(m => m.MesPlanningsComponent),
      },
      {
        path: 'planning',
        canActivate: [AuthGuard],
        data: {
          roles: [
            'Admin',
            'RH',
            'Manager',
            'Coach',
            'Référent technique',
            'RP',
            'Pilote',
            'Audit',
            'Equipe_Formation',
            'Superviseur',
            'Employee',
          ],
        },
        loadChildren: () =>
          import('./features/planning/planning-routing-module')
            .then(m => m.PlanningRoutingModule),
      },

      // ─── DOCUMENTATION (microservice intégré, lazy) ──
      {
        path: 'documentation',
        canActivate: [AuthGuard],
        data: {
          roles: ['Admin', 'RH', 'Employee', 'employee', 'Manager', 'Coach', 'RP', 'Pilote', 'Audit', 'Equipe_Formation', 'Equipe formation', 'Superviseur'],
        },
        loadChildren: () =>
          import('./features/documentation/documentation.module')
            .then(m => m.DocumentationModule),
      },

      // ─── PRIME ───────────────────────────────────
      {
        path: 'prime',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Manager', 'Coach', 'RP', 'Pilote', 'Audit', 'Employee', 'Superviseur'] },
        loadChildren: () =>
          import('./features/prime/prime.routes').then((m) => m.PRIME_ROUTES),
      },

      // ─── PARRAINAGE ──────────────────────────────
      {
        path: 'parrainage',
        canActivate: [AuthGuard],
        data: { roles: ['Admin', 'RH', 'Manager', 'Pilote', 'Employee', 'Audit', 'Coach', 'RP', 'Superviseur'] },
        loadChildren: () =>
          import('./features/parrainage/parrainage.routes').then((m) => m.PARRAINAGE_ROUTES),
      },

      { path: '', redirectTo: 'home', pathMatch: 'full' },
    ],
  },

  { path: '**', redirectTo: 'home' },
];
