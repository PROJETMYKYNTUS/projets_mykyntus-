/**
 * Valeurs ASCII pour X-Prime-Role : Kestrel / proxy refusent les en-têtes non-ASCII (HTTP 400).
 * Les paramètres de requête / corps peuvent garder les libellés accentués.
 */
const ROLE_TO_HEADER: Record<string, string> = {
  'Référent technique': 'ReferentTechnique',
  'Chef de projet': 'ChefDeProjet',
  Comptabilité: 'Comptabilite',
  Coach: 'Coach',
  RP: 'ChefDeProjet',
  Comptable: 'Comptabilite',
};

export function toPrimeRoleHeader(role: string): string {
  const trimmed = role.trim();
  return ROLE_TO_HEADER[trimmed] ?? trimmed;
}
