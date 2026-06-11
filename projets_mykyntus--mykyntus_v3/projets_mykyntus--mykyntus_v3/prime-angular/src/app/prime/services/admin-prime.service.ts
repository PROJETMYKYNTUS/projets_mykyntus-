import type {
  AdminAnomaly,
  AdminAuditLog,
  AdminDashboardCharts,
  AdminRbacRow,
  AdminSystemAlert,
  AdminSystemKpi,
  AdminWorkflowConfig,
} from '../mock-data/admin';
import { WORKFLOW_ACTIONS, WORKFLOW_STEP_ROLES } from '../mock-data/admin';
import type { WorkflowStepConfigDto, WorkflowGlobalConfigDto } from './prime-admin.service';
import { primeApiGet, primeApiPut } from './prime-http';

type DashboardStats = {
  totalPrimesThisMonth: number;
  budgetConsumption: number;
  primeEvolution: { month: string; amount: number }[];
  primeByDepartment: { name: string; value: number }[];
};

function mapApproverToUiRole(role: string): AdminWorkflowConfig['steps'][0]['role'] {
  const r = role.trim();
  if (r === 'Référent technique') return 'Coach';
  if (r === 'Chef de projet') return 'Manager';
  if (r === 'RH') return 'RH';
  if (r === 'Superviseur') return 'Superviseur';
  if (r === 'RP') return 'RP';
  if (WORKFLOW_STEP_ROLES.includes(r as (typeof WORKFLOW_STEP_ROLES)[number])) return r as AdminWorkflowConfig['steps'][0]['role'];
  return 'Coach';
}

function mapUiRoleToApprover(role: string): string {
  if (role === 'Coach') return 'Référent technique';
  if (role === 'Manager') return 'Chef de projet';
  return role;
}

function ensureRequiredWorkflowOrder(payload: AdminWorkflowConfig): AdminWorkflowConfig {
  const byRole = new Map(payload.steps.map((s) => [s.role, s]));
  const ensure = (role: 'Coach' | 'Superviseur' | 'Manager' | 'RP' | 'RH', fallbackId: string) =>
    byRole.get(role) ?? {
      id: fallbackId,
      role,
      slaHours: role === 'RH' ? 48 : 24,
      actions: role === 'RH' ? (['Approve', 'Reject', 'Archive'] as const) : (['Validate', 'Reject'] as const),
      notificationType: 'email' as const,
      notificationEnabled: true,
    };
  return {
    ...payload,
    steps: [
      ensure('Coach', 'wf-coach'),
      ensure('Superviseur', 'wf-superviseur'),
      ensure('Manager', 'wf-manager'),
      ensure('RP', 'wf-rp'),
      { ...ensure('RH', 'wf-rh-final'), role: 'RH' },
    ],
  };
}

export const AdminPrimeService = {
  getDashboard: async (): Promise<{
    kpis: AdminSystemKpi;
    charts: AdminDashboardCharts;
    alerts: AdminSystemAlert[];
  }> => {
    const [summary, dash, anomalies] = await Promise.all([
      primeApiGet<{
        statusCounts: { status: string; count: number }[];
        terminalStatuses: string[];
        total: number;
      }>('/api/prime/validation/summary'),
      primeApiGet<DashboardStats>('/api/prime/dashboard-stats'),
      primeApiGet<{ id: string; description: string; status: string; detectedAt: string; severity: string }[]>(
        '/api/prime/admin/anomalies?take=50',
      ).catch(() => []),
    ]);

    const terminalSet = new Set(summary.terminalStatuses ?? []);
    const validationsInProgress = (summary.statusCounts ?? [])
      .filter((x) => !terminalSet.has(x.status))
      .reduce((a, x) => a + x.count, 0);

    const vol = (dash.primeEvolution ?? []).map((x) => ({
      month: String(x.month),
      value: x.amount,
    }));
    const byDept = (dash.primeByDepartment ?? []).map((x) => ({
      name: x.name,
      value: x.value,
    }));
    const validationRate = vol.map((v) => ({
      month: v.month,
      value: summary.total > 0 ? Math.min(100, Math.round((100 * v.value) / Math.max(summary.total, 1))) : 0,
    }));

    const alerts: AdminSystemAlert[] = anomalies
      .filter((a) => a.status === 'Open' || a.status === 'InReview')
      .slice(0, 8)
      .map((a) => ({
        id: a.id,
        type: 'Incoherence',
        message: a.description,
        severity: (a.severity === 'Critical' || a.severity === 'High' ? 'Haute' : 'Moyenne') as AdminSystemAlert['severity'],
        date: (a.detectedAt ?? '').replace('T', ' ').slice(0, 16),
      }));

    return {
      kpis: {
        totalGeneratedPrimes: summary.total,
        validationsInProgress,
        errorCount: anomalies.filter((a) => a.status === 'Open').length,
        avgProcessingTimeSec: dash.budgetConsumption ?? 0,
      },
      charts: {
        volumeByMonth: vol.length > 0 ? vol : [{ month: '-', value: 0 }],
        validationRate: validationRate.length > 0 ? validationRate : [{ month: '-', value: 0 }],
        byDepartment: byDept.length > 0 ? byDept : [{ name: '-', value: 0 }],
      },
      alerts: alerts.length > 0 ? alerts : [],
    };
  },

  getRbacMatrix: async (): Promise<AdminRbacRow[]> => {
    const rows = await primeApiGet<{ role: string; action: string; isAllowed: boolean }[]>(
      '/api/prime/admin/rbac',
    ).catch(() => []);
    if (rows.length === 0) {
      return [
        { role: 'Admin', read: true, edit: true, validate: true, configure: true },
        { role: 'Superviseur', read: true, edit: true, validate: true, configure: false },
        { role: 'Référent technique', read: true, edit: true, validate: true, configure: false },
        { role: 'Chef de projet', read: true, edit: false, validate: true, configure: false },
        { role: 'RH', read: true, edit: false, validate: true, configure: true },
        { role: 'Pilote', read: true, edit: true, validate: false, configure: false },
        { role: 'Audit', read: true, edit: false, validate: false, configure: false },
        { role: 'Comptabilité', read: true, edit: false, validate: false, configure: false },
      ];
    }
    const byRole = new Map<string, AdminRbacRow>();
    for (const r of rows) {
      const cur = byRole.get(r.role) ?? {
        role: r.role,
        read: false,
        edit: false,
        validate: false,
        configure: false,
      };
      const act = String(r.action).toLowerCase();
      if (act === 'read' && r.isAllowed) cur.read = true;
      if (act === 'edit' && r.isAllowed) cur.edit = true;
      if (act === 'validate' && r.isAllowed) cur.validate = true;
      if (act === 'configure' && r.isAllowed) cur.configure = true;
      byRole.set(r.role, cur);
    }
    return [...byRole.values()];
  },

  toggleRbacPermission: async (_role: string, _permission: 'read' | 'edit' | 'validate' | 'configure'): Promise<AdminRbacRow[]> =>
    AdminPrimeService.getRbacMatrix(),

  getWorkflowConfig: async (): Promise<AdminWorkflowConfig> => {
    const [steps, global] = await Promise.all([
      primeApiGet<WorkflowStepConfigDto[]>('/api/prime/admin/workflow/steps'),
      primeApiGet<WorkflowGlobalConfigDto>('/api/prime/admin/workflow/global'),
    ]);
    const ordered = steps.slice().sort((a, b) => a.sortOrder - b.sortOrder);
    return {
      steps: ordered.map((s) => ({
        id: s.id,
        role: mapApproverToUiRole(s.approverRole),
        slaHours: s.slaHours,
        actions: ['Validate', 'Reject'],
        notificationType: 'email',
        notificationEnabled: true,
      })),
      auditAccess: {
        enabled: global.notificationsEnabled,
        readOnly: true,
        logs: true,
        history: true,
        export: true,
      },
    };
  },

  saveWorkflowConfig: async (payload: AdminWorkflowConfig): Promise<AdminWorkflowConfig> => {
    const normalized = ensureRequiredWorkflowOrder(payload);
    const global = await primeApiGet<WorkflowGlobalConfigDto>('/api/prime/admin/workflow/global');
    await primeApiPut('/api/prime/admin/workflow/global', {
      notificationsEnabled: !!payload.auditAccess.enabled,
      globalSlaHours: global.globalSlaHours,
      allowBulkApprove: global.allowBulkApprove,
      requireRejectReason: global.requireRejectReason,
    });
    const raw = await primeApiGet<WorkflowStepConfigDto[]>('/api/prime/admin/workflow/steps');
    const rawByUiRole = new Map(raw.map((r) => [mapApproverToUiRole(r.approverRole), r]));
    for (const step of normalized.steps) {
      const row = rawByUiRole.get(step.role);
      if (!row) continue;
      await primeApiPut(`/api/prime/admin/workflow/steps/${encodeURIComponent(row.id)}`, {
        sortOrder: row.sortOrder,
        approverRole: row.approverRole,
        fromStatus: row.fromStatus,
        toStatus: row.toStatus,
        isActive: row.isActive,
        slaHours: step.slaHours,
        capturesAmountsOnApproval: row.capturesAmountsOnApproval ?? false,
        terminalApproved: row.terminalApproved ?? false,
      });
    }
    return AdminPrimeService.getWorkflowConfig();
  },

  getAuditLogs: async (): Promise<AdminAuditLog[]> => {
    const rows = await primeApiGet<
      { id: string; userDisplayName: string; action: string; at: string }[]
    >('/api/prime/admin/audit-logs?take=100');
    return rows.map((r) => ({
      id: r.id,
      user: r.userDisplayName,
      action: r.action,
      date: (r.at ?? '').replace('T', ' ').slice(0, 16),
    }));
  },

  getAnomalies: async (): Promise<AdminAnomaly[]> => {
    const rows = await primeApiGet<
      { id: string; type: string; description: string; status: string }[]
    >('/api/prime/admin/anomalies?take=100');
    return rows.map((r) => ({
      id: r.id,
      type: 'Erreur de calcul',
      description: r.description,
      status: r.status === 'Resolved' ? 'Corrigee' : r.status === 'Ignored' ? 'Ignoree' : 'Ouverte',
    }));
  },

  updateAnomalyStatus: async (id: string, status: 'Corrigee' | 'Ignoree'): Promise<AdminAnomaly[]> => {
    const apiStatus = status === 'Corrigee' ? 'Resolved' : 'Ignored';
    await primeApiPut(`/api/prime/admin/anomalies/${encodeURIComponent(id)}`, {
      status: apiStatus,
      resolvedByUserId: 'e-admin',
      resolutionNote: 'Mis à jour depuis le module PRIME',
    });
    return AdminPrimeService.getAnomalies();
  },
};
