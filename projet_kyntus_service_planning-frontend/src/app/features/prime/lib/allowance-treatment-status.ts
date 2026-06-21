import type { AllowanceTreatmentStatus } from '../services/allowance-api.service';

const LABELS: Record<AllowanceTreatmentStatus, string> = {
  NotStarted: 'À traiter',
  HasDrafts: 'Brouillons',
  Submitted: 'Soumis',
  Validated: 'Validé',
  Rejected: 'Rejeté',
  NoBonus: 'Pas de prime',
};

const BADGE_CLASS: Record<AllowanceTreatmentStatus, string> = {
  NotStarted: 'allowance-badge--pending',
  HasDrafts: 'allowance-badge--draft',
  Submitted: 'allowance-badge--submitted',
  Validated: 'allowance-badge--validated',
  Rejected: 'allowance-badge--rejected',
  NoBonus: 'allowance-badge--none',
};

export function allowanceTreatmentLabel(status: AllowanceTreatmentStatus | string): string {
  return LABELS[status as AllowanceTreatmentStatus] ?? status;
}

export function allowanceTreatmentBadgeClass(status: AllowanceTreatmentStatus | string): string {
  return BADGE_CLASS[status as AllowanceTreatmentStatus] ?? 'allowance-badge--pending';
}

export function sortMembersByPriority<T extends { treatmentStatus: AllowanceTreatmentStatus; lastName: string; firstName: string }>(
  members: T[],
): T[] {
  const order: Record<AllowanceTreatmentStatus, number> = {
    NotStarted: 0,
    HasDrafts: 1,
    Submitted: 2,
    Rejected: 3,
    NoBonus: 4,
    Validated: 5,
  };
  return [...members].sort((a, b) => {
    const ao = order[a.treatmentStatus] ?? 99;
    const bo = order[b.treatmentStatus] ?? 99;
    if (ao !== bo) return ao - bo;
    return `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`, 'fr');
  });
}
