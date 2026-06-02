import { Injectable, inject } from '@angular/core';
import {
  DEFAULT_SYSTEM_CONFIG,
  type AuditLogEntry,
  type SystemConfig,
} from '../models/system-config.model';
import { normalizeReferralProgramRules, syncLegacyBonusFields } from '../lib/referral-program';
import { ParrainageApiService } from './parrainage-api.service';
import { ParrainageStoreService } from './parrainage-store.service';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly store = inject(ParrainageStoreService);
  private readonly api = inject(ParrainageApiService);

  getSystemConfig(): SystemConfig {
    return this.store.systemConfig();
  }

  async updateSystemConfig(
    config: Partial<SystemConfig>,
    actor: { id: string; label: string; role?: string },
  ): Promise<SystemConfig> {
    const current = this.getSystemConfig();
    let payload: Partial<SystemConfig> = { ...config };
    if (actor.role === 'RH' && current.adminWorkflow) {
      payload = { ...payload, adminWorkflow: current.adminWorkflow };
    }
    const next = await this.api.updateConfig({ ...payload, actor });
    const normalized = this.normalizeFullConfig(next);
    this.store.systemConfig.set(normalized);
    await this.store.refreshCore();
    return normalized;
  }

  getAuditLog(): AuditLogEntry[] {
    return [...this.store.auditLog()].sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime());
  }

  async addAuditLog(entry: Omit<AuditLogEntry, 'id' | 'timestamp'>): Promise<void> {
    await this.api.addAudit({
      action: entry.action,
      userId: entry.userId,
      userLabel: entry.userLabel,
      details: entry.details,
    });
    const audit = await this.api.getAudit();
    this.store.auditLog.set(audit);
  }

  private normalizeWorkflow(cfg: SystemConfig): SystemConfig['adminWorkflow'] {
    const fallback = DEFAULT_SYSTEM_CONFIG.adminWorkflow!;
    const raw = cfg.adminWorkflow ?? fallback;
    const allowedRoles = new Set(['Coach', 'Manager', 'RP', 'RH']);
    const allowedActions = new Set(['Validate', 'Reject', 'Approve', 'Archive']);
    const cleaned = raw.steps
      .filter((s) => allowedRoles.has(s.role))
      .map((s, i) => ({
        ...s,
        id: s.id || `wf-step-${i + 1}`,
        slaHours: Number.isFinite(s.slaHours) && s.slaHours >= 0 ? s.slaHours : 24,
        actions: s.actions.filter((a) => allowedActions.has(a)),
      }));
    const byRole = new Map(cleaned.map((s) => [s.role, s]));
    const ensure = (role: 'Coach' | 'Manager' | 'RP' | 'RH') =>
      byRole.get(role) ?? fallback.steps.find((s) => s.role === role)!;
    return {
      steps: [ensure('Coach'), ensure('Manager'), ensure('RP'), { ...ensure('RH'), role: 'RH' }],
      auditAccess: {
        enabled: !!raw.auditAccess.enabled,
        readOnly: true,
        logs: !!raw.auditAccess.logs,
        history: !!raw.auditAccess.history,
        export: !!raw.auditAccess.export,
      },
    };
  }

  private normalizeFullConfig(cfg: SystemConfig): SystemConfig {
    const rules = normalizeReferralProgramRules(cfg);
    const legacy = syncLegacyBonusFields(rules);
    const wf = this.normalizeWorkflow({ ...cfg, referralProgramRules: rules, ...legacy });
    return { ...cfg, referralProgramRules: rules, ...legacy, adminWorkflow: wf };
  }
}
