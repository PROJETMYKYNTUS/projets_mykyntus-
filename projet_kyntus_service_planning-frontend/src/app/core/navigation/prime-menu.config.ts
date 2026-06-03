import type { Role } from '../../features/prime/models';
import type {
  AdminSection,
  AuditSection,
  RpSection,
} from '../../features/prime/state/prime-section.service';
import type { MenuItem } from './microservices.config';

const PRIME_ROUTE = '/prime';

/** Entrées menu chemin (RH, Manager, Superviseur, Pilote, etc.). */
export const PRIME_PATH_MENU_ITEMS: Array<MenuItem & { primeRoles: Role[] }> = [
  {
    label: 'Tableau de bord',
    route: PRIME_ROUTE,
    primePath: '/',
    primeRoles: ['Admin', 'RH', 'RP', 'Chef de projet', 'Manager', 'Superviseur', 'Coach', 'Référent technique'],
  },
  {
    label: 'Mon tableau de bord',
    route: PRIME_ROUTE,
    primePath: '/employee/dashboard',
    primeRoles: ['Pilote'],
  },
  { label: 'Types de prime', route: PRIME_ROUTE, primePath: '/types', primeRoles: ['Admin'] },
  { label: 'Règles', route: PRIME_ROUTE, primePath: '/rules', primeRoles: ['Admin'] },
  {
    label: 'Résultats',
    route: PRIME_ROUTE,
    primePath: '/results',
    primeRoles: ['Admin', 'RH', 'RP', 'Chef de projet', 'Manager', 'Superviseur', 'Coach', 'Référent technique'],
  },
  {
    label: 'Affectations organisationnelles',
    route: PRIME_ROUTE,
    primePath: '/rh/organisation',
    primeRoles: ['RH', 'Admin'],
  },
  {
    label: 'Validation',
    route: PRIME_ROUTE,
    primePath: '/validation',
    primeRoles: ['Admin', 'RP', 'Chef de projet', 'Superviseur', 'Coach', 'Référent technique'],
  },
  {
    label: 'Suivi validation',
    route: PRIME_ROUTE,
    primePath: '/validation-history',
    primeRoles: ['Admin', 'RP', 'Chef de projet', 'Superviseur', 'Coach', 'Référent technique'],
  },
  {
    label: 'Synthèse globale PRIME',
    route: PRIME_ROUTE,
    primePath: '/global-pool',
    primeRoles: ['Admin', 'RH', 'Manager', 'Comptabilité'],
  },
  {
    label: 'Suivi synthèse',
    route: PRIME_ROUTE,
    primePath: '/synthesis-tracking',
    primeRoles: ['Admin', 'RH', 'Manager'],
  },
  { label: 'Historique', route: PRIME_ROUTE, primePath: '/history', primeRoles: ['Admin', 'RH', 'RP'] },
  {
    label: 'Performance équipe',
    route: PRIME_ROUTE,
    primePath: '/team-performance',
    primeRoles: ['Manager', 'Chef de projet', 'Superviseur', 'Coach', 'Référent technique'],
  },
  {
    label: 'Périmètre superviseur',
    route: PRIME_ROUTE,
    primePath: '/superviseur/scope',
    primeRoles: ['Superviseur'],
  },
  {
    label: 'Périmètre chef de projet',
    route: PRIME_ROUTE,
    primePath: '/chef-projet/scope',
    primeRoles: ['Chef de projet', 'RP'],
  },
  {
    label: 'Indicateurs PRIME (services / cellule)',
    route: PRIME_ROUTE,
    primePath: '/prime-cellule-indicateurs',
    primeRoles: ['Superviseur'],
  },
  {
    label: 'Partie commune (RACC / SAV)',
    route: PRIME_ROUTE,
    primePath: '/prime-saisie',
    primeRoles: ['Superviseur'],
  },
  {
    label: 'Partie personnalisée',
    route: PRIME_ROUTE,
    primePath: '/prime-fiches-pilotes',
    primeRoles: ['Superviseur'],
  },
  {
    label: 'Templates fiche PRIME',
    route: PRIME_ROUTE,
    primePath: '/template-manager',
    primeRoles: ['Superviseur', 'Admin'],
  },
  { label: 'Configuration', route: PRIME_ROUTE, primePath: '/configuration', primeRoles: ['Admin'] },
  { label: 'Mes primes', route: PRIME_ROUTE, primePath: '/employee/primes', primeRoles: ['Pilote'] },
  { label: 'Ma performance', route: PRIME_ROUTE, primePath: '/employee/performance', primeRoles: ['Pilote'] },
  {
    label: 'Notifications',
    route: PRIME_ROUTE,
    primePath: '/notifications',
    primeRoles: [
      'Admin',
      'RH',
      'RP',
      'Chef de projet',
      'Manager',
      'Superviseur',
      'Coach',
      'Référent technique',
      'Pilote',
      'Audit',
    ],
  },
  {
    label: 'Paramètres',
    route: PRIME_ROUTE,
    primePath: '/settings',
    primeRoles: [
      'Admin',
      'RH',
      'RP',
      'Chef de projet',
      'Manager',
      'Superviseur',
      'Coach',
      'Référent technique',
      'Pilote',
      'Audit',
    ],
  },
];

export const PRIME_RP_MENU_ITEMS: Array<MenuItem & { primeRoles: Role[] }> = [
  { label: 'Tableau de bord', route: PRIME_ROUTE, primeRpSection: 'dashboard', primeRoles: ['RP'] },
  { label: 'Performance équipe', route: PRIME_ROUTE, primeRpSection: 'performance', primeRoles: ['RP'] },
  { label: 'Validation finale', route: PRIME_ROUTE, primeRpSection: 'validation', primeRoles: ['RP'] },
  { label: 'Avancement fiches PRIME', route: PRIME_ROUTE, primeRpSection: 'suivi-projet', primeRoles: ['RP'] },
  { label: 'Notifications', route: PRIME_ROUTE, primeRpSection: 'notifications', primeRoles: ['RP'] },
  { label: 'Paramètres', route: PRIME_ROUTE, primeRpSection: 'settings', primeRoles: ['RP'] },
];

export const PRIME_ADMIN_MENU_ITEMS: Array<MenuItem & { primeRoles: Role[] }> = [
  { label: 'Dashboard système', route: PRIME_ROUTE, primeAdminSection: 'dashboard', primeRoles: ['Admin'] },
  { label: 'Gestion des accès', route: PRIME_ROUTE, primeAdminSection: 'access', primeRoles: ['Admin'] },
  { label: 'Configuration du flux', route: PRIME_ROUTE, primeAdminSection: 'workflows', primeRoles: ['Admin'] },
  { label: 'Supervision & logs', route: PRIME_ROUTE, primeAdminSection: 'logs', primeRoles: ['Admin'] },
  { label: 'Anomalies', route: PRIME_ROUTE, primeAdminSection: 'anomalies', primeRoles: ['Admin'] },
  { label: 'Notifications', route: PRIME_ROUTE, primeAdminSection: 'notifications', primeRoles: ['Admin'] },
  { label: 'Paramètres', route: PRIME_ROUTE, primeAdminSection: 'settings', primeRoles: ['Admin'] },
];

export const PRIME_AUDIT_MENU_ITEMS: Array<MenuItem & { primeRoles: Role[] }> = [
  { label: 'Journal d’audit', route: PRIME_ROUTE, primeAuditSection: 'journal', primeRoles: ['Audit'] },
  { label: 'Historique d’accès', route: PRIME_ROUTE, primeAuditSection: 'access-history', primeRoles: ['Audit'] },
  { label: 'Anomalies', route: PRIME_ROUTE, primeAuditSection: 'anomalies', primeRoles: ['Audit'] },
  { label: 'Reporting', route: PRIME_ROUTE, primeAuditSection: 'reporting', primeRoles: ['Audit'] },
  { label: 'Notifications', route: PRIME_ROUTE, primeAuditSection: 'notifications', primeRoles: ['Audit'] },
  { label: 'Paramètres', route: PRIME_ROUTE, primeAuditSection: 'settings', primeRoles: ['Audit'] },
];

export function buildPrimeMenuItemsForRole(role: Role): MenuItem[] {
  if (role === 'RP') {
    return PRIME_RP_MENU_ITEMS.filter((i) => i.primeRoles.includes(role));
  }
  if (role === 'Admin') {
    return PRIME_ADMIN_MENU_ITEMS.filter((i) => i.primeRoles.includes(role));
  }
  if (role === 'Audit') {
    return PRIME_AUDIT_MENU_ITEMS.filter((i) => i.primeRoles.includes(role));
  }
  return PRIME_PATH_MENU_ITEMS.filter((i) => i.primeRoles.includes(role));
}
