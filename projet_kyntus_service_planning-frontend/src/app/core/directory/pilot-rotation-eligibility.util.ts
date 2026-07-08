import type { PilotRotationEligibilityDto } from './directory-employee-api.service';

export type PilotRotationGuardDecision =
  | { action: 'proceed' }
  | { action: 'block'; message: string }
  | { action: 'admin-override'; message: string };

export function pilotRotationBlockMessage(eligibility: PilotRotationEligibilityDto): string {
  const serviceLabel =
    eligibility.currentServiceName ?? eligibility.currentServiceId ?? 'service actuel';
  return (
    `L'employé doit rester au moins 6 mois sur « ${serviceLabel} » ` +
    `(${eligibility.daysRemaining} jour(s) restant(s)).`
  );
}

export function evaluatePilotRotationEligibility(
  eligibility: PilotRotationEligibilityDto,
  callerRole: string,
): PilotRotationGuardDecision {
  if (eligibility.eligible || eligibility.isSameService) {
    return { action: 'proceed' };
  }

  const message = pilotRotationBlockMessage(eligibility);
  if (callerRole === 'Admin') {
    return { action: 'admin-override', message };
  }

  return { action: 'block', message };
}
