/** Comparaison de noms organisationnels (insensible à la casse, trim). */
export function orgNamesEqual(a: string | null | undefined, b: string | null | undefined): boolean {
  const x = (a ?? '').trim();
  const y = (b ?? '').trim();
  if (!x || !y) return false;
  return x.localeCompare(y, 'fr', { sensitivity: 'accent' }) === 0;
}

export const ORG_DUPLICATE_POLE_MSG = 'Un pôle porte déjà ce nom.';
export const ORG_DUPLICATE_CELLULE_MSG = 'Une cellule porte déjà ce nom pour ce pôle.';
export const ORG_DUPLICATE_SERVICE_MSG = 'Un service porte déjà ce nom pour cette cellule.';
