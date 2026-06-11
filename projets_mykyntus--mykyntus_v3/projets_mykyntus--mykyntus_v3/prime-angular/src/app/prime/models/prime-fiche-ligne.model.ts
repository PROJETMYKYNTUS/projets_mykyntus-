/** Ligne tableau PRIME — champs alignés UI / export JSON. */
export interface PrimeFicheLigneSaisie {
  repartitionRdv: string;
  resultatPrime: string;
  kpiPointMin: string;
  kpiPointMax: string;
  ponderationPrime: string;
  bonusAtteintPrime: string;
  montantPrime: string;
  resultatChallenge: string;
  kpiChallenge: string;
  ponderationChallenge: string;
  bonusAtteintChallenge: string;
  montantChallenge: string;
}

export type PrimeFichePrimeField = keyof Pick<
  PrimeFicheLigneSaisie,
  | 'resultatPrime'
  | 'kpiPointMin'
  | 'kpiPointMax'
  | 'ponderationPrime'
  | 'bonusAtteintPrime'
  | 'montantPrime'
>;

export type PrimeFicheChallengeField = keyof Pick<
  PrimeFicheLigneSaisie,
  'resultatChallenge' | 'kpiChallenge' | 'ponderationChallenge' | 'bonusAtteintChallenge' | 'montantChallenge'
>;

/** Champs numériques obligatoires pour validation saisie (répartition + chaque secteur). */
export const PRIME_FICHE_NUMERIC_FIELDS: (keyof PrimeFicheLigneSaisie)[] = [
  'repartitionRdv',
  'resultatPrime',
  'kpiPointMin',
  'kpiPointMax',
  'ponderationPrime',
  'bonusAtteintPrime',
  'montantPrime',
  'resultatChallenge',
  'kpiChallenge',
  'ponderationChallenge',
  'bonusAtteintChallenge',
  'montantChallenge',
];

/** Champs financiers strictement non négatifs dans les formulaires PRIME. */
export const PRIME_FICHE_NON_NEGATIVE_FIELDS = new Set<keyof PrimeFicheLigneSaisie>([
  'repartitionRdv',
  'resultatPrime',
  'kpiPointMin',
  'kpiPointMax',
  'ponderationPrime',
  'bonusAtteintPrime',
  'montantPrime',
  'resultatChallenge',
  'kpiChallenge',
  'ponderationChallenge',
  'bonusAtteintChallenge',
  'montantChallenge',
]);

/** Valeur vide autorisée en saisie ; sinon impose un nombre fini >= 0. */
export function isEmptyOrNonNegativeNumberString(raw: string): boolean {
  const t = raw.trim();
  if (t.length === 0) return true;
  const normalized = t.replace(',', '.');
  const n = Number(normalized);
  return Number.isFinite(n) && n >= 0;
}

/** Retourne vide pour une valeur négative/invalides, sinon garde la saisie utilisateur. */
export function sanitizeNonNegativeNumberInput(raw: string): string {
  const t = raw.trim();
  if (t.length === 0) return '';
  const n = Number(t.replace(',', '.'));
  if (!Number.isFinite(n) || n < 0) return '';
  return raw;
}

export function emptyPrimeFicheLigne(): PrimeFicheLigneSaisie {
  return {
    repartitionRdv: '',
    resultatPrime: '',
    kpiPointMin: '',
    kpiPointMax: '',
    ponderationPrime: '',
    bonusAtteintPrime: '',
    montantPrime: '',
    resultatChallenge: '',
    kpiChallenge: '',
    ponderationChallenge: '',
    bonusAtteintChallenge: '',
    montantChallenge: '',
  };
}

/** Valeurs Prime+Challenge pour un secteur (sans répartition, commune à la ligne). */
export type PrimeFicheSecteurPairValues = Omit<PrimeFicheLigneSaisie, 'repartitionRdv'>;

export function emptySecteurPairValues(): PrimeFicheSecteurPairValues {
  const { repartitionRdv: _, ...rest } = emptyPrimeFicheLigne();
  void _;
  return rest;
}
