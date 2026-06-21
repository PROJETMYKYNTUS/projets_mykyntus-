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

export function findStructureIncumbent(
  overview: OrgAssignmentsOverview,
  roleName: string,
  nodeIds: StructureNodeIds,
): StructureIncumbent | null {
  let userId: string | undefined;

  if (isChefDeProjetRole(roleName)) {
    const poleId = nodeIds.orgPoleId?.trim();
    if (!poleId) return null;
    userId = overview.managerEtage?.find((a) => a.etageId === poleId)?.userId;
  } else if (isSuperviseurRole(roleName)) {
    const celluleId = nodeIds.orgCelluleId?.trim();
    if (!celluleId) return null;
    userId = overview.supervisorService?.find(
      (a) => (a.celluleId ?? a.serviceId)?.trim() === celluleId,
    )?.userId;
  } else if (isReferentTechniqueRole(roleName)) {
    const serviceId = nodeIds.orgServiceId?.trim();
    if (!serviceId) return null;
    userId = overview.coachSousService?.find(
      (a) => (a.serviceId ?? a.sousServiceId)?.trim() === serviceId,
    )?.userId;
  }

  const resolved = userId?.trim();
  if (!resolved) return null;

  return {
    userId: resolved,
    displayName: employeeDisplayName(overview.employees, resolved),
  };
}

export function shouldConfirmOverwrite(
  incumbentUserId: string | null | undefined,
  assigneeGuid: string | null | undefined,
): boolean {
  const incumbent = (incumbentUserId ?? '').trim();
  if (!incumbent) return false;
  const assignee = (assigneeGuid ?? '').trim();
  if (assignee && incumbent === assignee) return false;
  return true;
}

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
