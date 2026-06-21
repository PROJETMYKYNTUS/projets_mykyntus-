/** Hiérarchie Organisation RH à 4 niveaux (source : Directory overview). */

export interface OrgServiceNode {
  id: string;
  name: string;
}

export interface OrgCelluleNode {
  id: string;
  name: string;
  services: OrgServiceNode[];
}

export interface OrgPoleNode {
  id: string;
  name: string;
  cellules: OrgCelluleNode[];
}

export interface OperationalDepartmentNode {
  id: string;
  code: string;
  name: string;
  managerEmployeeId?: string;
  poles: OrgPoleNode[];
}
