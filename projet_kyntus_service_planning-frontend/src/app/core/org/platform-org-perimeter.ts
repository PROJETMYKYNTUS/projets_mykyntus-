import type { Department, Employee } from '../../features/prime/models';
import type { OrgAssignmentsOverview } from '../../features/prime/services/prime-org-api.service';
import { enrichUserOrgPerimeter } from './user-org-perimeter';
import { resolveEmployeeOrgLabels } from '../../features/prime/lib/org-display-labels';

export interface PlatformOrgLabels {
  pole: string;
  cellule: string;
  service: string;
}

/** Pôle du Chef de projet : managerEtage puis poleId employé. */
export function resolveChefProjetPoleId(
  userGuid: string,
  employee: Pick<Employee, 'poleId' | 'departementId'>,
  overview: OrgAssignmentsOverview | null,
): string {
  const guid = userGuid.trim().toLowerCase();
  const mgr = overview?.managerEtage?.find((a) => a.userId.trim().toLowerCase() === guid);
  if (mgr?.etageId?.trim()) return mgr.etageId.trim();
  return (employee.poleId ?? employee.departementId ?? '').trim();
}

/** Pôle du Superviseur : supervisorService / celluleId employé. */
export function resolveSuperviseurCelluleId(
  userGuid: string,
  employee: Pick<Employee, 'celluleId'>,
  overview: OrgAssignmentsOverview | null,
): string {
  const guid = userGuid.trim().toLowerCase();
  const sup = overview?.supervisorService?.find((a) => a.userId.trim().toLowerCase() === guid);
  const cellId = (sup?.celluleId ?? sup?.serviceId ?? '').trim();
  if (cellId) return cellId;
  return (employee.celluleId ?? '').trim();
}

/** Libellés org avec fallback enrichUserOrgPerimeter + arbre legacy. */
export function resolvePlatformOrgLabels(
  employee: Employee,
  departments: readonly Department[],
  overview: OrgAssignmentsOverview | null,
  subServices: readonly { id: number; primeServiceId?: string | null }[] = [],
): PlatformOrgLabels {
  const enriched = enrichUserOrgPerimeter(
    {
      id: 0,
      guid: employee.id,
      roleId: 0,
      roleName: employee.role,
      managedSubServices: [],
      managedServices: [],
      firstName: employee.firstName,
      lastName: employee.lastName,
      email: employee.email,
      hireDate: '',
      isActive: true,
      createdAt: '',
      level: 0,
      subServiceId: undefined,
    },
    departments,
    overview,
    subServices,
  );
  const legacy = resolveEmployeeOrgLabels(employee, departments);
  return {
    pole: enriched.pole?.trim() || legacy.pole,
    cellule: enriched.cellule?.trim() || legacy.cellule,
    service: enriched.service?.trim() || legacy.service,
  };
}

/** Déduplique par email — conserve l’entrée la plus riche en infos org. */
export function dedupeEmployeesByEmail(employees: readonly Employee[]): Employee[] {
  const byEmail = new Map<string, Employee>();
  for (const e of employees) {
    const key = (e.email ?? '').trim().toLowerCase();
    if (!key) {
      byEmail.set(`__id:${e.id}`, e);
      continue;
    }
    const existing = byEmail.get(key);
    if (!existing || orgRichness(e) > orgRichness(existing)) {
      byEmail.set(key, e);
    }
  }
  return [...byEmail.values()];
}

function orgRichness(e: Employee): number {
  let score = 0;
  if (e.poleId?.trim()) score += 4;
  if (e.celluleId?.trim()) score += 2;
  if (e.serviceId?.trim()) score += 1;
  if (isGuid(e.id)) score += 8;
  return score;
}

function isGuid(id: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id.trim());
}

/** Collaborateurs visibles pour un Chef de projet (même pôle). */
export function employeesInChefProjetPole(
  employees: readonly Employee[],
  poleId: string,
): Employee[] {
  const pid = poleId.trim();
  if (!pid) return [];
  return employees.filter((e) => {
    const role = (e.role ?? '').trim();
    if (role === 'Admin' || role === 'RH' || role === 'Audit') return false;
    const poleKey = (e.poleId ?? e.departementId ?? '').trim();
    return poleKey === pid;
  });
}

/** Collaborateurs sous une cellule supervisée. */
export function employeesInSuperviseurCellule(
  employees: readonly Employee[],
  celluleId: string,
): Employee[] {
  const cid = celluleId.trim();
  if (!cid) return [];
  return employees.filter((e) => (e.celluleId ?? '').trim() === cid);
}
