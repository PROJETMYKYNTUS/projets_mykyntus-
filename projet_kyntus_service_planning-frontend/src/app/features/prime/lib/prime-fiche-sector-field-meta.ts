import type { PrimeFicheSecteurPairValues } from '../models/prime-fiche-ligne.model';
import type { PrimeFicheTemplateSecteurSlice } from '../models/prime-fiche-template.schema';

export const PRIME_FIELD_LABELS: { key: keyof PrimeFicheSecteurPairValues; label: string }[] = [
  { key: 'resultatPrime', label: 'Résultat' },
  { key: 'kpiPointMin', label: 'KPI Point MIN' },
  { key: 'kpiPointMax', label: 'KPI Point MAX' },
  { key: 'ponderationPrime', label: 'Pondération' },
  { key: 'bonusAtteintPrime', label: 'Bonus Atteint (%)' },
  { key: 'montantPrime', label: 'Montant' },
];

export const CHALLENGE_FIELD_LABELS: { key: keyof PrimeFicheSecteurPairValues; label: string }[] = [
  { key: 'resultatChallenge', label: 'Résultat' },
  { key: 'kpiChallenge', label: 'KPI Challenge' },
  { key: 'ponderationChallenge', label: 'Pondération' },
  { key: 'bonusAtteintChallenge', label: 'Bonus Atteint (%)' },
  { key: 'montantChallenge', label: 'Montant' },
];

/** Classes Tailwind partagées saisie grille (Prime / Challenge / KPI). */
export const PRIME_INPUT_FIELD_CLASS =
  'w-full px-3 py-2 border border-default rounded-lg focus:ring-2 focus:ring-blue-500/50 focus:border-blue-500 bg-input text-primary placeholder:text-muted';

export const PRIME_KPI_GRID_CLASS =
  'grid min-w-0 grid-cols-1 gap-x-3 gap-y-3 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4';

/** Titre ligne 1 Excel au-dessus des colonnes KPI additionnelles. */
export function customBandHeading(s: PrimeFicheTemplateSecteurSlice): string {
  const t = (s.customKpis ?? []).map((k) => k.bandTitle).find((x) => (x ?? '').trim().length > 0);
  return (t ?? '').trim() || 'Secteur additionnel';
}
