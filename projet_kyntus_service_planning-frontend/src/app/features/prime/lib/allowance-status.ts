import type { Role } from '../models';

export const ALLOWANCE_STATUSES = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  ManagerApproved: 'ManagerApproved',
  RhApproved: 'RhApproved',
  ComptaApproved: 'ComptaApproved',
  Paid: 'Paid',
  Rejected: 'Rejected',
} as const;

export type AllowanceStatus = (typeof ALLOWANCE_STATUSES)[keyof typeof ALLOWANCE_STATUSES];

export type AllowanceStatusViewer = 'manager' | 'stakeholder';

const STATUS_META: Record<string, { label: string; managerLabel?: string; badgeClass: string }> = {
  Draft: { label: 'Brouillon', badgeClass: 'bg-slate-600/40 text-slate-300' },
  Submitted: { label: 'Soumis', badgeClass: 'bg-amber-500/20 text-amber-300' },
  ManagerApproved: {
    label: 'En attente validation RH',
    managerLabel: 'Soumis au RH',
    badgeClass: 'bg-blue-500/20 text-blue-300',
  },
  RhApproved: { label: 'Validé RH', badgeClass: 'bg-indigo-500/20 text-indigo-300' },
  ComptaApproved: { label: 'Validé compta', badgeClass: 'bg-violet-500/20 text-violet-300' },
  Paid: { label: 'Payé', badgeClass: 'bg-emerald-500/20 text-emerald-300' },
  Rejected: { label: 'Rejeté', badgeClass: 'bg-rose-500/20 text-rose-300' },
};

export function allowanceStatusLabel(status: string, viewer: AllowanceStatusViewer = 'stakeholder'): string {
  const meta = STATUS_META[status];
  if (!meta) return status;
  if (viewer === 'manager' && meta.managerLabel) return meta.managerLabel;
  return meta.label;
}

export function allowanceStatusBadgeClass(status: string): string {
  return STATUS_META[status]?.badgeClass ?? 'bg-slate-600/40 text-slate-300';
}

export function allowanceSourceLabel(source: string): string {
  const s = source.trim();
  if (s === 'Auto') return 'Automatique';
  if (s === 'Manual') return 'Manuelle';
  return s || '—';
}

export function inboxStepLabel(role: Role | string): string {
  const r = String(role).trim();
  if (r === 'RH') return 'Validation RH — étape 1';
  if (r === 'Comptabilité' || r === 'Comptable') return 'Validation comptabilité — étape 2';
  return 'File de validation — Primes Support';
}

const ROLE_EXPECTED_STATUS: Record<string, string> = {
  RH: ALLOWANCE_STATUSES.ManagerApproved,
  Comptabilité: ALLOWANCE_STATUSES.RhApproved,
  Comptable: ALLOWANCE_STATUSES.RhApproved,
};

export function canValidateAtStep(role: Role | string, status: string): boolean {
  const expected = ROLE_EXPECTED_STATUS[String(role).trim()];
  return !!expected && status === expected;
}

export function currentAllowancePeriod(): string {
  return new Date().toISOString().slice(0, 7);
}

export function countByStatus(rows: { status: string }[]): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const r of rows) {
    counts[r.status] = (counts[r.status] ?? 0) + 1;
  }
  return counts;
}

export function isPendingRhValidation(status: string): boolean {
  return status === ALLOWANCE_STATUSES.ManagerApproved;
}

export function isPendingForManager(status: string): boolean {
  return !['Paid', 'Rejected'].includes(status);
}

export function validateAllowanceAmount(
  amount: number,
  type: { minAmount?: number; maxAmount?: number; requiresJustification?: boolean },
  reason: string,
): string | null {
  if (type.minAmount != null && amount < type.minAmount) {
    return `Montant inférieur au minimum (${type.minAmount}).`;
  }
  if (type.maxAmount != null && amount > type.maxAmount) {
    return `Montant supérieur au maximum (${type.maxAmount}).`;
  }
  if (type.requiresJustification && !reason.trim()) {
    return 'Motif obligatoire pour ce type de prime.';
  }
  return null;
}
