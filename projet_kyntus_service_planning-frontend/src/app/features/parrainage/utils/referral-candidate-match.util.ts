import type { Referral } from '../models/referral.model';

/** Score à partir duquel on propose un dossier (alerte RH). */
export const REFERRAL_MATCH_ALERT_THRESHOLD = 0.7;
/** Score élevé — signal « très probable » (toujours confirmation RH, pas de liaison silencieuse). */
export const REFERRAL_MATCH_PRESELECT_THRESHOLD = 0.85;
export const REFERRAL_MATCH_AMBIGUITY_GAP = 0.05;
/** Seuil bas pour la recherche dossier (typos / noms proches). */
export const REFERRAL_SEARCH_THRESHOLD = 0.55;
/** Longueur mini d’un prénom / nom pour lancer le matching identité. */
export const REFERRAL_MATCH_MIN_NAME_LENGTH = 2;
/** Un token doit atteindre ce score pour contribuer au matching aligné. */
const TOKEN_PAIR_MIN = 0.65;

export interface ReferralMatchCandidate {
  referral: Referral;
  score: number;
}

export interface ReferralMatchResult {
  best: ReferralMatchCandidate | null;
  alertMatches: ReferralMatchCandidate[];
  ambiguous: boolean;
  /** True si un match unique est très proche — UI peut le mettre en avant, sans auto-lier. */
  shouldPreselect: boolean;
}

export interface ReferralMatchInput {
  firstName: string;
  lastName: string;
  email?: string;
  /** Emails additionnels (ex. personnel + interne) pour booster le score. */
  emails?: Array<string | null | undefined>;
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

/**
 * Similarité chaîne → [0,1].
 * Les préfixes / inclusions courts (ex. « ben » ⊂ « benjelloun ») ne donnent plus un faux 0.9.
 */
export function scoreStrings(left: string, right: string): number {
  const a = normalize(left);
  const b = normalize(right);
  if (!a || !b) return 0;
  if (a === b) return 1;

  const short = a.length <= b.length ? a : b;
  const long = a.length <= b.length ? b : a;
  const ratio = short.length / long.length;

  // Inclusion / préfixe seulement si la chaîne courte couvre une part significative.
  if (ratio >= 0.72) {
    if (long.includes(short)) return Math.min(1, 0.9 + 0.1 * ratio);
    if (long.startsWith(short) || short.startsWith(long)) return Math.min(1, 0.85 + 0.12 * ratio);
  } else if (ratio >= 0.5 && short.length >= 4 && long.includes(short)) {
    return 0.55 + 0.35 * ratio;
  }

  const maxLen = Math.max(a.length, b.length);
  const dist = levenshtein(a, b);
  return Math.max(0, 1 - dist / maxLen);
}

function fullNameVariants(firstName: string, lastName: string): string[] {
  const first = firstName.trim();
  const last = lastName.trim();
  if (!first || !last) return [];
  return [`${last} ${first}`, `${first} ${last}`];
}

/** Similarité sur le nom complet (prénom + nom) vs libellé candidat. */
function scoreFullName(input: ReferralMatchInput, candidateName: string): number {
  const variants = fullNameVariants(input.firstName, input.lastName);
  if (!variants.length) return 0;
  const candidate = candidateName.trim();
  let best = 0;
  for (const v of variants) {
    best = Math.max(best, scoreStrings(v, candidate));
  }
  return best;
}

/**
 * Aligne prénom et nom sur deux tokens distincts du dossier.
 * Les deux doivent être proches — un seul token fort ne suffit pas.
 */
function scoreAlignedTokens(input: ReferralMatchInput, candidateName: string): number {
  const first = normalize(input.firstName);
  const last = normalize(input.lastName);
  if (!first || !last) return 0;

  const tokens = normalize(candidateName).split(/\s+/).filter(Boolean);
  if (tokens.length === 0) return 0;

  let best = 0;
  for (let i = 0; i < tokens.length; i++) {
    for (let j = 0; j < tokens.length; j++) {
      if (tokens.length > 1 && i === j) continue;
      const lastScore = scoreStrings(last, tokens[i]);
      const firstScore = scoreStrings(first, tokens[j]);
      if (lastScore < TOKEN_PAIR_MIN || firstScore < TOKEN_PAIR_MIN) continue;
      best = Math.max(best, (lastScore + firstScore) / 2);
    }
  }
  return best;
}

function scoreReferral(input: ReferralMatchInput, referral: Referral): number {
  const first = input.firstName.trim();
  const last = input.lastName.trim();
  if (
    first.length < REFERRAL_MATCH_MIN_NAME_LENGTH ||
    last.length < REFERRAL_MATCH_MIN_NAME_LENGTH
  ) {
    return 0;
  }

  let best = Math.max(
    scoreFullName(input, referral.candidateName),
    scoreAlignedTokens(input, referral.candidateName),
  );

  const candidateEmail = normalize(referral.candidateEmail ?? '');
  const emails = [
    input.email,
    ...(input.emails ?? []),
  ]
    .map((e) => normalize(e ?? ''))
    .filter(Boolean);

  if (candidateEmail && emails.includes(candidateEmail)) {
    best = Math.min(1, Math.max(best, 0.8) + 0.15);
  }
  return best;
}

function scoreReferralAgainstQuery(query: string, referral: Referral): number {
  const q = normalize(query);
  if (!q) return 1;

  const name = referral.candidateName.trim();
  const nameNorm = normalize(name);
  let best = scoreStrings(q, name);

  // Requête multi-mots : comparer comme nom complet.
  const qTokens = q.split(/\s+/).filter(Boolean);
  if (qTokens.length >= 2) {
    best = Math.max(best, scoreStrings(q, name));
    best = Math.max(
      best,
      scoreAlignedTokens({ firstName: qTokens[0], lastName: qTokens.slice(1).join(' ') }, name),
      scoreAlignedTokens({ firstName: qTokens[qTokens.length - 1], lastName: qTokens.slice(0, -1).join(' ') }, name),
    );
  } else if (qTokens.length === 1 && qTokens[0].length >= 3) {
    // Un seul mot : match sur un token du nom (pas email/poste pour éviter le bruit trop large).
    for (const token of nameNorm.split(/\s+/).filter(Boolean)) {
      best = Math.max(best, scoreStrings(qTokens[0], token));
    }
    if (nameNorm.includes(qTokens[0]) && qTokens[0].length / Math.max(nameNorm.length, 1) >= 0.35) {
      best = Math.max(best, 0.8);
    }
  }

  // Email / parrain / poste en complément (seuil plus bas via includes strict).
  for (const field of [referral.candidateEmail, referral.referrerName, referral.position]) {
    const fieldNorm = normalize(field ?? '');
    if (!fieldNorm) continue;
    best = Math.max(best, scoreStrings(q, fieldNorm) * 0.95);
    if (qTokens.length === 1 && fieldNorm.includes(qTokens[0]) && qTokens[0].length >= 4) {
      best = Math.max(best, 0.75);
    }
  }

  return best;
}

/**
 * Si le RH colle « Nom Prénom » dans un seul champ, reconstitue first/last.
 * Convention dossier : souvent « Nom Prénom » (1er token = nom de famille).
 */
export function coerceIdentityInput(input: ReferralMatchInput): ReferralMatchInput {
  let first = (input.firstName ?? '').trim();
  let last = (input.lastName ?? '').trim();

  if (first.length < REFERRAL_MATCH_MIN_NAME_LENGTH && last.split(/\s+/).filter(Boolean).length >= 2) {
    const parts = last.split(/\s+/).filter(Boolean);
    last = parts[0];
    first = parts.slice(1).join(' ');
  } else if (last.length < REFERRAL_MATCH_MIN_NAME_LENGTH && first.split(/\s+/).filter(Boolean).length >= 2) {
    const parts = first.split(/\s+/).filter(Boolean);
    last = parts[0];
    first = parts.slice(1).join(' ');
  }

  return { ...input, firstName: first, lastName: last };
}

export function matchReferralCandidates(
  input: ReferralMatchInput,
  referrals: Referral[],
): ReferralMatchResult {
  const coerced = coerceIdentityInput(input);
  const first = coerced.firstName.trim();
  const last = coerced.lastName.trim();
  if (
    first.length < REFERRAL_MATCH_MIN_NAME_LENGTH ||
    last.length < REFERRAL_MATCH_MIN_NAME_LENGTH
  ) {
    return { best: null, alertMatches: [], ambiguous: false, shouldPreselect: false };
  }

  const scored = referrals
    .map((referral) => ({ referral, score: scoreReferral(coerced, referral) }))
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

/** Recherche floue triée — pour le champ « Rechercher un dossier ». */
export function rankReferralsByQuery(
  query: string,
  referrals: Referral[],
  threshold = REFERRAL_SEARCH_THRESHOLD,
): ReferralMatchCandidate[] {
  const q = normalize(query);
  if (!q) {
    return referrals.map((referral) => ({ referral, score: 1 }));
  }
  return referrals
    .map((referral) => ({ referral, score: scoreReferralAgainstQuery(query, referral) }))
    .filter((item) => item.score >= threshold)
    .sort((a, b) => b.score - a.score);
}

export function filterLinkableReferrals(referrals: Referral[], query: string): Referral[] {
  return rankReferralsByQuery(query, referrals).map((r) => r.referral);
}
