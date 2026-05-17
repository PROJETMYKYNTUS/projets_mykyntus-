import { Department, Employee, PrimeResult, PrimeRule, PrimeType, Role } from '../models';
import { applyDrillDownToEmployeeRows, type HierarchyDrillSelection } from '../lib/hierarchyDrillDown';
import { OrgAssignmentService } from './org-assignment.service';
import { primeApiGet } from './prime-http';

export interface PrimeDashboardStats {
  totalPrimesThisMonth: number;
  budgetConsumption: number;
  topTeams: { name: string; amount: number }[];
  topEmployees: { name: string; amount: number }[];
  primeByDepartment: { name: string; value: number }[];
  primeEvolution: { month: string; amount: number }[];
}

/** Workflow : Pending → Coach → Superviseur → Manager → RP → RH (RH/Admin conservent une validation finale directe). */
export function getNextStatusAfterApproval(
  current: PrimeResult['status'],
  actorRole: Role,
): PrimeResult['status'] | null {
  if (actorRole === 'RH' || actorRole === 'Admin') {
    if (current === 'RH Approved' || current === 'Rejected') return null;
    return 'RH Approved';
  }
  if (actorRole === 'Coach') {
    return current === 'Pending' ? 'Coach Approved' : null;
  }
  if (actorRole === 'Superviseur') {
    return current === 'Coach Approved' ? 'Superviseur Approved' : null;
  }
  if (actorRole === 'Manager') {
    return current === 'Superviseur Approved' ? 'Manager Approved' : null;
  }
  if (actorRole === 'RP') {
    return current === 'Manager Approved' ? 'RP Approved' : null;
  }
  return null;
}

export const PrimeService = {
  getDepartments: (): Promise<Department[]> => primeApiGet('/api/prime/departments'),

  getEmployees: (): Promise<Employee[]> => primeApiGet('/api/prime/employees'),

  getPrimeTypes: (): Promise<PrimeType[]> => primeApiGet('/api/prime/types'),

  getPrimeRules: (): Promise<PrimeRule[]> => primeApiGet('/api/prime/rules'),

  getPrimeResults: (): Promise<PrimeResult[]> => primeApiGet('/api/prime/results'),

  getPrimeResultsScoped: async (
    viewerRole: Role,
    viewerId: string,
    drill: HierarchyDrillSelection = {},
  ): Promise<PrimeResult[]> => {
    const [employees, base] = await Promise.all([PrimeService.getEmployees(), PrimeService.getPrimeResults()]);
    const hierarchyScoped = applyDrillDownToEmployeeRows(base, viewerRole, viewerId, employees, drill, await PrimeService.getDepartments());
    const orgAssignmentScoped = await OrgAssignmentService.getAllowedEmployeeIds(viewerRole, viewerId, employees);
    if (orgAssignmentScoped === null) return hierarchyScoped;
    return hierarchyScoped.filter((row) => orgAssignmentScoped.has(row.employeeId));
  },

  getMyPrimeResults: async (employeeId: string): Promise<PrimeResult[]> => {
    const rows = await primeApiGet<PrimeResult[]>(
      `/api/prime/my-results?employeeId=${encodeURIComponent(employeeId)}`,
    );
    return rows.map((r) => ({ ...r }));
  },

  updatePrimeResultStatus: async (
    _id: string,
    _status: PrimeResult['status'],
    _approvedBy?: string,
  ): Promise<PrimeResult> => {
    throw new Error('Utilisez /api/prime/validation pour valider ou rejeter une fiche.');
  },

  getDashboardStats: () => primeApiGet<PrimeDashboardStats>('/api/prime/dashboard-stats'),
};
