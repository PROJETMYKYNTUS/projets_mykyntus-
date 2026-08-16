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

/** Lundi ISO de la semaine contenant `date`. */
export function getIsoWeekMonday(date: Date = new Date()): Date {
  const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const day = d.getDay(); // 0=dim … 6=sam
  const diff = day === 0 ? -6 : 1 - day;
  d.setDate(d.getDate() + diff);
  return d;
}

/** Code API ISO : 2026-W34 */
export function toIsoWeekCode(date: Date = new Date()): string {
  const monday = getIsoWeekMonday(date);
  const thursday = new Date(monday);
  thursday.setDate(monday.getDate() + 3);
  const isoYear = thursday.getFullYear();
  const jan4 = new Date(isoYear, 0, 4);
  const jan4Monday = getIsoWeekMonday(jan4);
  const week =
    Math.round((monday.getTime() - jan4Monday.getTime()) / (7 * 86400000)) + 1;
  return `${isoYear}-W${String(week).padStart(2, '0')}`;
}

export interface WeekSelectOption {
  value: string; // API week code 2026-Wxx
  label: string; // 2026-Sxx · jj/mm
}

/** Liste déroulante de semaines (passées + actuelles + prochaines). */
export function buildWeekSelectOptions(
  pastWeeks = 16,
  futureWeeks = 4,
): WeekSelectOption[] {
  const monday = getIsoWeekMonday();
  const options: WeekSelectOption[] = [];
  for (let i = -pastWeeks; i <= futureWeeks; i++) {
    const d = new Date(monday);
    d.setDate(monday.getDate() + i * 7);
    const code = toIsoWeekCode(d);
    const end = new Date(d);
    end.setDate(d.getDate() + 6);
    const dd = (n: number) => String(n).padStart(2, '0');
    options.push({
      value: code,
      label: `${formatWeekLabel(code)} · ${dd(d.getDate())}/${dd(d.getMonth() + 1)} → ${dd(end.getDate())}/${dd(end.getMonth() + 1)}`,
    });
  }
  return options.reverse();
}

export type RequestFilterPeriod =
  | 'thisMonth'
  | 'lastMonth'
  | 'last3Months'
  | 'thisYear'
  | 'all';

export const REQUEST_PERIOD_OPTIONS: { value: RequestFilterPeriod; label: string }[] = [
  { value: 'thisMonth', label: 'Ce mois' },
  { value: 'lastMonth', label: 'Mois dernier' },
  { value: 'last3Months', label: '3 derniers mois' },
  { value: 'thisYear', label: 'Cette année' },
  { value: 'all', label: 'Tout' },
];

