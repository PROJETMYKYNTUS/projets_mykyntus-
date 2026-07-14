import { describe, expect, it } from 'vitest';
import type { Referral } from '../models/referral.model';
import {
  REFERRAL_MATCH_ALERT_THRESHOLD,
  REFERRAL_MATCH_PRESELECT_THRESHOLD,
  filterLinkableReferrals,
  matchReferralCandidates,
  rankReferralsByQuery,
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
  it('flags a strong unique match without requiring silent auto-link in UI', () => {
    const referrals = [referral({ id: 'ref-1', candidateName: 'Dupont Jean', candidateEmail: 'jean@test.com' })];
    const result = matchReferralCandidates(
      { firstName: 'Jean', lastName: 'Dupont', email: 'jean@test.com' },
      referrals,
    );
    expect(result.best?.referral.id).toBe('ref-1');
    expect(result.best?.score).toBeGreaterThanOrEqual(REFERRAL_MATCH_PRESELECT_THRESHOLD);
    expect(result.shouldPreselect).toBe(true);
    expect(result.alertMatches.length).toBe(1);
  });

  it('alerts on similar names above alert threshold', () => {
    const referrals = [referral({ id: 'ref-1', candidateName: 'Benali Karim' })];
    const result = matchReferralCandidates({ firstName: 'Karim', lastName: 'Benaly' }, referrals);
    expect(result.alertMatches.length).toBeGreaterThanOrEqual(1);
    expect(result.best?.score).toBeGreaterThanOrEqual(REFERRAL_MATCH_ALERT_THRESHOLD);
  });

  it('flags ambiguity when two scores are close', () => {
    const referrals = [
      referral({ id: 'ref-a', candidateName: 'Martin Paul', candidateEmail: 'a@test.com' }),
      referral({ id: 'ref-b', candidateName: 'Paul Martin', candidateEmail: 'b@test.com' }),
    ];
    const result = matchReferralCandidates({ firstName: 'Paul', lastName: 'Martin' }, referrals);
    expect(result.ambiguous).toBe(true);
    expect(result.shouldPreselect).toBe(false);
    expect(result.alertMatches.length).toBeGreaterThanOrEqual(2);
  });

  it('does not match on short partial tokens alone (fatima + ben)', () => {
    const referrals = [
      referral({ id: 'ref-1', candidateName: 'Fatima Zahra Bennis' }),
      referral({ id: 'ref-2', candidateName: 'Khadija Benjelloun' }),
      referral({ id: 'ref-3', candidateName: 'Nadia Benchrif' }),
    ];
    const result = matchReferralCandidates({ firstName: 'ben', lastName: 'fatima' }, referrals);
    expect(result.alertMatches.map((m) => m.referral.id)).not.toContain('ref-2');
    expect(result.alertMatches.map((m) => m.referral.id)).not.toContain('ref-3');
    expect(result.alertMatches.length).toBe(0);
  });

  it('ranks the closest full name when typing a near-complete identity', () => {
    const referrals = [
      referral({ id: 'ref-1', candidateName: 'Fatima Zahra Bennis' }),
      referral({ id: 'ref-2', candidateName: 'Khadija Benjelloun' }),
      referral({ id: 'ref-3', candidateName: 'Nadia Benchrif' }),
    ];
    const result = matchReferralCandidates(
      { firstName: 'Fatima Zahra', lastName: 'Bennis' },
      referrals,
    );
    expect(result.best?.referral.id).toBe('ref-1');
    expect(result.alertMatches.map((m) => m.referral.id)).not.toContain('ref-2');
  });

  it('requires both first and last name before matching', () => {
    const referrals = [referral({ id: 'ref-1', candidateName: 'Dupont Jean' })];
    expect(matchReferralCandidates({ firstName: 'Jean', lastName: '' }, referrals).alertMatches).toEqual([]);
    expect(matchReferralCandidates({ firstName: '', lastName: 'Dupont' }, referrals).alertMatches).toEqual([]);
  });
});

describe('rankReferralsByQuery / filterLinkableReferrals', () => {
  const referrals = [
    referral({ id: 'ref-1', candidateName: 'Dupont Jean', candidateEmail: 'jean.dupont@mail.com' }),
    referral({ id: 'ref-2', candidateName: 'Martin Paul', referrerName: 'Yasmine El Idrissi' }),
    referral({ id: 'ref-3', candidateName: 'Benali Sara', position: 'Agent chat' }),
  ];

  it('returns fuzzy matches for typos in candidate name', () => {
    const ranked = rankReferralsByQuery('Dupond', referrals);
    expect(ranked.map((r) => r.referral.id)).toContain('ref-1');
  });

  it('matches first/last name order and partial tokens', () => {
    const byFirst = filterLinkableReferrals(referrals, 'Jean');
    expect(byFirst.map((r) => r.id)).toContain('ref-1');
    const byReversed = filterLinkableReferrals(referrals, 'Jean Dupont');
    expect(byReversed[0]?.id).toBe('ref-1');
  });

  it('matches referrer or position fields', () => {
    expect(filterLinkableReferrals(referrals, 'Yasmine').map((r) => r.id)).toContain('ref-2');
    expect(filterLinkableReferrals(referrals, 'chat').map((r) => r.id)).toContain('ref-3');
  });

  it('returns all referrals when query is empty', () => {
    expect(filterLinkableReferrals(referrals, '').length).toBe(3);
  });
});
