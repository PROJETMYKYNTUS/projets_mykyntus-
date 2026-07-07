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
  SUBMITTED: 'bg-amber-500/10 text-amber-500 border-amber-500/20',
  PROCESSED: 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20',
  IN_TRAINING: 'bg-orange-500/10 text-orange-400 border-orange-500/20',
  APPROVED: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  REJECTED: 'bg-red-500/10 text-red-500 border-red-500/20',
  REWARDED: 'bg-blue-500/10 text-blue-500 border-blue-500/20',
};

/** Styles for dense RH list/dashboard cards (slightly stronger contrast). */
export const REFERRAL_STATUS_STYLES_RH: Record<ReferralStatus, string> = {
  SUBMITTED: 'bg-blue-500/15 text-blue-300 border-blue-500/40',
  PROCESSED: 'bg-cyan-500/15 text-cyan-300 border-cyan-500/40',
  IN_TRAINING: 'bg-amber-500/15 text-amber-300 border-amber-500/40',
  APPROVED: 'bg-emerald-500/15 text-emerald-300 border-emerald-500/40',
  REJECTED: 'bg-red-500/15 text-red-300 border-red-500/40',
  REWARDED: 'bg-purple-500/15 text-purple-200 border-purple-500/40',
};

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
