import type { Employee } from '../models';
import { DEMO_DEPARTMENTS, DEMO_EMPLOYEES } from './prime-demo-org';
import {
  buildDemoEmployeeFicheList,
  demoMyResults,
  DEMO_ACTIVE_DRAFTS,
  DEMO_ADMIN_ANOMALIES,
  DEMO_ADMIN_AUDIT_LOGS,
  DEMO_CELLS_SUMMARY,
  DEMO_DASHBOARD_STATS,
  DEMO_GLOBAL_POOL_STATE,
  DEMO_INDICATORS,
  DEMO_ORG_OVERVIEW,
  DEMO_PERIODS,
  DEMO_PRIME_RESULTS,
  DEMO_PRIME_RULES,
  DEMO_PRIME_TYPES,
  DEMO_RBAC_CATALOG,
  DEMO_RBAC_PERMISSIONS,
  DEMO_RP_DASHBOARD,
  DEMO_RP_TEAM,
  DEMO_RP_VALIDATIONS,
  DEMO_SUPERVISOR_SCOPE,
  DEMO_VALIDATION_FICHES,
  DEMO_VALIDATION_SUMMARY,
  DEMO_WORKFLOW_GLOBAL,
  DEMO_WORKFLOW_META,
  DEMO_WORKFLOW_STEPS,
} from './prime-demo-dataset';

function pathOnly(url: string): string {
  try {
    const u = url.startsWith('http') ? new URL(url) : new URL(url, 'http://local');
    return u.pathname;
  } catch {
    return url.split('?')[0] ?? url;
  }
}

function queryParams(url: string): URLSearchParams {
  try {
    const u = url.startsWith('http') ? new URL(url) : new URL(url, 'http://local');
    return u.searchParams;
  } catch {
    const q = url.indexOf('?');
    return new URLSearchParams(q >= 0 ? url.slice(q + 1) : '');
  }
}

/** Réponse vide ou non exploitable pour l’UI de démo. */
export function isPrimeDemoEmptyPayload(body: unknown): boolean {
  if (body == null) return true;
  if (Array.isArray(body)) return body.length === 0;
  if (typeof body === 'object') {
    const o = body as Record<string, unknown>;
    const total = o['total'];
    const statusCounts = o['statusCounts'];
    const primeEvolution = o['primeEvolution'];
    const memberPerformance = o['memberPerformance'];
    if (typeof total === 'number' && total === 0) {
      if (Array.isArray(statusCounts) && statusCounts.length === 0) return true;
    }
    if (
      Array.isArray(statusCounts) &&
      statusCounts.length === 0 &&
      (total === 0 || total == null)
    ) {
      return true;
    }
    if (Array.isArray(primeEvolution) && primeEvolution.length === 0) return true;
    if (Array.isArray(memberPerformance) && memberPerformance.length === 0) return true;
  }
  return false;
}

/**
 * Résout une réponse mock pour les GET /api/prime et /api/rp (démo soutenance).
 * Retourne `undefined` si aucun mock ne s’applique à cette URL.
 */
export function resolvePrimeDemoGet(url: string, method = 'GET'): unknown | undefined {
  if (method !== 'GET') return undefined;
  const path = pathOnly(url);
  const q = queryParams(url);

  if (path === '/api/prime/employees') return DEMO_EMPLOYEES;
  if (path === '/api/prime/departments') return DEMO_DEPARTMENTS;
  if (path === '/api/prime/types') return DEMO_PRIME_TYPES;
  if (path === '/api/prime/rules') return DEMO_PRIME_RULES;
  if (path === '/api/prime/results') return DEMO_PRIME_RESULTS;
  if (path === '/api/prime/dashboard-stats') return DEMO_DASHBOARD_STATS;

  if (path === '/api/prime/my-results') {
    const employeeId = q.get('employeeId') ?? '';
    return demoMyResults(employeeId);
  }

  if (path === '/api/prime/validation/summary') return DEMO_VALIDATION_SUMMARY;
  if (path === '/api/prime/validation/workflow-meta') return DEMO_WORKFLOW_META;
  if (path === '/api/prime/validation/periods') return DEMO_PERIODS;
  if (path === '/api/prime/validation') return DEMO_VALIDATION_FICHES;

  if (path === '/api/prime/org/supervisor-scope') return DEMO_SUPERVISOR_SCOPE;
  if (path === '/api/prime/org/etages') return DEMO_ORG_OVERVIEW.etages;
  if (path === '/api/prime/org/services') return DEMO_ORG_OVERVIEW.services;
  if (path === '/api/prime/org/sous-services') return DEMO_ORG_OVERVIEW.sousServices;
  if (path.startsWith('/api/prime/org/assignments/')) {
    const uid = q.get('userId') ?? q.get('coachUserId') ?? '';
    if (path.includes('manager-etage')) {
      return DEMO_ORG_OVERVIEW.managerEtage.filter((a) => !uid || a.userId === uid);
    }
    if (path.includes('supervisor-service')) {
      return DEMO_ORG_OVERVIEW.supervisorService.filter((a) => !uid || a.userId === uid);
    }
    if (path.includes('coach-sous-service')) {
      return DEMO_ORG_OVERVIEW.coachSousService.filter((a) => !uid || a.userId === uid);
    }
    if (path.includes('coach-pilot')) {
      return DEMO_ORG_OVERVIEW.coachPilot.filter((a) => !uid || a.coachUserId === uid);
    }
  }

  const indMatch = path.match(/^\/api\/prime\/services\/([^/]+)\/prime-indicators$/);
  if (indMatch) return DEMO_INDICATORS.map((i) => ({ ...i, serviceId: indMatch[1] }));

  if (path === '/api/prime/supervisor-pole-prime-drafts/list-active') return DEMO_ACTIVE_DRAFTS;

  const draftMatch = path.match(/^\/api\/prime\/supervisor-pole-prime-drafts\/([^/]+)\/global-pool$/);
  if (draftMatch) {
    return { ...DEMO_GLOBAL_POOL_STATE, draftId: draftMatch[1] };
  }

  if (path === '/api/prime/supervisor-pole-prime-drafts') {
    const period = q.get('period') ?? '';
    const celluleId = q.get('celluleId') ?? q.get('poleId') ?? '';
    const draft = DEMO_ACTIVE_DRAFTS.find(
      (d) => d.period === period && (d.celluleId === celluleId || !celluleId),
    );
    if (draft) {
      return {
        id: draft.id,
        supervisorUserId: draft.supervisorUserId ?? 'e-sup-nadia',
        celluleId: draft.celluleId,
        period: draft.period,
        templateId: draft.templateId,
        templateDisplayName: draft.templateDisplayName,
        templateFormatVersion: draft.templateFormatVersion,
        status: draft.status,
        schemaJson: '{}',
        celluleSaisieJson: '{}',
        computedJson: null,
        templateCalcSnapshotJson: null,
        updatedAt: draft.updatedAt,
      };
    }
  }

  if (path === '/api/prime/employee-prime-cell-fiches/list') {
    const serviceId = q.get('serviceId') ?? undefined;
    return buildDemoEmployeeFicheList(serviceId ?? undefined);
  }

  if (path === '/api/prime/employee-prime-cell-fiches/for-employee') {
    const employeeId = q.get('employeeId') ?? '';
    const emp = DEMO_EMPLOYEES.find((e: Employee) => e.id === employeeId);
    if (!emp) return undefined;
    return {
      id: `fiche-pilote-${employeeId}`,
      cellulePrimeDraftId: DEMO_ACTIVE_DRAFTS[0]?.id ?? 'draft-demo',
      supervisorUserId: 'e-sup-nadia',
      employeeId,
      serviceId: emp.serviceId,
      celluleId: emp.celluleId,
      period: q.get('period') ?? '2026-05',
      serviceSaisieJson: '{}',
      fillingStatus: 'Complete',
      validationStatus: 'Pending',
      isReadyForValidation: true,
      updatedAt: '2026-05-19T12:00:00.000Z',
    };
  }

  if (path === '/api/prime/pilotage/cells-summary') return DEMO_CELLS_SUMMARY;

  if (path === '/api/prime/admin/anomalies') return DEMO_ADMIN_ANOMALIES;
  if (path === '/api/prime/admin/audit-logs') return DEMO_ADMIN_AUDIT_LOGS;
  if (path === '/api/prime/admin/workflow/steps') return DEMO_WORKFLOW_STEPS;
  if (path === '/api/prime/admin/workflow/global') return DEMO_WORKFLOW_GLOBAL;
  if (path === '/api/prime/admin/rbac') return DEMO_RBAC_PERMISSIONS;
  if (path === '/api/prime/admin/rbac/catalog') return DEMO_RBAC_CATALOG;

  if (path === '/api/rp/assigned-project-ids') return ['proj-crm-2026', 'proj-devops-2026'];
  if (path === '/api/rp/dashboard-stats') return DEMO_RP_DASHBOARD;
  if (path === '/api/rp/team-performance') return DEMO_RP_TEAM;
  if (path === '/api/rp/manager-validated') return DEMO_RP_VALIDATIONS;

  return undefined;
}
