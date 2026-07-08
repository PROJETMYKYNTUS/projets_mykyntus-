import type { Employee } from '../../features/prime/models';
import type { OrgAssignmentsOverview } from '../../features/prime/services/prime-org-api.service';
import {
  isChefDeProjetRole,
  isReferentTechniqueRole,
  isSuperviseurRole,
} from './org-role-assignment';

export type StructureIncumbent = {
  userId: string;
  displayName: string;
};

export type StructureNodeIds = {
  orgPoleId?: string;
  orgCelluleId?: string;
  orgServiceId?: string;
};

export function employeeDisplayName(
  employees: readonly Employee[],
  userId: string,
): string {
  const id = userId.trim();
  const emp = employees.find((e) => e.id === id);
  return emp ? `${emp.firstName} ${emp.lastName}`.trim() : id;
}

export function structureRoleLabel(roleName: string): string {
  if (isChefDeProjetRole(roleName)) return 'chef de projet';
  if (isSuperviseurRole(roleName)) return 'superviseur';
  if (isReferentTechniqueRole(roleName)) return 'référent technique';
  return 'titulaire';
}

export function findStructureIncumbents(
  overview: OrgAssignmentsOverview,
  roleName: string,
  nodeIds: StructureNodeIds,
): StructureIncumbent[] {
  const userIds: string[] = [];

  if (isChefDeProjetRole(roleName)) {
    const poleId = nodeIds.orgPoleId?.trim();
    if (!poleId) return [];
    for (const a of overview.managerEtage ?? []) {
      if (a.etageId === poleId && a.userId?.trim()) userIds.push(a.userId.trim());
    }
  } else if (isSuperviseurRole(roleName)) {
    const celluleId = nodeIds.orgCelluleId?.trim();
    if (!celluleId) return [];
    for (const a of overview.supervisorService ?? []) {
      if ((a.celluleId ?? a.serviceId)?.trim() === celluleId && a.userId?.trim()) {
        userIds.push(a.userId.trim());
      }
    }
  } else if (isReferentTechniqueRole(roleName)) {
    const serviceId = nodeIds.orgServiceId?.trim();
    if (!serviceId) return [];
    for (const a of overview.coachSousService ?? []) {
      if ((a.serviceId ?? a.sousServiceId)?.trim() === serviceId && a.userId?.trim()) {
        userIds.push(a.userId.trim());
      }
    }
  }

  const unique = [...new Set(userIds)];
  return unique.map((userId) => ({
    userId,
    displayName: employeeDisplayName(overview.employees, userId),
  }));
}

/** @deprecated Préférer findStructureIncumbents — retourne le premier titulaire. */
export function findStructureIncumbent(
  overview: OrgAssignmentsOverview,
  roleName: string,
  nodeIds: StructureNodeIds,
): StructureIncumbent | null {
  return findStructureIncumbents(overview, roleName, nodeIds)[0] ?? null;
}

/** Charges multiples : pas d'écrasement silencieux — dialogue RH add/replace. */
export function shouldConfirmOverwrite(
  _incumbentUserId: string | null | undefined,
  _assigneeGuid: string | null | undefined,
): boolean {
  return false;
}

export function shouldConfirmIncumbentChoice(incumbents: readonly StructureIncumbent[]): boolean {
  return incumbents.length > 0;
}

export function buildIncumbentChoiceMessage(
  roleName: string,
  incumbents: readonly StructureIncumbent[],
): string {
  const label = structureRoleLabel(roleName);
  const names = incumbents.map((i) => i.displayName).join(', ');
  return `Ce poste a déjà ${incumbents.length} ${label}(s) : ${names}.`;
}

export type IncumbentAssignmentChoice = 'add' | 'replace' | 'cancel';

export function buildStructureOverwriteMessage(
  incumbent: StructureIncumbent,
  roleName: string,
): string {
  const label = structureRoleLabel(roleName);
  return `Voulez-vous écraser le ${label} actuel ${incumbent.displayName} ?`;
}

export type EmployeeStructuralRole = {
  role: string;
  nodeId: string;
  nodeLabel?: string;
  departmentCode?: string;
};

export function findEmployeeStructuralRole(
  overview: OrgAssignmentsOverview,
  employeeId: string,
): EmployeeStructuralRole | null {
  const id = employeeId.trim();
  if (!id) return null;

  for (const md of overview.operationalDepartments ?? []) {
    if (md.managerEmployeeId === id) {
      return {
        role: 'Manager',
        nodeId: md.id,
        nodeLabel: md.name,
        departmentCode: md.code,
      };
    }
  }

  const chef = overview.managerEtage?.find((a) => a.userId === id);
  if (chef) {
    return { role: 'Chef de projet', nodeId: chef.etageId };
  }

  const sup = overview.supervisorService?.find((a) => a.userId === id);
  if (sup) {
    return {
      role: 'Superviseur',
      nodeId: (sup.celluleId ?? sup.serviceId)?.trim() ?? '',
    };
  }

  const coach = overview.coachSousService?.find((a) => a.userId === id);
  if (coach) {
    return {
      role: 'Référent technique',
      nodeId: (coach.serviceId ?? coach.sousServiceId)?.trim() ?? '',
    };
  }

  const emp = overview.employees?.find((e) => e.id === id);
  if (emp?.role === 'Pilote' && emp.serviceId) {
    return { role: 'Pilote', nodeId: emp.serviceId };
  }

  if (
    emp &&
    (isChefDeProjetRole(emp.role) ||
      isSuperviseurRole(emp.role) ||
      isReferentTechniqueRole(emp.role))
  ) {
    return { role: emp.role, nodeId: emp.serviceId ?? emp.celluleId ?? emp.poleId ?? '' };
  }

  return null;
}

export function buildCrossRoleOverwriteMessage(
  assigneeDisplayName: string,
  existing: EmployeeStructuralRole,
): string {
  const where = existing.nodeLabel ?? existing.departmentCode ?? existing.nodeId;
  const suffix = where ? ` (${where})` : '';
  return `${assigneeDisplayName} est déjà ${existing.role}${suffix}. Cette affectation remplacera le rôle précédent.`;
}

function isCoachOrReferentRole(role: string): boolean {
  const r = role.trim().toLowerCase();
  return r === 'coach' || r === 'référent technique' || r === 'referent technique';
}

/** Superviseurs rattachés au chef de projet sélectionné (parentId ou titulaire cellule). */
export function filterSuperviseursForChefDeProjet(
  overview: OrgAssignmentsOverview,
  chefDeProjetId: string,
  orgCelluleId?: string,
): StructureIncumbent[] {
  const chefId = chefDeProjetId.trim();
  if (!chefId) return [];

  const fromHierarchy = (overview.employees ?? []).filter(
    (e) => isSuperviseurRole(e.role) && e.parentId === chefId,
  );

  const celluleId = orgCelluleId?.trim();
  const fromStructure = celluleId
    ? (overview.supervisorService ?? [])
        .filter((a) => (a.celluleId ?? a.serviceId)?.trim() === celluleId && a.userId?.trim())
        .map((a) => a.userId!.trim())
    : [];

  const userIds = [...new Set([
    ...fromHierarchy.map((e) => e.id),
    ...fromStructure,
  ])];

  return userIds.map((userId) => ({
    userId,
    displayName: employeeDisplayName(overview.employees ?? [], userId),
  }));
}

/** Référents techniques sous le superviseur sélectionné. */
export function filterReferentsForSuperviseur(
  overview: OrgAssignmentsOverview,
  superviseurId: string,
  orgServiceId?: string,
): StructureIncumbent[] {
  const supId = superviseurId.trim();
  if (!supId) return [];

  const fromHierarchy = (overview.employees ?? []).filter(
    (e) => isCoachOrReferentRole(e.role) && e.parentId === supId,
  );

  const serviceId = orgServiceId?.trim();
  const fromStructure = serviceId
    ? (overview.coachSousService ?? [])
        .filter((a) => (a.serviceId ?? a.sousServiceId)?.trim() === serviceId && a.userId?.trim())
        .map((a) => a.userId!.trim())
    : [];

  const userIds = [...new Set([
    ...fromHierarchy.map((e) => e.id),
    ...fromStructure,
  ])];

  return userIds.map((userId) => ({
    userId,
    displayName: employeeDisplayName(overview.employees ?? [], userId),
  }));
}
