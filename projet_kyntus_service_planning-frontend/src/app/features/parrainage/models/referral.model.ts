export type ReferralStatus = 'SUBMITTED' | 'PROCESSED' | 'APPROVED' | 'REJECTED' | 'REWARDED';

export type ReferralPaymentStatus = 'NOT_ELIGIBLE' | 'AWAITING_RH' | 'READY' | 'PAID';

export type ReferralPositionMode = 'CATALOG' | 'CUSTOM';

export interface Referral {
  id: string;
  referrerId: string;
  referrerName: string;
  projectId: string;
  projectName: string;
  teamId: string;
  candidateName: string;
  candidateEmail: string;
  candidatePhone: string;
  position: string;
  positionMode?: ReferralPositionMode;
  appliedRuleId?: string;
  status: ReferralStatus;
  rewardAmount: number;
  cvUrl?: string;
  notes?: string;
  candidateStartDate?: string;
  approvedAt?: Date;
  eligibleForPaymentAt?: Date;
  paymentStatus: ReferralPaymentStatus;
  paidAt?: Date;
  paidByUserId?: string;
  paidByLabel?: string;
  paymentReference?: string;
  createdAt: Date;
}

export type ReferralHistoryAction =
  | 'SUBMITTED'
  | 'PROCESSED'
  | 'APPROVED'
  | 'REJECTED'
  | 'REWARDED'
  | 'ELIGIBILITY_DUE'
  | 'ELIGIBILITY_CONFIRMED'
  | 'PAYMENT_READY'
  | 'PAYMENT_UNDONE';

export interface ReferralHistoryEntry {
  id: string;
  referralId: string;
  candidateName: string;
  action: ReferralHistoryAction;
  performedById: string;
  performedByLabel: string;
  createdAt: Date;
  comment?: string;
  rewardAmount?: number;
}

export type NotificationAudienceRole =
  | 'PILOTE'
  | 'RH'
  | 'ADMIN'
  | 'MANAGER'
  | 'COACH'
  | 'RP'
  | 'COMPTA'
  | 'COMPTABILITE'
  | 'ALL';

export interface ReferralNotification {
  id: string;
  type: 'NEW_REFERRAL' | 'STATUS_CHANGED' | 'REFERRAL_REWARDED' | 'REFERRAL_ELIGIBILITY_DUE' | 'REFERRAL_PAYMENT_READY';
  message: string;
  createdAt: Date;
  read: boolean;
  referralId?: string;
  referrerId?: string;
  targetRoles?: NotificationAudienceRole[];
}

export interface NotificationPreferences {
  email: boolean;
  inApp: boolean;
  systemAlerts?: boolean;
  referrals?: boolean;
  approvals?: boolean;
  payments?: boolean;
}

export type ReferralRuleType = 'REWARD_PER_POSITION' | 'REWARD_AFTER_PROBATION';
export type ReferralRuleStatus = 'ACTIVE' | 'PAUSED';

export interface ReferralRule {
  id: string;
  name: string;
  type: ReferralRuleType;
  value: number;
  target?: string;
  minDurationMonths: number;
  status: ReferralRuleStatus;
  createdAt: Date;
}

export interface ReferralRuleCatalogItem {
  ruleId: string;
  target: string;
  value: number;
  minDurationMonths: number;
}

export interface ReferralRewardPreview {
  suggestedAmount: number;
  minDurationMonths: number;
  ruleLabel: string;
  appliedRuleId?: string;
  positionMode: ReferralPositionMode;
}

export type ParrainageRole =
  | 'PILOTE'
  | 'RH'
  | 'ADMIN'
  | 'MANAGER'
  | 'COACH'
  | 'RP'
  | 'AUDIT'
  | 'COMPTA';

export type RoleFilter = ParrainageRole;

export interface ParrainageUser {
  id: string;
  name: string;
  email?: string;
  role: ParrainageRole;
  parentId?: string;
  projectId?: string;
  departmentId?: string;
  poleId?: string;
  celluleId?: string;
}
