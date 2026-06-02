import { PARRAINAGE_DEMO } from './parrainage-demo-dataset';

function pathOnly(url: string): string {
  try {
    const u = url.startsWith('http') ? new URL(url) : new URL(url, 'http://local');
    return u.pathname;
  } catch {
    return url.split('?')[0] ?? url;
  }
}

export function isParrainageDemoEmptyPayload(body: unknown): boolean {
  if (body == null) return true;
  if (Array.isArray(body)) return body.length === 0;
  if (typeof body === 'object') {
    const o = body as Record<string, unknown>;
    if (Array.isArray(o['duplicateCandidates']) && (o['duplicateCandidates'] as unknown[]).length === 0) {
      if (Array.isArray(o['suspiciousEmails']) && (o['suspiciousEmails'] as unknown[]).length === 0) return true;
    }
  }
  return false;
}

export function resolveParrainageDemoGet(url: string, method = 'GET'): unknown | undefined {
  if (method !== 'GET') return undefined;
  const path = pathOnly(url);

  if (path === '/api/parrainage/referrals') return PARRAINAGE_DEMO.referrals;
  if (path === '/api/parrainage/referrals/history') return PARRAINAGE_DEMO.history;
  if (path === '/api/parrainage/rules') return PARRAINAGE_DEMO.rules;
  if (path === '/api/parrainage/notifications/preferences') return PARRAINAGE_DEMO.notificationPreferences;
  if (path === '/api/parrainage/config') return PARRAINAGE_DEMO.systemConfig;
  if (path === '/api/parrainage/audit') return PARRAINAGE_DEMO.auditLog;
  if (path === '/api/parrainage/admin/export') {
    return {
      exportedAt: new Date().toISOString(),
      referrals: PARRAINAGE_DEMO.referrals,
      rules: PARRAINAGE_DEMO.rules,
      history: PARRAINAGE_DEMO.history,
      notifications: PARRAINAGE_DEMO.notifications,
      notificationPreferences: PARRAINAGE_DEMO.notificationPreferences,
      systemConfig: PARRAINAGE_DEMO.systemConfig,
      auditLog: PARRAINAGE_DEMO.auditLog,
    };
  }
  if (path.startsWith('/api/parrainage/notifications')) return PARRAINAGE_DEMO.notifications;
  if (path === '/api/parrainage/anomalies') {
    return { duplicateCandidates: [], suspiciousEmails: [] };
  }
  if (path === '/api/parrainage/health' || path === '/api/parrainage/ping') {
    return { status: 'healthy', service: 'parrainage-service-mock' };
  }
  return undefined;
}
