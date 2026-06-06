import type { AdminSection, AuditSection, RpSection } from '../../features/prime/state/prime-section.service';
import type { ParrainageView } from '../../features/parrainage/state/parrainage-nav.service';
import type { AuditSectionId } from '../../features/parrainage/state/audit-section.service';
import type { DocumentationTabId } from '../../features/documentation/services/documentation-navigation.service';
import type { AuditInterfaceSectionId } from '../../features/documentation/services/audit-interface-nav.service';

/**
 * Configuration centrale du menu latéral global des microservices.
 *
 * Chaque entrée de premier niveau = un microservice (groupe). En cliquant dessus,
 * on déplie son menu détaillé (children). La visibilité est filtrée par rôle :
 * - un item est visible si `roles` est vide/absent (tous), ou si le rôle courant y figure.
 * - un groupe est visible s'il a au moins un enfant visible (ou si `roles` au niveau groupe l'autorise).
 *
 * Rôles connus (claim JWT) : Admin, RH, Manager, Coach, RP, Pilote, Audit, Equipe_Formation, Employee, Superviseur.
 */

export interface MenuItem {
  label: string;
  /** Lien interne (routerLink) au sein de la SPA. */
  route?: string;
  /** Lien externe (module non encore fusionné en lazy-loading) — ouvert dans un nouvel onglet. */
  externalUrl?: string;
  /** Rôles JWT autorisés. Vide/absent = visible par tous les utilisateurs authentifiés. */
  roles?: string[];
  /** Navigation interne Prime (vue signal, URL reste /prime). */
  primePath?: string;
  primeAdminSection?: AdminSection;
  primeRpSection?: RpSection;
  primeAuditSection?: AuditSection;
  /** Navigation interne Parrainage (vue signal, URL reste /parrainage). */
  parrainageView?: ParrainageView;
  parrainageAuditSection?: AuditSectionId;
  /** Onglet documentation (routes /documentation/...). */
  documentationTab?: DocumentationTabId;
  documentationAuditSection?: AuditInterfaceSectionId;
}

export interface Microservice {
  id: string;
  label: string;
  /** SVG complet (rendu via innerHTML sécurisé dans le shell). */
  icon: string;
  /** Rôles autorisés au niveau du groupe (optionnel — sinon dérivé des enfants). */
  roles?: string[];
  /** Enfants générés dynamiquement par NavigationMenuService (Prime, Parrainage). */
  dynamicChildren?: boolean;
  children: MenuItem[];
}
const ALL_ROLES = ['Admin', 'RH', 'Manager', 'Coach', 'RP', 'Pilote', 'Audit', 'Equipe_Formation', 'Employee', 'Superviseur'];
const MANAGER_ROLES = ['Admin', 'RH', 'Manager'];
const ADMIN_RH = ['Admin', 'RH'];
const EMPLOYEE_ROLES = ['Employee', 'Manager', 'Coach', 'RP', 'Audit', 'Equipe_Formation', 'Superviseur'];

const ICONS = {
  grid: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>',
  building: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>',
  users: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75"/></svg>',
  calendar: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>',
  leave: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.5 19H9a7 7 0 117-7v1"/><path d="M17 9l4-4-4-4M21 5H9"/></svg>',
  graduation: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>',
  mail: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>',
  chat: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/></svg>',
  doc: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>',
  award: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="8" r="6"/><path d="M15.477 12.89L17 22l-5-3-5 3 1.523-9.11"/></svg>',
  share: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><line x1="8.59" y1="13.51" x2="15.42" y2="17.49"/><line x1="15.41" y1="6.51" x2="8.59" y2="10.49"/></svg>',
};

export const MICROSERVICES: Microservice[] = [
  {
    id: 'organisation',
    label: 'Organisation',
    icon: ICONS.building,
    roles: ADMIN_RH,
    children: [
      { label: 'Étages', route: '/floors', roles: ADMIN_RH },
      { label: 'Services', route: '/services', roles: ADMIN_RH },
      { label: 'Sous-services', route: '/sub-services', roles: ADMIN_RH },
    ],
  },
  {
    id: 'rh',
    label: 'Ressources Humaines',
    icon: ICONS.users,
    children: [
      { label: 'Employés', route: '/users', roles: ADMIN_RH },
      { label: 'Import employés', route: '/import', roles: ADMIN_RH },
      { label: 'Contrats', route: '/contracts', roles: MANAGER_ROLES },
      { label: 'Nouveaux Employés', route: '/new-employees', roles: MANAGER_ROLES },
    ],
  },
  {
    id: 'conges',
    label: 'Congés',
    icon: ICONS.leave,
    children: [
      { label: 'Gestion des congés', route: '/conge-gestion', roles: MANAGER_ROLES },
      { label: 'Congés (ancien)', route: '/conge', roles: MANAGER_ROLES },
      { label: 'Mes congés', route: '/mes-conges', roles: EMPLOYEE_ROLES },
    ],
  },
  {
    id: 'planning',
    label: 'Planification',
    icon: ICONS.calendar,
    roles: ['Admin', 'RH', 'Manager', 'Coach', 'RP', 'Pilote', 'Audit', 'Equipe_Formation'],
    children: [
      { label: 'Plannings', route: '/planning' },
      { label: 'Configuration Shifts', route: '/planning/shift-config' },
      { label: 'Historique Samedis', route: '/planning/saturday-history' },
    ],
  },
  {
    id: 'formation',
    label: 'Formation',
    icon: ICONS.graduation,
    children: [
      { label: 'Gestion des formations', route: '/formations', roles: ['Admin', 'RH', 'Equipe_Formation'] },
      { label: 'Mes formations', route: '/mes-formations', roles: EMPLOYEE_ROLES },
    ],
  },
  {
    id: 'communication',
    label: 'Communication',
    icon: ICONS.mail,
    roles: ADMIN_RH,
    children: [
      { label: 'Newsletters', route: '/newsletter', roles: ADMIN_RH },
    ],
  },
  {
    id: 'qualite',
    label: 'Qualité & Amélioration',
    icon: ICONS.chat,
    children: [
      { label: 'Réclamations (gestion)', route: '/reclamations-admin', roles: ['RH', 'Manager', 'RP', 'Admin', 'Audit'] },
      { label: 'Mes réclamations', route: '/reclamations', roles: ALL_ROLES },
    ],
  },
  {
    id: 'documentation',
    label: 'Documentation',
    icon: ICONS.doc,
    dynamicChildren: true,
    children: [],
  },
  {
    id: 'prime',
    label: 'Prime',
    icon: ICONS.award,
    roles: ['Admin', 'RH', 'Manager', 'Coach', 'RP', 'Pilote', 'Audit', 'Employee', 'Superviseur'],
    dynamicChildren: true,
    children: [],
  },
  {
    id: 'parrainage',
    label: 'Parrainage',
    icon: ICONS.share,
    roles: ['Admin', 'RH', 'Manager', 'Pilote', 'Audit', 'Coach', 'RP', 'Employee', 'Superviseur'],
    dynamicChildren: true,
    children: [],
  },
];
