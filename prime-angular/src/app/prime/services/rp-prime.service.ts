import type { HierarchyDrillSelection } from '../lib/hierarchyDrillDown';
import { piloteIdsForRpDrill } from '../lib/hierarchyDrillDown';
import { intersectNullableEmployeeSets, orgAllowedEmployeeIds } from '../lib/organizationScope';
import { primeApiGet, primeApiPut } from './prime-http';
import { PrimeService } from './prime.service';

export interface RpValidationItem {
  id: string;
  employeeId: string;
  employeeName: string;
  projectId: string;
  projectName: string;
  performanceScore: number;
  managerValidated: boolean;
  status: 'Manager Approved' | 'RP Approved' | 'Rejected';
  period: string;
}

export interface RpDashboardStats {
  projectProgress: number;
  completedTasks: number;
  averageTeamPerformance: number;
  pendingValidations: number;
  performanceEvolution: Array<{ month: string; score: number }>;
  memberPerformance: Array<{ name: string; score: number; status: 'Excellent' | 'Moyen' | 'Faible' }>;
}

type ChefProjetTeamMemberPerformance = {
  employeeId: string;
  employeeName: string;
  projectId: string;
  projectName: string;
  completedTasks: number;
  totalTasks: number;
  objectivesReached: number;
  totalObjectives: number;
  monthlyPerformance: { month: string; score: number }[];
};

type ChefProjetValidationItem = {
  id: string;
  employeeId: string;
  employeeName: string;
  projectId: string;
  projectName: string;
  performanceScore: number;
  superviseurValidated: boolean;
  status: string;
  period: string;
};

type ChefProjetDashboardStats = {
  projectProgress: number;
  completedTasks: number;
  averageTeamPerformance: number;
  pendingValidations: number;
  performanceEvolution: { month: string; score: number }[];
  memberPerformance: { name: string; score: number; status: string }[];
};

async function combinedRpPiloteScope(rpUserId: string, drill: HierarchyDrillSelection): Promise<Set<string> | null> {
  const employees = await PrimeService.getEmployees();
  const departments = await PrimeService.getDepartments();
  const piloteScope = piloteIdsForRpDrill(employees, rpUserId, drill);
  if (piloteScope === null) return null;
  const orgScope = orgAllowedEmployeeIds('RP', rpUserId, employees, departments);
  return intersectNullableEmployeeSets(piloteScope, orgScope) ?? new Set<string>();
}

export const RpPrimeService = {
  getAssignedProjectIds: async (rpUserId: string): Promise<string[]> => {
    const ids = await primeApiGet<string[]>(
      `/api/rp/assigned-project-ids?rpUserId=${encodeURIComponent(rpUserId)}`,
    );
    return ids?.length ? ids : ['default'];
  },

  getRpDashboardStats: async (rpUserId: string, drill: HierarchyDrillSelection = {}): Promise<RpDashboardStats> => {
    const scope = await combinedRpPiloteScope(rpUserId, drill);
    if (scope === null) {
      return {
        projectProgress: 0,
        completedTasks: 0,
        averageTeamPerformance: 0,
        pendingValidations: 0,
        performanceEvolution: [],
        memberPerformance: [],
      };
    }
    const stats = await primeApiGet<ChefProjetDashboardStats>(
      `/api/rp/dashboard-stats?rpUserId=${encodeURIComponent(rpUserId)}`,
    );
    const memberPerformance = (stats.memberPerformance ?? []).map((m) => ({
      name: m.name,
      score: m.score,
      status: (m.status === 'Excellent' || m.status === 'Moyen' || m.status === 'Faible'
        ? m.status
        : 'Moyen') as RpDashboardStats['memberPerformance'][0]['status'],
    }));
    return {
      projectProgress: stats.projectProgress,
      completedTasks: stats.completedTasks,
      averageTeamPerformance: stats.averageTeamPerformance,
      pendingValidations: stats.pendingValidations,
      performanceEvolution: stats.performanceEvolution ?? [],
      memberPerformance,
    };
  },

  getTeamPerformanceByProject: async (rpUserId: string, drill: HierarchyDrillSelection = {}) => {
    const scope = await combinedRpPiloteScope(rpUserId, drill);
    if (scope === null) return [];
    const rows = await primeApiGet<ChefProjetTeamMemberPerformance[]>(
      `/api/rp/team-performance?rpUserId=${encodeURIComponent(rpUserId)}`,
    );
    return rows.filter((r) => scope.has(r.employeeId));
  },

  getManagerValidatedPrimes: async (rpUserId: string, drill: HierarchyDrillSelection = {}): Promise<RpValidationItem[]> => {
    const scope = await combinedRpPiloteScope(rpUserId, drill);
    if (scope === null) return [];
    const rows = await primeApiGet<ChefProjetValidationItem[]>(
      `/api/rp/manager-validated?rpUserId=${encodeURIComponent(rpUserId)}`,
    );
    return rows
      .filter((r) => scope.has(r.employeeId) && r.superviseurValidated)
      .map((r) => ({
        id: r.id,
        employeeId: r.employeeId,
        employeeName: r.employeeName,
        projectId: r.projectId,
        projectName: r.projectName,
        performanceScore: r.performanceScore,
        managerValidated: r.superviseurValidated,
        status: (['Manager Approved', 'RP Approved', 'Rejected'].includes(r.status)
          ? r.status
          : 'Manager Approved') as RpValidationItem['status'],
        period: r.period,
      }));
  },

  updateRpValidationStatus: async (
    rpUserId: string,
    drill: HierarchyDrillSelection,
    id: string,
    status: 'RP Approved' | 'Rejected',
  ) => {
    const scope = await combinedRpPiloteScope(rpUserId, drill);
    if (scope === null) throw new Error('Selection Manager/Coach requise');
    const item = await primeApiPut<RpValidationItem>(`/api/rp/validations/${encodeURIComponent(id)}/status`, {
      status,
    });
    if (!scope.has(item.employeeId)) throw new Error('Acces refuse hors perimetre hierarchique');
    return item;
  },
};
