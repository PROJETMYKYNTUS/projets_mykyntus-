export interface Organization {
  departementId: string;
  poleId: string;
  celluleId: string;
}

export interface OrgNode extends Organization {
  id: string;
  parentId?: string;
}

const DEFAULT_ORG: Organization = { departementId: 'dept-1', poleId: 'pole-1', celluleId: 'cell-1' };

/** Nœuds hiérarchie — alimentés par GET /api/parrainage/org/nodes (sync Planning/Prime). */
let orgNodesCache: OrgNode[] = [];

export function setOrgNodes(nodes: OrgNode[]): void {
  orgNodesCache = nodes;
}

export function getOrgNodes(): OrgNode[] {
  return orgNodesCache;
}

export function isReferrerUnderManager(viewerId: string, referrerId: string, nodes: OrgNode[] = orgNodesCache): boolean {
  if (viewerId === referrerId) return true;
  let cur = nodes.find((n) => n.id === referrerId);
  const guard = new Set<string>();
  while (cur?.parentId) {
    if (cur.parentId === viewerId) return true;
    if (guard.has(cur.id)) break;
    guard.add(cur.id);
    cur = nodes.find((n) => n.id === cur!.parentId);
  }
  return false;
}

export interface HierarchyDrillSelection {
  managerId?: string;
  coachId?: string;
}

export function listManagersUnderRp(nodes: OrgNode[], rpId: string): OrgNode[] {
  return nodes.filter((n) => n.parentId === rpId);
}

export function listCoachesUnderManager(nodes: OrgNode[], managerId: string): OrgNode[] {
  return nodes.filter((n) => n.parentId === managerId);
}

export function piloteIdsForManagerDrill(nodes: OrgNode[], managerId: string, coachId?: string): string[] {
  const coaches = listCoachesUnderManager(nodes, managerId);
  const coachIds = coachId ? [coachId] : coaches.map((c) => c.id);
  return nodes.filter((n) => n.parentId && coachIds.includes(n.parentId)).map((n) => n.id);
}

export function piloteIdsForRpDrill(nodes: OrgNode[], rpId: string, drill: HierarchyDrillSelection): string[] {
  const managers = listManagersUnderRp(nodes, rpId);
  const managerIds = drill.managerId ? [drill.managerId] : managers.map((m) => m.id);
  const coachIds = drill.coachId
    ? [drill.coachId]
    : managerIds.flatMap((mid) => listCoachesUnderManager(nodes, mid).map((c) => c.id));
  return nodes.filter((n) => n.parentId && coachIds.includes(n.parentId)).map((n) => n.id);
}

/** Mappe la réponse API vers OrgNode avec org par défaut. */
export function mapApiOrgNodes(
  rows: Array<{ id: string; parentId?: string | null }>
): OrgNode[] {
  return rows.map((r) => ({
    id: r.id,
    parentId: r.parentId ?? undefined,
    ...DEFAULT_ORG,
  }));
}
