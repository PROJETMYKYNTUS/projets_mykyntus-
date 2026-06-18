import type { AuditAnomaly, AuditOperation, AuditTrailLog } from '../mock-data/audit';
import {
  mockAuditAnomalies,
  mockAuditCharts,
  mockAuditKpis,
  mockAuditOperations,
  mockAuditTrailLogs,
} from '../mock-data/audit';
import { isPrimeDemoMockEnabled } from '../mock-data/prime-demo-config';
import { primeApiGet } from './prime-http';

export type AuditHistoryFilter = {
  dateFrom?: string;
  dateTo?: string;
  project?: string;
  status?: 'Validé' | 'Rejeté' | 'En cours' | 'Tous';
};

type WorkflowSummary = {
  pending: number;
  referentTechniqueApproved: number;
  superviseurApproved: number;
  chefDeProjetApproved: number;
  rhApproved: number;
  rejected: number;
  total: number;
};

type AuditLogDto = {
  id: string;
  at: string;
  userDisplayName: string;
  action: string;
  entityType?: string | null;
  detailJson?: string | null;
};

type AnomalyDto = { id: string; status: string };

export const AuditPrimeService = {
  getDashboard: async () => {
    const [summary, anomalies, logs] = await Promise.all([
      primeApiGet<WorkflowSummary>('/api/prime/validation/summary'),
      primeApiGet<AnomalyDto[]>('/api/prime/admin/anomalies?take=200'),
      primeApiGet<AuditLogDto[]>('/api/prime/admin/audit-logs?take=200'),
    ]);
    const openAnomalies = anomalies.filter((a) => a.status === 'Open').length;
    const validated = summary.rhApproved ?? 0;
    const rejected = summary.rejected ?? 0;
    const useDemo = isPrimeDemoMockEnabled() && summary.total === 0;
    if (useDemo) {
      return { kpis: mockAuditKpis, charts: mockAuditCharts };
    }
    return {
      kpis: {
        totalPrimes: summary.total,
        validations: summary.total - (summary.pending ?? 0),
        anomalies: openAnomalies,
        conformityRate: summary.total === 0 ? 100 : Math.round((100 * validated) / Math.max(validated + rejected, 1)),
      },
      charts: {
        flowByStep: [
          { step: 'Réf. tech.', value: summary.referentTechniqueApproved ?? 0 },
          { step: 'Superviseur', value: summary.superviseurApproved ?? 0 },
          { step: 'CdP', value: summary.chefDeProjetApproved ?? 0 },
          { step: 'RH', value: summary.rhApproved ?? 0 },
        ],
        validationVsRejection: [
          { name: 'Validé (RH)', value: validated },
          { name: 'Rejeté', value: rejected },
        ],
        activityByRole: [
          { role: 'Audit (logs)', value: logs.length },
          { role: 'En attente', value: summary.pending ?? 0 },
          { role: 'Total fiches', value: summary.total },
        ],
      },
    };
  },

  getOperations: async (): Promise<AuditOperation[]> => {
    const logs = await primeApiGet<AuditLogDto[]>('/api/prime/admin/audit-logs?take=200');
    if (isPrimeDemoMockEnabled() && logs.length === 0) return mockAuditOperations;
    const mapped = logs.map((r) => ({
      id: r.id,
      employeeName: r.userDisplayName,
      projectName: r.entityType ?? '—',
      steps: [] as AuditOperation['steps'],
      validatedBy: r.userDisplayName,
      date: (r.at ?? '').slice(0, 10),
      status: 'En cours' as const,
    }));
    if (isPrimeDemoMockEnabled() && mapped.every((o) => o.steps.length === 0)) {
      return mockAuditOperations;
    }
    return mapped;
  },

  getAuditTrailLogs: async (): Promise<AuditTrailLog[]> => {
    const logs = await primeApiGet<AuditLogDto[]>('/api/prime/admin/audit-logs?take=300');
    if (isPrimeDemoMockEnabled() && logs.length === 0) return mockAuditTrailLogs;
    const mapped = logs.map((r) => ({
      id: r.id,
      user: r.userDisplayName,
      action: r.action,
      date: r.at ?? '',
      detail: r.detailJson ?? '',
    }));
    return mapped.length > 0 ? mapped : isPrimeDemoMockEnabled() ? mockAuditTrailLogs : mapped;
  },

  getAnomalies: async (): Promise<AuditAnomaly[]> => {
    const rows = await primeApiGet<
      { id: string; type: string; description: string; status: string }[]
    >('/api/prime/admin/anomalies?take=100');
    if (isPrimeDemoMockEnabled() && rows.length === 0) return mockAuditAnomalies;
    const mapped = rows.map((r) => ({
      id: r.id,
      type: 'Incohérence' as const,
      description: r.description,
      status: (r.status === 'Resolved' ? 'Corrigée' : 'Ouverte') as AuditAnomaly['status'],
    }));
    return mapped.length > 0 ? mapped : isPrimeDemoMockEnabled() ? mockAuditAnomalies : mapped;
  },
};

export function applyAuditHistoryFilters(ops: AuditOperation[], filter: AuditHistoryFilter) {
  const { dateFrom, dateTo, project, status } = filter;
  return ops.filter((op) => {
    const matchesProject = project ? op.projectName === project : true;
    const matchesStatus = status && status !== 'Tous' ? op.status === status : true;
    const matchesFrom = dateFrom ? op.date >= dateFrom : true;
    const matchesTo = dateTo ? op.date <= dateTo : true;
    return matchesProject && matchesStatus && matchesFrom && matchesTo;
  });
}

export function formatAuditSteps(steps: AuditOperation['steps']) {
  return steps.map((s) => `${s.role}: ${s.status}`).join(' • ');
}

export type { AuditOperation, AuditTrailLog, AuditAnomaly };
