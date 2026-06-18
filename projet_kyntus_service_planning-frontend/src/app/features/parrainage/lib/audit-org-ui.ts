export type AuditRoleFilter = 'RP' | 'Manager' | 'Coach' | 'Pilote';

export interface AuditOrgDimensions {
  departement: string;
  pole: string;
  cellule: string;
  roleMetier: AuditRoleFilter;
}

const EMPTY_ORG: AuditOrgDimensions = {
  departement: '—',
  pole: '—',
  cellule: '—',
  roleMetier: 'Pilote',
};

export function enrichAuditRowFromId(_id: string): AuditOrgDimensions {
  return { ...EMPTY_ORG };
}

export function getAuditOrgTree(): Array<{ dept: string; poles: Array<{ name: string; cellules: string[] }> }> {
  return [];
}
