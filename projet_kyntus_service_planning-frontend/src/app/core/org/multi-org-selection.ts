import type { OrgAssignmentsOverview } from '../../features/prime/services/prime-org-api.service';
import type {
  OperationalDepartmentNode,
  OrgPoleNode,
} from '../../features/prime/models/org-tree.types';
import {
  isChefDeProjetRole,
  isPiloteRole,
  isReferentTechniqueRole,
  isSuperviseurRole,
  type OrgRoleAssignmentDepth,
} from './org-role-assignment';
import {
  findOperationalSelectionByCelluleId,
  findOperationalSelectionByPoleId,
  findOperationalSelectionByServiceId,
  operationalSelectionSummary,
} from './operational-org-picker';

export type MultiOrgSelectionState = {
  selectedOrgNodeIds: string[];
  primaryOrgNodeId: string;
};

/** Chef / Superviseur / RT : multi-nœuds. Pilote et managers restent mono. */
export function supportsMultiOrgSelection(roleName: string): boolean {
  return (
    isChefDeProjetRole(roleName) ||
    isSuperviseurRole(roleName) ||
    isReferentTechniqueRole(roleName)
  );
}

export function directoryAssignmentKindForRole(
  roleName: string,
): 'ChefDeProjet' | 'Superviseur' | 'ReferentTechnique' | null {
  if (isChefDeProjetRole(roleName)) return 'ChefDeProjet';
  if (isSuperviseurRole(roleName)) return 'Superviseur';
  if (isReferentTechniqueRole(roleName)) return 'ReferentTechnique';
  return null;
}

export function addOrgNodeSelection(
  state: MultiOrgSelectionState,
  nodeId: string,
): MultiOrgSelectionState {
  const id = nodeId.trim();
  if (!id) return state;
  if (state.selectedOrgNodeIds.includes(id)) {
    return state;
  }
  const selectedOrgNodeIds = [...state.selectedOrgNodeIds, id];
  const primaryOrgNodeId = state.primaryOrgNodeId.trim() || id;
  return { selectedOrgNodeIds, primaryOrgNodeId };
}

export function removeOrgNodeSelection(
  state: MultiOrgSelectionState,
  nodeId: string,
): MultiOrgSelectionState {
  const id = nodeId.trim();
  const selectedOrgNodeIds = state.selectedOrgNodeIds.filter((n) => n !== id);
  let primaryOrgNodeId = state.primaryOrgNodeId;
  if (primaryOrgNodeId === id) {
    primaryOrgNodeId = selectedOrgNodeIds[0] ?? '';
  }
  return { selectedOrgNodeIds, primaryOrgNodeId };
}

export function setPrimaryOrgNode(
  state: MultiOrgSelectionState,
  nodeId: string,
): MultiOrgSelectionState {
  const id = nodeId.trim();
  if (!id || !state.selectedOrgNodeIds.includes(id)) return state;
  return { ...state, primaryOrgNodeId: id };
}

export function clearMultiOrgSelection(): MultiOrgSelectionState {
  return { selectedOrgNodeIds: [], primaryOrgNodeId: '' };
}

export function hydrateMultiOrgSelectionFromOverview(
  overview: OrgAssignmentsOverview | null | undefined,
  employeeId: string,
  roleName: string,
): MultiOrgSelectionState | null {
  const guid = employeeId.trim();
  if (!overview || !guid) return null;

  let nodeIds: string[] = [];
  if (isChefDeProjetRole(roleName)) {
    nodeIds = (overview.managerEtage ?? [])
      .filter((a) => a.userId === guid && a.etageId?.trim())
      .map((a) => a.etageId.trim());
  } else if (isSuperviseurRole(roleName)) {
    nodeIds = (overview.supervisorService ?? [])
      .filter((a) => a.userId === guid)
      .map((a) => (a.celluleId ?? a.serviceId ?? '').trim())
      .filter(Boolean);
  } else if (isReferentTechniqueRole(roleName) || isPiloteRole(roleName)) {
    nodeIds = (overview.coachSousService ?? [])
      .filter((a) => a.userId === guid)
      .map((a) => (a.serviceId ?? a.sousServiceId ?? '').trim())
      .filter(Boolean);
    if (nodeIds.length === 0) {
      const emp = overview.employees?.find((e) => e.id === guid);
      const svc = (emp?.serviceId ?? '').trim();
      if (svc) nodeIds = [svc];
    }
  } else {
    return null;
  }

  const unique = [...new Set(nodeIds)];
  if (unique.length === 0) return null;

  const emp = overview.employees?.find((e) => e.id === guid);
  let primary = '';
  if (isChefDeProjetRole(roleName)) {
    primary = (emp?.poleId ?? '').trim();
  } else if (isSuperviseurRole(roleName)) {
    primary = (emp?.celluleId ?? '').trim();
  } else {
    primary = (emp?.serviceId ?? '').trim();
  }
  if (!unique.includes(primary)) {
    primary = unique[0];
  }

  return { selectedOrgNodeIds: unique, primaryOrgNodeId: primary };
}

export function resolveOrgNodePathLabel(
  operationalDepartments: readonly OperationalDepartmentNode[],
  unassignedPoles: readonly OrgPoleNode[],
  depth: OrgRoleAssignmentDepth,
  nodeId: string,
): string {
  const id = nodeId.trim();
  if (!id) return '—';

  if (depth === 'pole') {
    const sel = findOperationalSelectionByPoleId(operationalDepartments, unassignedPoles, id);
    if (!sel) return id;
    return operationalSelectionSummary(
      operationalDepartments,
      unassignedPoles,
      sel.operationalDeptId,
      sel.poleId,
      '',
      '',
    );
  }

  if (depth === 'cellule') {
    const sel = findOperationalSelectionByCelluleId(operationalDepartments, unassignedPoles, id);
    if (!sel) return id;
    return operationalSelectionSummary(
      operationalDepartments,
      unassignedPoles,
      sel.operationalDeptId,
      sel.poleId,
      sel.celluleId,
      '',
    );
  }

  if (depth === 'service') {
    const sel = findOperationalSelectionByServiceId(operationalDepartments, unassignedPoles, id);
    if (!sel) return id;
    return operationalSelectionSummary(
      operationalDepartments,
      unassignedPoles,
      sel.operationalDeptId,
      sel.poleId,
      sel.celluleId,
      sel.serviceId,
    );
  }

  return id;
}

export function validateMultiOrgSelection(
  state: MultiOrgSelectionState,
  supportsMulti: boolean,
): string | null {
  if (!supportsMulti) return null;
  if (state.selectedOrgNodeIds.length === 0) {
    return 'Sélectionnez au moins un périmètre organisationnel.';
  }
  const primary = state.primaryOrgNodeId.trim();
  if (!primary) {
    return 'Définissez un périmètre principal.';
  }
  if (!state.selectedOrgNodeIds.includes(primary)) {
    return 'Le périmètre principal doit faire partie de la sélection.';
  }
  return null;
}

export function summarizeMultiOrgSelection(
  operationalDepartments: readonly OperationalDepartmentNode[],
  unassignedPoles: readonly OrgPoleNode[],
  depth: OrgRoleAssignmentDepth,
  state: MultiOrgSelectionState,
): string {
  if (state.selectedOrgNodeIds.length === 0) return '—';
  return state.selectedOrgNodeIds
    .map((id) => {
      const label = resolveOrgNodePathLabel(operationalDepartments, unassignedPoles, depth, id);
      return id === state.primaryOrgNodeId ? `${label} (principal)` : label;
    })
    .join(' · ');
}
