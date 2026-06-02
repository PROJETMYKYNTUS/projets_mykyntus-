import type {
  NotificationPreferences,
  Referral,
  ReferralHistoryEntry,
  ReferralNotification,
  ReferralRule,
} from '../models/referral.model';
import { DEFAULT_SYSTEM_CONFIG, type AuditLogEntry, type SystemConfig } from '../models/system-config.model';

function buildReferrals(): Referral[] {
  const now = Date.now();
  const base: Omit<Referral, 'createdAt' | 'rewardAmount'>[] = [
    { id: 'ref-1001', referrerId: 'emp-1', referrerName: 'Jean Dupont', projectId: 'proj-1', projectName: 'Alpha Digital', teamId: 'team-a', candidateName: 'Claire Martin', candidateEmail: 'claire.martin@email.com', candidatePhone: '+33 6 12 34 56 78', position: 'Développeur Full-Stack', status: 'SUBMITTED', paymentStatus: 'NOT_ELIGIBLE' },
    { id: 'ref-1002', referrerId: 'emp-1', referrerName: 'Jean Dupont', projectId: 'proj-1', projectName: 'Alpha Digital', teamId: 'team-a', candidateName: 'Paul Bernard', candidateEmail: 'paul.bernard@email.com', candidatePhone: '+33 6 98 76 54 32', position: 'Chef de projet', status: 'PROCESSED', paymentStatus: 'NOT_ELIGIBLE' },
    { id: 'ref-1003', referrerId: 'emp-2', referrerName: 'Sophie Leroy', projectId: 'proj-2', projectName: 'Beta Ops', teamId: 'team-b', candidateName: 'Luc Petit', candidateEmail: 'luc.petit@email.com', candidatePhone: '+33 6 11 22 33 44', position: 'Analyste data', status: 'REJECTED', paymentStatus: 'NOT_ELIGIBLE' },
    { id: 'ref-1004', referrerId: 'emp-2', referrerName: 'Sophie Leroy', projectId: 'proj-2', projectName: 'Beta Ops', teamId: 'team-b', candidateName: 'Nadia Kaci', candidateEmail: 'nadia.kaci@email.com', candidatePhone: '+33 6 55 66 77 88', position: 'Développeur', status: 'REWARDED', paymentStatus: 'PAID' },
    { id: 'ref-1005', referrerId: 'emp-3', referrerName: 'Thomas Bernard', projectId: 'proj-3', projectName: 'Gamma Cloud', teamId: 'team-c', candidateName: 'Amélie Rousseau', candidateEmail: 'amelie.rousseau@email.com', candidatePhone: '+33 6 44 55 66 77', position: 'DevOps', status: 'SUBMITTED', paymentStatus: 'NOT_ELIGIBLE' },
  ];
  return base.map((r, idx) => ({
    ...r,
    rewardAmount: r.status === 'APPROVED' ? 750 : r.status === 'REWARDED' ? 600 + (idx % 3) * 50 : 0,
    createdAt: new Date(now - idx * 1000 * 60 * 60 * 12),
  }));
}

function buildRules(): ReferralRule[] {
  const now = Date.now();
  return [
    { id: 'rule-1', name: 'Récompense Développeur', type: 'REWARD_PER_POSITION', target: 'Développeur', value: 600, status: 'ACTIVE', createdAt: new Date(now - 86400000 * 30) },
    { id: 'rule-2', name: 'Récompense Chef de projet', type: 'REWARD_PER_POSITION', target: 'Chef de projet', value: 750, status: 'ACTIVE', createdAt: new Date(now - 86400000 * 30) },
    { id: 'rule-3', name: 'Récompense post-probatoire', type: 'REWARD_AFTER_PROBATION', value: 200, status: 'PAUSED', createdAt: new Date(now - 86400000 * 25) },
  ];
}

function buildHistoryAndNotifications(referrals: Referral[]) {
  const history: ReferralHistoryEntry[] = [];
  const notifications: ReferralNotification[] = [];
  for (const r of referrals) {
    history.push({
      id: `hist-${r.id}-sub`,
      referralId: r.id,
      candidateName: r.candidateName,
      action: 'SUBMITTED',
      performedById: r.referrerId,
      performedByLabel: r.referrerName,
      createdAt: r.createdAt,
    });
    notifications.push({
      id: `nt-${r.id}-sub`,
      type: 'NEW_REFERRAL',
      message: `Nouveau parrainage : ${r.candidateName} (${r.position})`,
      createdAt: r.createdAt,
      read: false,
      referralId: r.id,
      referrerId: r.referrerId,
      targetRoles: ['RH', 'ADMIN', 'MANAGER', 'COACH', 'RP'],
    });
  }
  return { history, notifications };
}

const DEMO_REFERRALS = buildReferrals();
const DEMO_RULES = buildRules();
const { history: DEMO_HISTORY, notifications: DEMO_NOTIFICATIONS } = buildHistoryAndNotifications(DEMO_REFERRALS);

const DEMO_PREFS: NotificationPreferences = {
  email: true,
  inApp: true,
  systemAlerts: true,
  referrals: true,
  approvals: true,
  payments: true,
};

const DEMO_CONFIG: SystemConfig = { ...DEFAULT_SYSTEM_CONFIG };
const DEMO_AUDIT: AuditLogEntry[] = [];

export const PARRAINAGE_DEMO = {
  referrals: DEMO_REFERRALS,
  rules: DEMO_RULES,
  history: DEMO_HISTORY,
  notifications: DEMO_NOTIFICATIONS,
  notificationPreferences: DEMO_PREFS,
  systemConfig: DEMO_CONFIG,
  auditLog: DEMO_AUDIT,
};
