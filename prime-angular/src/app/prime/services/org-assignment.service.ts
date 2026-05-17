import type { Employee, Role } from '../models';
import { PRIME_API_BASE } from './prime-http';

type ManagerEtageAssignment = { userId: string; etageId: string };
type SupervisorServiceAssignment = { userId: string; serviceId: string };
type CoachSousServiceAssignment = { userId: string; sousServiceId: string };
type CoachPilotLink = { coachUserId: string; pilotUserId: string };

async function fetchJson<T>(path: string): Promise<T> {
  const response = await fetch(`${PRIME_API_BASE}${path}`, { credentials: 'include' });
  if (!response.ok) {
    throw new Error(`Org API error: ${response.status}`);
  }
  return response.json() as Promise<T>;
}

/** Périmètre dérivé du teamId pour les employés sans departementId (fallback). */
function effectiveDepartementId(e: Employee): string {
  if (e.departementId) return e.departementId;
  return e.poleId ?? '';
}

export const OrgAssignmentService = {
  async getAllowedEmployeeIds(role: Role, userId: string, employees: Employee[]): Promise<Set<string> | null> {
    if (
      role !== 'Manager' &&
      role !== 'Chef de projet' &&
      role !== 'RP' &&
      role !== 'Superviseur' &&
      role !== 'Coach' &&
      role !== 'Référent technique'
    )
      return null;

    return await this.getAllowedEmployeeIdsFromApi(role, userId, employees);
  },

  async getAllowedEmployeeIdsFromApi(role: Role, userId: string, employees: Employee[]): Promise<Set<string> | null> {
    if (role === 'Manager' || role === 'Chef de projet' || role === 'RP') {
      const rows = await fetchJson<ManagerEtageAssignment[]>(
        `/api/prime/org/assignments/manager-etage?userId=${encodeURIComponent(userId)}`,
      );
      const allowedEtages = new Set(rows.map((r) => r.etageId));
      return new Set(employees.filter((e) => allowedEtages.has(effectiveDepartementId(e))).map((e) => e.id));
    }
    if (role === 'Superviseur') {
      const rows = await fetchJson<SupervisorServiceAssignment[]>(
        `/api/prime/org/assignments/supervisor-service?userId=${encodeURIComponent(userId)}`,
      );
      const allowedCellules = new Set(rows.map((r) => r.serviceId));
      return new Set(employees.filter((e) => allowedCellules.has(e.celluleId ?? '')).map((e) => e.id));
    }
    const sousServices = await fetchJson<CoachSousServiceAssignment[]>(
      `/api/prime/org/assignments/coach-sous-service?userId=${encodeURIComponent(userId)}`,
    );
    const coachPilots = await fetchJson<CoachPilotLink[]>(
      `/api/prime/org/assignments/coach-pilot?coachUserId=${encodeURIComponent(userId)}`,
    );
    const allowedSousServices = new Set(sousServices.map((r) => r.sousServiceId));
    const linkedPilotIds = new Set(coachPilots.map((r) => r.pilotUserId));
    return new Set(
      employees
        .filter(
          (e) =>
            e.id === userId ||
            (allowedSousServices.has(e.serviceId) && (e.role !== 'Pilote' || linkedPilotIds.has(e.id))),
        )
        .map((e) => e.id),
    );
  },
};
