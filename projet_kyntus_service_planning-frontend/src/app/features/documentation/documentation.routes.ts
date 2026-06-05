import { Routes } from '@angular/router';

import { DocumentationShellComponent } from './components/documentation-shell/documentation-shell.component';

export const DOCUMENTATION_ROUTES: Routes = [
  {
    path: '',
    component: DocumentationShellComponent,
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./components/dashboard-home/dashboard-home.component').then(
            (m) => m.DashboardHomeComponent,
          ),
      },
      {
        path: 'my-docs',
        loadComponent: () =>
          import('./pages/my-documents-page.component').then((m) => m.MyDocumentsPageComponent),
      },
      {
        path: 'request',
        loadComponent: () =>
          import('./pages/request-document-page.component').then((m) => m.RequestDocumentPageComponent),
      },
      {
        path: 'tracking',
        loadComponent: () =>
          import('./pages/request-tracking-page.component').then((m) => m.RequestTrackingPageComponent),
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./pages/notifications-page.component').then((m) => m.NotificationsPageComponent),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./pages/settings-page.component').then((m) => m.SettingsPageComponent),
      },
      {
        path: 'team-docs',
        loadComponent: () =>
          import('./pages/team-documents-page.component').then((m) => m.TeamDocumentsPageComponent),
      },
      {
        path: 'team-requests',
        loadComponent: () =>
          import('./pages/team-requests-page.component').then((m) => m.TeamRequestsPageComponent),
      },
      {
        path: 'hr-mgmt',
        loadComponent: () =>
          import('./pages/hr-management-page.component').then((m) => m.HrManagementPageComponent),
      },
      {
        path: 'hr-doc-history',
        loadComponent: () =>
          import('./pages/hr-generated-history-page.component').then(
            (m) => m.HrGeneratedHistoryPageComponent,
          ),
      },
      {
        path: 'doc-gen',
        loadComponent: () =>
          import('./pages/doc-gen-page.component').then((m) => m.DocGenPageComponent),
      },
      {
        path: 'templates',
        loadComponent: () =>
          import('./pages/templates-page.component').then((m) => m.TemplatesPageComponent),
      },
      {
        path: 'admin-config',
        loadComponent: () =>
          import('./pages/admin-config-page.component').then((m) => m.AdminConfigPageComponent),
      },
      {
        path: 'doc-types',
        loadComponent: () =>
          import('./pages/admin-doc-types-page.component').then((m) => m.AdminDocTypesPageComponent),
      },
      {
        path: 'permissions',
        loadComponent: () =>
          import('./pages/admin-permissions-page.component').then((m) => m.AdminPermissionsPageComponent),
      },
      {
        path: 'workflow',
        loadComponent: () =>
          import('./pages/admin-workflow-page.component').then((m) => m.AdminWorkflowPageComponent),
      },
      {
        path: 'storage',
        loadComponent: () =>
          import('./pages/admin-storage-page.component').then((m) => m.AdminStoragePageComponent),
      },
      {
        path: 'audit-logs',
        loadComponent: () =>
          import('./pages/audit-logs-page.component').then((m) => m.AuditLogsPageComponent),
      },
      {
        path: 'access-history',
        loadComponent: () =>
          import('./pages/access-history-page.component').then((m) => m.AccessHistoryPageComponent),
      },
    ],
  },
];
