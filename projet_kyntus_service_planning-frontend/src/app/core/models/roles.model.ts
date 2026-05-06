export enum Role {
  EMPLOYEE        = 'employee',
  RH              = 'RH',
  MANAGER         = 'Manager',
  COACH           = 'Coach',
  RP              = 'RP',
  ADMIN           = 'Admin',
  AUDIT           = 'Audit',
  EQUIPE_FORMATION= 'Equipe_Formation'
}
 
const ALL_ROLES = [
  Role.EMPLOYEE, Role.RH, Role.MANAGER, Role.COACH,
  Role.RP, Role.ADMIN, Role.AUDIT, Role.EQUIPE_FORMATION
];
 
export const PERMISSIONS: Record<string, Record<string, Role[]>> = {
 
  newsletter: {
    receive:   ALL_ROLES,
    create:    [Role.RH, Role.ADMIN],
    analytics: [Role.RH, Role.ADMIN],
    history:   [Role.RH, Role.ADMIN],
  },
 
  dashboard: {
    admin:    [Role.RH, Role.ADMIN],
    employee: [Role.EMPLOYEE, Role.MANAGER, Role.COACH, Role.RP, Role.AUDIT, Role.EQUIPE_FORMATION],
  },
 
  planning: {
    view:   ALL_ROLES,
    create: [Role.RH, Role.MANAGER, Role.ADMIN],
  },
 
  // ── NOUVEAU ─────────────────────────────────────────────
  reclamation: {
    // Tout le monde peut soumettre et suivre ses propres demandes
    soumettre: ALL_ROLES,
    suivre:    ALL_ROLES,
 
    // Traitement et gestion
    traiter:   [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN],
    assigner:  [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN],
    prioriser: [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN],
 
    // Reporting
    reporting: [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN, Role.AUDIT],
 
    // Audit complet
    historique:[Role.RP, Role.ADMIN, Role.AUDIT],
  },
 
  proposition: {
    soumettre: ALL_ROLES,
    suivre:    ALL_ROLES,
    evaluer:   [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN],
    assigner:  [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN],
    prioriser: [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN],
    reporting: [Role.RH, Role.MANAGER, Role.RP, Role.ADMIN, Role.AUDIT],
    historique:[Role.RP, Role.ADMIN, Role.AUDIT],
  },
};