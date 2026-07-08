import { describe, expect, it } from 'vitest';
import { evaluatePilotRotationEligibility } from './pilot-rotation-eligibility.util';
import type { PilotRotationEligibilityDto } from './directory-employee-api.service';

const blocked: PilotRotationEligibilityDto = {
  eligible: false,
  isSameService: false,
  currentServiceId: 'svc-a',
  currentServiceName: 'Service Alpha',
  currentSince: '2025-01-01',
  eligibleAt: '2025-07-01',
  daysRemaining: 42,
};

describe('evaluatePilotRotationEligibility', () => {
  it('allows rotation when eligible', () => {
    const result = evaluatePilotRotationEligibility(
      { ...blocked, eligible: true, daysRemaining: 0 },
      'RH',
    );
    expect(result.action).toBe('proceed');
  });

  it('allows assignment on the same service', () => {
    const result = evaluatePilotRotationEligibility(
      { ...blocked, eligible: false, isSameService: true },
      'RH',
    );
    expect(result.action).toBe('proceed');
  });

  it('blocks RH before 6 months', () => {
    const result = evaluatePilotRotationEligibility(blocked, 'RH');
    expect(result.action).toBe('block');
    if (result.action === 'block') {
      expect(result.message).toContain('Service Alpha');
      expect(result.message).toContain('42');
    }
  });

  it('requires admin override for Admin role', () => {
    const result = evaluatePilotRotationEligibility(blocked, 'Admin');
    expect(result.action).toBe('admin-override');
  });
});
