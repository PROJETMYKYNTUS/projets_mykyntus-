import {
  isEmptyOrNonNegativeNumberString,
  PRIME_FICHE_OPTIONAL_NUMERIC_FIELDS,
  type PrimeFicheLigneSaisie,
  type PrimeFicheSecteurPairValues,
} from '../models/prime-fiche-ligne.model';
import { SECTOR_PAIR_NUMERIC_KEYS } from '../models/prime-fiche-template.schema';

/** Validation numérique d'un champ secteur (vide autorisé pour les champs optionnels Excel). */
export function passesPrimeFicheNumericFieldValidation(
  field: keyof PrimeFicheLigneSaisie,
  value: string,
): boolean {
  if (PRIME_FICHE_OPTIONAL_NUMERIC_FIELDS.has(field)) {
    return isEmptyOrNonNegativeNumberString(value);
  }
  return value.trim() !== '' && isEmptyOrNonNegativeNumberString(value);
}

export function numericFieldValidationMessage(field: string, sectorIndex?: number): string {
  const optional = PRIME_FICHE_OPTIONAL_NUMERIC_FIELDS.has(field as keyof PrimeFicheLigneSaisie);
  if (sectorIndex != null) {
    return optional
      ? `Secteur ${sectorIndex + 1} : le champ « ${field} » doit être numérique avec une valeur >= 0.`
      : `Secteur ${sectorIndex + 1} : le champ « ${field} » est obligatoire et doit être numérique avec une valeur >= 0.`;
  }
  return optional
    ? `Le champ "${field}" doit être numérique avec une valeur >= 0.`
    : `Le champ "${field}" est obligatoire et doit être numérique avec une valeur >= 0.`;
}

/** Valide les champs Prime+Challenge d'un secteur. Retourne le message d'erreur ou null. */
export function validateSecteurCoreFields(
  core: PrimeFicheSecteurPairValues,
  sectorIndex: number,
): string | null {
  for (const f of SECTOR_PAIR_NUMERIC_KEYS) {
    if (!passesPrimeFicheNumericFieldValidation(f, core[f])) {
      return numericFieldValidationMessage(String(f), sectorIndex);
    }
  }
  return null;
}
