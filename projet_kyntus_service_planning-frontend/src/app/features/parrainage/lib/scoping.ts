import type { Referral, ParrainageRole, ParrainageUser } from '../models/referral.model';
import {
  getOrgNodes,
  isReferrerUnderManager,
  piloteIdsForManagerDrill,
  piloteIdsForRpDrill,
  type HierarchyDrillSelection,
  type OrgNode,
} from './org-hierarchy';

export type { HierarchyDrillSelection };

function orgAllowedReferrerIds(user: ParrainageUser, nodes: OrgNode[]): Set<string> | null {
  if (user.role === 'ADMIN' || user.role === 'RH' || user.role === 'AUDIT') return null;
  const viewer = nodes.find((n) => n.id === user.id);
  if (!viewer) return new Set();
  if (user.role === 'PILOTE') return new Set([user.id]);
  const ids = new Set<string>();
  for (const n of nodes) {
    if (user.role === 'COACH' && n.celluleId === viewer.celluleId) ids.add(n.id);
    if (user.role === 'MANAGER' && n.poleId === viewer.poleId) ids.add(n.id);
    if (user.role === 'RP' && n.departementId === viewer.departementId) ids.add(n.id);
  }
  return ids;
}

export function getScopedReferrals(
  referrals: Referral[],
  user: ParrainageUser | null | undefined,
  drill: HierarchyDrillSelection = {},
): Referral[] {
  if (!user) return [];

  let result: Referral[];
  switch (user.role) {
    case 'ADMIN':
    case 'RH':
    case 'AUDIT':
      result = referrals;
      break;
    case 'RP': {
      const piloteIds = piloteIdsForRpDrill(getOrgNodes(), user.id, drill);
      result = referrals.filter((r) => piloteIds.includes(r.referrerId));
      break;
    }
    case 'MANAGER': {
      const piloteIds = piloteIdsForManagerDrill(getOrgNodes(), user.id, drill.coachId);
      result = referrals.filter((r) => piloteIds.includes(r.referrerId));
      break;
    }
    case 'COACH':
      result = referrals.filter((r) => isReferrerUnderManager(user.id, r.referrerId));
      break;
    case 'PILOTE':
      result = referrals.filter((r) => r.referrerId === user.id);
      break;
    default:
      result = [];
  }

  const orgIds = orgAllowedReferrerIds(user, getOrgNodes());
  if (orgIds === null) return result;
  return result.filter((r) => orgIds.has(r.referrerId));
}
