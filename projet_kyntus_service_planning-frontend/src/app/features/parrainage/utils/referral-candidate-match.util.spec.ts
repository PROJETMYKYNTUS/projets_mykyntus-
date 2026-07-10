import { describe, expect, it } from 'vitest';
import type { Referral } from '../models/referral.model';
import {
  REFERRAL_MATCH_PRESELECT_THRESHOLD,
  matchReferralCandidates,
} from './referral-candidate-match.util';

function referral(partial: Partial<Referral> & Pick<Referral, 'id' | 'candidateName'>): Referral {
  return {
    id: partial.id,
    referrerId: partial.referrerId ?? 'r1',
    referrerName: partial.referrerName ?? 'Parrain',
    projectId: 'p1',
    projectName: 'Projet',
    teamId: 't1',
    candidateName: partial.candidateName,
    candidateEmail: partial.candidateEmail ?? 'c@test.com',
    candidatePhone: '+33',
    position: partial.position ?? 'Dev',
    positionMode: 'CUSTOM',
    status: partial.status ?? 'SUBMITTED',
    paymentStatus: 'NOT_ELIGIBLE',
    createdAt: new Date(),
  };
}

describe('matchReferralCandidates', () => {
  it('preselects exact name match', () => {
    const referrals = [referral({ id: 'ref-1', candidateName: 'Dupont Jean', candidateEmail: 'jean@test.com' })];
    const result = matchReferralCandidates(
      { firstName: 'Jean', lastName: 'Dupont', email: 'jean@test.com' },
      referrals,
    );
    expect(result.shouldPreselect).toBe(true);
    expect(result.best?.referral.id).toBe('ref-1');
    expect(result.best?.score).toBeGreaterThanOrEqual(REFERRAL_MATCH_PRESELECT_THRESHOLD);
  });

  it('flags ambiguity when two scores are close', () => {
    // Même identité sous deux ordres (Nom Prénom / Prénom Nom) → scores égaux → ambigu.
    const referrals = [
      referral({ id: 'ref-a', candidateName: 'Martin Paul', candidateEmail: 'a@test.com' }),
      referral({ id: 'ref-b', candidateName: 'Paul Martin', candidateEmail: 'b@test.com' }),
    ];
    const result = matchReferralCandidates({ firstName: 'Paul', lastName: 'Martin' }, referrals);
    expect(result.ambiguous).toBe(true);
    expect(result.shouldPreselect).toBe(false);
    expect(result.alertMatches.length).toBeGreaterThanOrEqual(2);
  });
});
