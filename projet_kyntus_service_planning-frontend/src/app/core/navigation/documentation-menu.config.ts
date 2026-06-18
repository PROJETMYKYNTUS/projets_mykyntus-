import type { DocumentationRole } from '../../features/documentation/interfaces/documentation-role';
import type { DocumentationTabId } from '../../features/documentation/services/documentation-navigation.service';
import type { AuditInterfaceSectionId } from '../../features/documentation/services/audit-interface-nav.service';
import { DOCUMENTATION_ROUTE_BASE } from '../../features/documentation/lib/documentation-route-base';
import type { MenuItem } from './microservices.config';

const DOC_ROUTE = DOCUMENTATION_ROUTE_BASE;

const NAV_LABELS: Record<string, string> = {
  'nav.dashboard': 'Tableau de bord',
  'nav.myDocs': 'Mes documents',
  'nav.requestDoc': 'Demande de document',
  'nav.requestTracking': 'Suivi des demandes',
  'nav.notifications': 'Notifications',
  'nav.settings': 'Paramètres',
  'nav.teamDocs': 'Documents de l’équipe',
  'nav.teamRequests': 'Demandes de l’équipe',
  'nav.allRequests': 'Demandes RH',
  'nav.hrDocHistory': 'Historique',
  'nav.docGen': 'Génération de documents',
  'nav.templates': 'Modèles',
  'nav.adminConfig': 'Configuration',
  'nav.docTypes': 'Types de documents',
  'nav.permissions': 'Permissions',
  'nav.storage': 'Stockage',
  'nav.accessHistory': 'Historique d’accès',
};

function docPath(tab: DocumentationTabId): string {
  if (tab === 'dashboard') return DOC_ROUTE;
  return `${DOC_ROUTE}/${tab}`;
}

function item(labelKey: string, tab: DocumentationTabId): MenuItem {
  return {
    label: NAV_LABELS[labelKey] ?? labelKey,
    route: docPath(tab),
    documentationTab: tab,
  };
}

const PILOTE_ITEMS: MenuItem[] = [
  item('nav.dashboard', 'dashboard'),
  item('nav.myDocs', 'my-docs'),
  item('nav.requestDoc', 'request'),
  item('nav.requestTracking', 'tracking'),
];

const MANAGER_ITEMS: MenuItem[] = [
  item('nav.teamDocs', 'team-docs'),
  item('nav.teamRequests', 'team-requests'),
];

const RH_ITEMS: MenuItem[] = [
  item('nav.allRequests', 'hr-mgmt'),
  item('nav.hrDocHistory', 'hr-doc-history'),
  item('nav.docGen', 'doc-gen'),
  item('nav.templates', 'templates'),
];

const RP_ITEMS: MenuItem[] = [
  item('nav.dashboard', 'dashboard'),
  item('nav.teamDocs', 'team-docs'),
  item('nav.allRequests', 'hr-mgmt'),
  item('nav.hrDocHistory', 'hr-doc-history'),
];

const ADMIN_ITEMS: MenuItem[] = [
  item('nav.adminConfig', 'admin-config'),
  item('nav.docTypes', 'doc-types'),
  item('nav.permissions', 'permissions'),
  item('nav.storage', 'storage'),
];

const AUDIT_ITEMS: MenuItem[] = [
  {
    label: 'Journal d’audit',
    route: docPath('audit-logs'),
    documentationTab: 'audit-logs',
    documentationAuditSection: 'journal',
  },
  {
    label: NAV_LABELS['nav.accessHistory'],
    route: docPath('access-history'),
    documentationTab: 'access-history',
    documentationAuditSection: 'access-history',
  },
  {
    label: 'Anomalies',
    route: docPath('audit-logs'),
    documentationTab: 'audit-logs',
    documentationAuditSection: 'anomalies',
  },
  {
    label: 'Reporting',
    route: docPath('audit-logs'),
    documentationTab: 'audit-logs',
    documentationAuditSection: 'reporting',
  },
];

export function buildDocumentationMenuItemsForRole(role: DocumentationRole): MenuItem[] {
  switch (role) {
    case 'Pilote':
      return PILOTE_ITEMS;
    case 'Manager':
    case 'Coach':
      return MANAGER_ITEMS;
    case 'RH':
      return RH_ITEMS;
    case 'RP':
      return RP_ITEMS;
    case 'Admin':
      return ADMIN_ITEMS;
    case 'Audit':
      return AUDIT_ITEMS;
    default:
      return PILOTE_ITEMS;
  }
}

export { mapJwtRoleToDocumentationRole } from '../session/kyntus-role-ui.config';
