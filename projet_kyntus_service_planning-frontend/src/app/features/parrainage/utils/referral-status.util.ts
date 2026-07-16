import type { ReferralStatus } from '../models/referral.model';

export const REFERRAL_STATUS_LABELS: Record<ReferralStatus, string> = {
  SUBMITTED: 'En attente',
  PROCESSED: 'Consulté',
  IN_TRAINING: 'En cours de formation',
  APPROVED: 'Validé',
  REJECTED: 'Rejeté',
  REWARDED: 'Prime versée',
};

export const REFERRAL_STATUS_STYLES: Record<ReferralStatus, string> = {
  SUBMITTED: 'ky-badge ky-badge--warning',
  PROCESSED: 'ky-badge ky-badge--info',
  IN_TRAINING: 'ky-badge ky-badge--warning',
  APPROVED: 'ky-badge ky-badge--success',
  REJECTED: 'ky-badge ky-badge--danger',
  REWARDED: 'ky-badge ky-badge--info',
};

/** @deprecated Use REFERRAL_STATUS_STYLES — kept as alias for backward compatibility. */
export const REFERRAL_STATUS_STYLES_RH: Record<ReferralStatus, string> = REFERRAL_STATUS_STYLES;

export const REFERRAL_PROCESSED_FILTER_LABEL = 'Consulté — attente entrée';
export const REFERRAL_PROCESSED_KPI_LABEL = 'Consultés — attente entrée';
export const REFERRAL_PROCESSED_PAYMENT_LABEL = 'Consulté — attente entrée';

export function referralStatusLabel(status: ReferralStatus): string {
  return REFERRAL_STATUS_LABELS[status] ?? status;
}

export function referralHistoryActionLabel(action: string): string {
  if (action === 'PROCESSED') return REFERRAL_STATUS_LABELS.PROCESSED;
  if (action === 'SUBMITTED') return REFERRAL_STATUS_LABELS.SUBMITTED;
  if (action === 'IN_TRAINING') return REFERRAL_STATUS_LABELS.IN_TRAINING;
  if (action === 'APPROVED') return REFERRAL_STATUS_LABELS.APPROVED;
  if (action === 'REJECTED') return REFERRAL_STATUS_LABELS.REJECTED;
  if (action === 'REWARDED') return REFERRAL_STATUS_LABELS.REWARDED;
  return action;
}
