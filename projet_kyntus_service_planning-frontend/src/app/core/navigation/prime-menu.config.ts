import type { Role } from '../../features/prime/models';
import type {
  AdminSection,
  AuditSection,
  RpSection,
} from '../../features/prime/state/prime-section.service';
import { SUPPORT_MANAGER_ALLOWED_PATHS } from '../../features/prime/lib/prime-nav-access';
import {
  OPERATIONAL_MANAGER_ALLOWED_PATHS,
  resolveManagerPrimeTrack,
} from '../../features/prime/lib/prime-manager-track';
import type { PrimeDepartmentManagerNav } from '../../features/prime/lib/prime-manager-nav';
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
    label: 'Import fiche prête',
    route: PRIME_ROUTE,
    primePath: '/prime-fiche-import',
    primeRoles: ['Superviseur', 'Admin'],
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
];

/** Ordre menu manager Support : action principale en premier. */
const SUPPORT_MANAGER_MENU_ORDER: readonly string[] = [
  '/allowances/allocation',
  '/allowances/dashboard',
  '/allowances/progress',
  '/allowances/history',
];

function sortAllowanceMenuItems(items: MenuItem[]): MenuItem[] {
  return [...items].sort((a, b) => {
    const ai = SUPPORT_MANAGER_MENU_ORDER.indexOf(a.primePath ?? '');
    const bi = SUPPORT_MANAGER_MENU_ORDER.indexOf(b.primePath ?? '');
    const aOrder = ai >= 0 ? ai : 99;
    const bOrder = bi >= 0 ? bi : 99;
    if (aOrder !== bOrder) return aOrder - bOrder;
    return (a.label ?? '').localeCompare(b.label ?? '', 'fr');
  });
}

/** Track Allowances — départements Support (module Parrainage exclu). */
export const PRIME_ALLOWANCE_MENU_ITEMS: Array<
  MenuItem & { primeRoles: Role[]; departmentKinds?: ('Support' | 'Operational')[] }
> = [
  {
    label: 'Affectation équipe',
    route: PRIME_ROUTE,
    primePath: '/allowances/allocation',
    primeRoles: ['Manager'],
    departmentKinds: ['Support'],
  },
  {
    label: 'Tableau de bord',
    route: PRIME_ROUTE,
    primePath: '/allowances/dashboard',
    primeRoles: ['Manager'],
    departmentKinds: ['Support'],
  },
  {
    label: 'Avancement de traitement',
    route: PRIME_ROUTE,
    primePath: '/allowances/progress',
    primeRoles: ['Manager'],
    departmentKinds: ['Support'],
  },
  {
    label: 'Historique',
    route: PRIME_ROUTE,
    primePath: '/allowances/history',
    primeRoles: ['Manager'],
    departmentKinds: ['Support'],
  },
  {
    label: 'Synthèse',
    route: PRIME_ROUTE,
    primePath: '/allowances',
    primeRoles: ['RH', 'Admin'],
    departmentKinds: ['Support'],
  },
  {
    label: 'Validation RH',
    route: PRIME_ROUTE,
    primePath: '/allowances/inbox',
    primeRoles: ['RH', 'Comptabilité'],
  },
  {
    label: 'Mes primes reçues',
    route: PRIME_ROUTE,
    primePath: '/allowances/my',
    primeRoles: ['Pilote'],
    departmentKinds: ['Support'],
  },
  {
    label: 'Suivi global',
    route: PRIME_ROUTE,
    primePath: '/allowances/supervision',
    primeRoles: ['RH', 'Admin'],
  },
  {
    label: 'Types de prime',
    route: PRIME_ROUTE,
    primePath: '/allowances/catalog',
    primeRoles: ['RH', 'Admin'],
  },
];

export const PRIME_RP_MENU_ITEMS: Array<MenuItem & { primeRoles: Role[] }> = [
  { label: 'Tableau de bord', route: PRIME_ROUTE, primeRpSection: 'dashboard', primeRoles: ['RP'] },
  { label: 'Performance équipe', route: PRIME_ROUTE, primeRpSection: 'performance', primeRoles: ['RP'] },
  { label: 'Validation finale', route: PRIME_ROUTE, primeRpSection: 'validation', primeRoles: ['RP'] },
  { label: 'Avancement fiches PRIME', route: PRIME_ROUTE, primeRpSection: 'suivi-projet', primeRoles: ['RP'] },
];

export const PRIME_ADMIN_MENU_ITEMS: Array<MenuItem & { primeRoles: Role[] }> = [
  { label: 'Dashboard système', route: PRIME_ROUTE, primeAdminSection: 'dashboard', primeRoles: ['Admin'] },
  { label: 'Gestion des accès', route: PRIME_ROUTE, primeAdminSection: 'access', primeRoles: ['Admin'] },
  { label: 'Configuration du flux', route: PRIME_ROUTE, primeAdminSection: 'workflows', primeRoles: ['Admin'] },
  { label: 'Supervision & logs', route: PRIME_ROUTE, primeAdminSection: 'logs', primeRoles: ['Admin'] },
  { label: 'Anomalies', route: PRIME_ROUTE, primeAdminSection: 'anomalies', primeRoles: ['Admin'] },
];

export const PRIME_AUDIT_MENU_ITEMS: Array<MenuItem & { primeRoles: Role[] }> = [
  { label: 'Journal d’audit', route: PRIME_ROUTE, primeAuditSection: 'journal', primeRoles: ['Audit'] },
  { label: 'Historique d’accès', route: PRIME_ROUTE, primeAuditSection: 'access-history', primeRoles: ['Audit'] },
  { label: 'Anomalies', route: PRIME_ROUTE, primeAuditSection: 'anomalies', primeRoles: ['Audit'] },
  { label: 'Reporting', route: PRIME_ROUTE, primeAuditSection: 'reporting', primeRoles: ['Audit'] },
];

export function buildPrimeMenuItemsForRole(
  role: Role,
  departmentKind: 'Support' | 'Operational' | null = null,
  managerNav: PrimeDepartmentManagerNav = { isSupportManager: false, isOperationalManager: false },
): MenuItem[] {
  const { isSupportManager, isOperationalManager } = managerNav;
  const managerTrack = resolveManagerPrimeTrack(role, managerNav);

  if (role === 'Manager' && managerTrack === 'support') {
    return withAllowanceSection(
      sortAllowanceMenuItems(
        PRIME_ALLOWANCE_MENU_ITEMS.filter((i) => {
          if (!i.primeRoles.includes(role)) return false;
          const p = i.primePath;
          return !!p && (SUPPORT_MANAGER_ALLOWED_PATHS as readonly string[]).includes(p);
        }),
      ),
    );
  }

  if (role === 'Manager' && managerTrack === 'operational') {
    return PRIME_PATH_MENU_ITEMS.filter((i) => {
      if (!i.primeRoles.includes(role)) return false;
      const p = i.primePath;
      return !!p && (OPERATIONAL_MANAGER_ALLOWED_PATHS as readonly string[]).includes(p);
    });
  }

  if (role === 'RP') {
    return PRIME_RP_MENU_ITEMS.filter((i) => i.primeRoles.includes(role));
  }
  if (role === 'Admin') {
    return withAllowanceSection([
      ...PRIME_ADMIN_MENU_ITEMS.filter((i) => i.primeRoles.includes(role)),
      ...PRIME_ALLOWANCE_MENU_ITEMS.filter((i) => i.primeRoles.includes(role)),
    ]);
  }
  if (role === 'Audit') {
    return PRIME_AUDIT_MENU_ITEMS.filter((i) => i.primeRoles.includes(role));
  }

  const base = PRIME_PATH_MENU_ITEMS.filter((i) => i.primeRoles.includes(role));
  const allowances = PRIME_ALLOWANCE_MENU_ITEMS.filter((i) => {
    if (!i.primeRoles.includes(role)) return false;
    if (!i.departmentKinds?.length) return true;
    if (!departmentKind) return role === 'RH' || role === 'Comptabilité';
    return i.departmentKinds.includes(departmentKind);
  });

  let merged = [...base, ...allowances];

  if (role === 'Manager' && isOperationalManager) {
    merged = merged.filter((i) => !i.primePath?.startsWith('/allowances'));
  }

  if (departmentKind === 'Operational') {
    merged = merged.filter((i) => !i.primePath?.startsWith('/allowances'));
  }

  return withAllowanceSection(merged);
}

function withAllowanceSection(items: MenuItem[]): MenuItem[] {
  const idx = items.findIndex((i) => i.primePath?.startsWith('/allowances'));
  if (idx < 0) return items;
  if (items[idx - 1]?.isSectionHeader) return items;
  const header: MenuItem = {
    label: 'Primes Support',
    route: PRIME_ROUTE,
    isSectionHeader: true,
  };
  return [...items.slice(0, idx), header, ...items.slice(idx)];
}
