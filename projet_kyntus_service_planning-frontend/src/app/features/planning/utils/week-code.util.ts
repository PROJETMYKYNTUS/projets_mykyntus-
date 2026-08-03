/** Affichage FR : 2026-W34 → 2026-S34 (API/DB restent en W). */
export function formatWeekLabel(weekCode: string | null | undefined): string {
  if (!weekCode) return '';
  return String(weekCode).replace(/-W/gi, '-S');
}

/** Saisie UI → code API : 2026-S34 → 2026-W34. */
export function toApiWeekCode(weekCode: string | null | undefined): string {
  if (!weekCode) return '';
  return String(weekCode).trim().replace(/-S/gi, '-W');
}
