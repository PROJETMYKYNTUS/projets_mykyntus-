import { Injectable, inject } from '@angular/core';
import type {
  NotificationAudienceRole,
  NotificationPreferences,
  Referral,
  ReferralHistoryEntry,
  ReferralNotification,
  ReferralRule,
  ReferralStatus,
  RoleFilter,
} from '../models/referral.model';
import { AdminService } from './admin.service';
import { ParrainageApiService } from './parrainage-api.service';
import { ParrainageStoreService } from './parrainage-store.service';
import { isReferrerUnderManager } from '../lib/org-hierarchy';
import { accruedBonusDH, totalPotentialBonusDH } from '../lib/referral-program';
import { ParrainageRoleService } from '../state/parrainage-role.service';

type Actor = { id: string; label: string };

@Injectable({ providedIn: 'root' })
export class ReferralService {
  private readonly store = inject(ParrainageStoreService);
  private readonly api = inject(ParrainageApiService);
  private readonly admin = inject(AdminService);
  private readonly roleSvc = inject(ParrainageRoleService);

  getAllReferrals(): Referral[] {
    return this.store.referrals();
  }

  getReferralById(id: string): Referral | undefined {
    return this.store.referrals().find((r) => r.id === id);
  }

  async updateStatus(
    id: string,
    status: ReferralStatus,
    actor?: Actor,
    comment?: string,
  ): Promise<Referral | undefined> {
    try {
      const updated = await this.api.updateStatus(id, status, actor, comment);
      this.store.patchReferral(updated);
      await this.refreshAfterMutation();
      return updated;
    } catch {
      return undefined;
    }
  }

  async forceApprove(id: string, actor?: Actor): Promise<Referral | undefined> {
    const ref = this.getReferralById(id);
    const resolvedActor = actor ?? { id: 'admin-1', label: 'Administrateur' };
    if (ref?.status === 'SUBMITTED') {
      await this.processReferral(id, 'Traitement automatique (admin)', resolvedActor);
    }
    const amount = this.getSuggestedReward(id) || 1500;
    return this.approveReferral(
      id,
      { candidateStartDate: new Date().toISOString().slice(0, 10), rewardAmount: amount },
      resolvedActor,
    );
  }

  async forceReject(id: string, actor?: Actor, reason?: string): Promise<Referral | undefined> {
    return this.updateStatus(
      id,
      'REJECTED',
      actor ?? { id: 'admin-1', label: 'Administrateur' },
      reason ?? 'Rejet opérationnel',
    );
  }

  async assignReward(id: string, amount: number, actor?: Actor): Promise<Referral | undefined> {
    return this.markReferralPaid(id, { paid: true }, actor);
  }

  async approveReferral(
    id: string,
    data: { candidateStartDate: string; rewardAmount: number; comment?: string },
    actor?: Actor,
  ): Promise<Referral | undefined> {
    try {
      const updated = await this.api.approveReferral(id, { ...data, actor });
      this.store.patchReferral(updated);
      await this.refreshAfterMutation();
      return updated;
    } catch {
      return undefined;
    }
  }

  async processReferral(id: string, comment?: string, actor?: Actor): Promise<Referral | undefined> {
    try {
      const updated = await this.api.processReferral(id, { comment, actor });
      this.store.patchReferral(updated);
      await this.refreshAfterMutation();
      return updated;
    } catch {
      return undefined;
    }
  }

  async markReferralPaid(
    id: string,
    body: { paid: boolean; paidAt?: string; reference?: string },
    actor?: Actor,
  ): Promise<Referral | undefined> {
    try {
      const updated = await this.api.markReferralPaid(id, { ...body, actor });
      this.store.patchReferral(updated);
      await this.refreshAfterMutation();
      return updated;
    } catch {
      return undefined;
    }
  }

  async getPaymentsInbox() {
    return this.api.getPaymentsInbox();
  }

  async payAllReferrals(actor?: Actor): Promise<void> {
    await this.api.payAllReferrals({ actor });
    await this.refreshAfterMutation();
  }

  async confirmPaymentEligibility(
    id: string,
    comment?: string,
    actor?: Actor,
  ): Promise<Referral | undefined> {
    try {
      const updated = await this.api.confirmPaymentEligibility(id, { comment, actor });
      this.store.patchReferral(updated);
      await this.refreshAfterMutation();
      return updated;
    } catch {
      return undefined;
    }
  }

  paymentStatusLabel(referral: Referral): string {
    if (referral.status === 'REJECTED') return 'Rejeté';
    if (referral.status === 'SUBMITTED') return 'En attente RH';
    if (referral.status === 'PROCESSED') return 'Traité — attente entrée';
    if (referral.status === 'REWARDED') return 'Versé';
    if (referral.paymentStatus === 'READY') return 'Prêt compta';
    if (referral.paymentStatus === 'AWAITING_RH') return 'À confirmer RH';
    if (referral.paymentStatus === 'NOT_ELIGIBLE') return 'Période en cours';
    return referral.paymentStatus;
  }

  daysUntilEligible(referral: Referral): number | null {
    if (!referral.eligibleForPaymentAt || referral.paymentStatus !== 'NOT_ELIGIBLE') return null;
    const ms = referral.eligibleForPaymentAt.getTime() - Date.now();
    return Math.max(0, Math.ceil(ms / (1000 * 60 * 60 * 24)));
  }

  async updateReferralManual(
    id: string,
    patch: Partial<
      Pick<Referral, 'candidateName' | 'candidateEmail' | 'candidatePhone' | 'position' | 'projectName' | 'status' | 'rewardAmount'>
    >,
    actor?: Actor,
  ): Promise<Referral | undefined> {
    try {
      const updated = await this.api.patchReferral(id, { ...patch, actor });
      this.store.patchReferral(updated);
      await this.refreshAfterMutation();
      return updated;
    } catch {
      return undefined;
    }
  }

  async exportDataSnapshot(): Promise<string> {
    try {
      const snap = await this.api.exportSnapshot();
      return JSON.stringify(snap, null, 2);
    } catch {
      return JSON.stringify(
        {
          exportedAt: new Date().toISOString(),
          referrals: this.store.referrals(),
          rules: this.store.rules(),
          history: this.store.history(),
          notifications: this.store.notifications(),
          notificationPreferences: this.store.notificationPrefs(),
          systemConfig: this.store.systemConfig(),
          auditLog: this.store.auditLog(),
        },
        null,
        2,
      );
    }
  }

  getHistory(): ReferralHistoryEntry[] {
    return [...this.store.history()].sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());
  }

  getRules(): ReferralRule[] {
    return [...this.store.rules()].sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());
  }

  async upsertRule(
    rule: Omit<ReferralRule, 'createdAt' | 'id'> & { id?: string; createdAt?: Date },
  ): Promise<ReferralRule> {
    const id = rule.id ?? `rule-${Date.now()}`;
    const saved = await this.api.upsertRule(id, {
      name: rule.name,
      type: rule.type,
      value: rule.value,
      target: rule.target,
      minDurationMonths: rule.minDurationMonths,
      status: rule.status === 'ACTIVE' ? 'ACTIVE' : 'PAUSED',
    });
    await this.store.refreshRules();
    return saved;
  }

  async deleteRule(ruleId: string): Promise<boolean> {
    try {
      await this.api.deleteRule(ruleId);
      await this.store.refreshRules();
      return true;
    } catch {
      return false;
    }
  }

  getNotificationPreferences(): NotificationPreferences {
    return this.store.notificationPrefs();
  }

  async updateNotificationPreferences(prefs: NotificationPreferences): Promise<void> {
    const next = await this.api.updateNotificationPreferences(prefs);
    this.store.notificationPrefs.set(next);
  }

  getNotifications(): ReferralNotification[] {
    return [...this.store.notifications()].sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());
  }

  getNotificationsForRole(role: RoleFilter, user: { id: string; projectId?: string }): ReferralNotification[] {
    const all = this.getNotifications();
    const referrals = this.store.referrals();
    return all.filter((n) => {
      const targets = n.targetRoles;
      if (targets?.length && !targets.includes('ALL')) {
        const roleTarget = role as NotificationAudienceRole;
        if (
          !targets.includes(roleTarget) &&
          !(role === 'COMPTA' && targets.includes('COMPTABILITE'))
        )
          return false;
      }
      if (role === 'PILOTE') {
        if (n.referrerId && n.referrerId !== user.id) return false;
        if (n.referralId) {
          const ref = referrals.find((r) => r.id === n.referralId);
          if (ref && ref.referrerId !== user.id) return false;
        }
      }
      if ((role === 'MANAGER' || role === 'COACH') && n.referralId) {
        const ref = referrals.find((r) => r.id === n.referralId);
        if (ref && !isReferrerUnderManager(user.id, ref.referrerId)) return false;
      }
      return true;
    });
  }

  async markNotificationAsRead(id: string): Promise<void> {
    await this.api.markNotificationRead(id);
    const u = this.roleSvc.user();
    await this.store.refreshNotifications(u.role, u.id, u.projectId);
  }

  async markAllNotificationsAsRead(): Promise<void> {
    await this.api.markAllNotificationsRead();
    const u = this.roleSvc.user();
    await this.store.refreshNotifications(u.role, u.id, u.projectId);
  }

  getSuggestedReward(referralId: string): number {
    const referral = this.getReferralById(referralId);
    if (!referral) return 0;
    if (referral.appliedRuleId) {
      const rule = this.getRules().find((r) => r.id === referral.appliedRuleId);
      if (rule) return rule.value;
    }
    return this.admin.getSystemConfig().defaultBonusAmount;
  }

  getSuggestedMinDuration(referralId: string): number {
    const referral = this.getReferralById(referralId);
    if (!referral) return this.admin.getSystemConfig().minDurationMonths;
    if (referral.appliedRuleId) {
      const rule = this.getRules().find((r) => r.id === referral.appliedRuleId);
      if (rule?.minDurationMonths) return rule.minDurationMonths;
    }
    return this.admin.getSystemConfig().minDurationMonths;
  }

  getRuleLabelForReferral(referralId: string): string {
    const referral = this.getReferralById(referralId);
    if (!referral) return '';
    if (referral.appliedRuleId) {
      const rule = this.getRules().find((r) => r.id === referral.appliedRuleId);
      if (rule) {
        return `Poste ${rule.target} (${rule.value} DH, ${rule.minDurationMonths} mois)`;
      }
    }
    const cfg = this.admin.getSystemConfig();
    return `Règle générale (${cfg.defaultBonusAmount} DH, ${cfg.minDurationMonths} mois)`;
  }

  async getRulesCatalog() {
    return this.api.getRulesCatalog();
  }

  async getRewardPreview(referralId: string) {
    return this.api.getRewardPreview(referralId);
  }

  getTotalReferralBonusPotentialDH(referralId: string): number {
    const referral = this.getReferralById(referralId);
    if (!referral) return 0;
    return totalPotentialBonusDH(this.admin.getSystemConfig().referralProgramRules!);
  }

  getAccruedReferralBonusDH(referralId: string): number {
    const referral = this.getReferralById(referralId);
    if (!referral) return 0;
    return accruedBonusDH(referral.createdAt, this.admin.getSystemConfig().referralProgramRules!);
  }

  async submitReferral(data: {
    referrerId: string;
    referrerName: string;
    candidateName: string;
    candidateEmail: string;
    candidatePhone: string;
    ruleId?: string;
    position?: string;
    project?: string;
    notes?: string;
    cvFile: File;
  }): Promise<Referral> {
    const created = await this.api.createReferral({
      referrerId: data.referrerId,
      referrerName: data.referrerName,
      candidateName: data.candidateName,
      candidateEmail: data.candidateEmail,
      candidatePhone: data.candidatePhone,
      ruleId: data.ruleId,
      position: data.position,
      project: data.project,
      notes: data.notes,
    });
    const result = await this.api.uploadReferralCv(created.id, data.cvFile);
    this.store.patchReferral(result);
    await this.refreshAfterMutation();
    return result;
  }

  detectAnomalies(): {
    duplicateCandidates: { email: string; referrals: Referral[] }[];
    suspiciousEmails: { email: string; count: number; referralIds: string[] }[];
  } {
    return this.detectAnomaliesFromReferrals(this.store.referrals());
  }

  private detectAnomaliesFromReferrals(referrals: Referral[]): {
    duplicateCandidates: { email: string; referrals: Referral[] }[];
    suspiciousEmails: { email: string; count: number; referralIds: string[] }[];
  } {
    const byEmail = new Map<string, Referral[]>();
    for (const r of referrals) {
      const k = r.candidateEmail.trim().toLowerCase();
      if (!byEmail.has(k)) byEmail.set(k, []);
      byEmail.get(k)!.push(r);
    }
    const duplicateCandidates = [...byEmail.entries()]
      .filter(([, arr]) => arr.length > 1)
      .map(([email, arr]) => ({ email, referrals: arr }));
    return {
      duplicateCandidates,
      suspiciousEmails: duplicateCandidates.map(({ email, referrals: arr }) => ({
        email,
        count: arr.length,
        referralIds: arr.map((x) => x.id),
      })),
    };
  }

  private async refreshAfterMutation(): Promise<void> {
    const u = this.roleSvc.user();
    await this.store.refreshCore(u.role, u.id, u.projectId);
  }
}
