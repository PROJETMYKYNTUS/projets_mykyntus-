import type { Referral } from '../models/referral.model';

export const REFERRAL_MATCH_PRESELECT_THRESHOLD = 0.85;
export const REFERRAL_MATCH_ALERT_THRESHOLD = 0.7;
export const REFERRAL_MATCH_AMBIGUITY_GAP = 0.05;

export interface ReferralMatchCandidate {
  referral: Referral;
  score: number;
}

export interface ReferralMatchResult {
  best: ReferralMatchCandidate | null;
  alertMatches: ReferralMatchCandidate[];
  ambiguous: boolean;
  shouldPreselect: boolean;
}

export interface ReferralMatchInput {
  firstName: string;
  lastName: string;
  email?: string;
}

function normalize(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function levenshtein(a: string, b: string): number {
  if (a === b) return 0;
  if (!a.length) return b.length;
  if (!b.length) return a.length;
  const row = Array.from({ length: b.length + 1 }, (_, i) => i);
  for (let i = 1; i <= a.length; i++) {
    let prev = i;
    for (let j = 1; j <= b.length; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      const next = Math.min(row[j] + 1, prev + 1, row[j - 1] + cost);
      row[j - 1] = prev;
      prev = next;
    }
    row[b.length] = prev;
  }
  return row[b.length];
}

function scoreStrings(left: string, right: string): number {
  const a = normalize(left);
  const b = normalize(right);
  if (!a || !b) return 0;
  if (a === b) return 1;
  if (a.includes(b) || b.includes(a)) return 0.95;
  const maxLen = Math.max(a.length, b.length);
  const dist = levenshtein(a, b);
  return Math.max(0, 1 - dist / maxLen);
}

function candidateVariants(input: ReferralMatchInput): string[] {
  const first = input.firstName.trim();
  const last = input.lastName.trim();
  const variants = new Set<string>();
  if (first && last) {
    variants.add(`${last} ${first}`);
    variants.add(`${first} ${last}`);
  }
  if (last) variants.add(last);
  if (first) variants.add(first);
  return [...variants];
}

function referralNameVariants(referral: Referral): string[] {
  const name = referral.candidateName.trim();
  const tokens = name.split(/\s+/).filter(Boolean);
  const variants = new Set<string>([name]);
  if (tokens.length >= 2) {
    variants.add(`${tokens[0]} ${tokens.slice(1).join(' ')}`);
    variants.add(`${tokens.slice(1).join(' ')} ${tokens[0]}`);
  }
  return [...variants];
}

function scoreReferral(input: ReferralMatchInput, referral: Referral): number {
  const inputVariants = candidateVariants(input);
  const referralVariants = referralNameVariants(referral);
  let best = 0;
  for (const left of inputVariants) {
    for (const right of referralVariants) {
      best = Math.max(best, scoreStrings(left, right));
    }
  }
  const emailA = normalize(input.email ?? '');
  const emailB = normalize(referral.candidateEmail ?? '');
  if (emailA && emailB && emailA === emailB) {
    best = Math.min(1, best + 0.1);
  }
  return best;
}

export function matchReferralCandidates(
  input: ReferralMatchInput,
  referrals: Referral[],
): ReferralMatchResult {
  const scored = referrals
    .map((referral) => ({ referral, score: scoreReferral(input, referral) }))
    .filter((item) => item.score >= REFERRAL_MATCH_ALERT_THRESHOLD)
    .sort((a, b) => b.score - a.score);

  const best = scored[0] ?? null;
  const second = scored[1] ?? null;
  const ambiguous =
    best !== null &&
    second !== null &&
    best.score - second.score < REFERRAL_MATCH_AMBIGUITY_GAP;

  const shouldPreselect =
    best !== null &&
    !ambiguous &&
    best.score >= REFERRAL_MATCH_PRESELECT_THRESHOLD;

  return {
    best,
    alertMatches: scored,
    ambiguous,
    shouldPreselect,
  };
}

export function filterLinkableReferrals(referrals: Referral[], query: string): Referral[] {
  const q = normalize(query);
  if (!q) return referrals;
  return referrals.filter((r) => {
    const haystack = normalize(
      `${r.candidateName} ${r.candidateEmail} ${r.referrerName} ${r.position}`,
    );
    return haystack.includes(q);
  });
}
