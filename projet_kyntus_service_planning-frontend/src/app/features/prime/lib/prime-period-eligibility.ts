/** Période PRIME au format YYYY-MM (mois civil terminé uniquement). */

const MONTH_LABELS_FR = [
  'Janvier',
  'Février',
  'Mars',
  'Avril',
  'Mai',
  'Juin',
  'Juillet',
  'Août',
  'Septembre',
  'Octobre',
  'Novembre',
  'Décembre',
] as const;

/** Dernier mois entièrement terminé (ex. en juin → mai). */
export function defaultClosedPrimePeriod(now: Date = new Date()): { year: number; month: number } {
  const d = new Date(now);
  d.setDate(1);
  d.setMonth(d.getMonth() - 1);
  return { year: d.getFullYear(), month: d.getMonth() + 1 };
}

/** Vrai si le mois civil est terminé (on est au 1er du mois suivant ou après). */
export function isPrimePeriodClosed(year: number, month: number, now: Date = new Date()): boolean {
  if (!Number.isFinite(year) || month < 1 || month > 12) return false;
  const startOfNextMonth = new Date(year, month, 1);
  return now >= startOfNextMonth;
}

export function parsePrimePeriodLabel(period: string): { year: number; month: number } | null {
  const m = /^(\d{4})-(\d{2})$/.exec(period.trim());
  if (!m) return null;
  const year = Number(m[1]);
  const month = Number(m[2]);
  if (!Number.isFinite(year) || month < 1 || month > 12) return null;
  return { year, month };
}

export function isPrimePeriodLabelClosed(period: string, now: Date = new Date()): boolean {
  const p = parsePrimePeriodLabel(period);
  return p ? isPrimePeriodClosed(p.year, p.month, now) : false;
}

export function formatPrimePeriodLabel(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, '0')}`;
}

/** Libellé lisible pour l’UI (ex. « Juin 2025 (2025-06) »). */
export function formatPrimePeriodFriendly(year: number, month: number): string {
  if (month < 1 || month > 12) return formatPrimePeriodLabel(year, month);
  return `${MONTH_LABELS_FR[month - 1]!} ${year} (${formatPrimePeriodLabel(year, month)})`;
}

/** Valeur `max` pour `<input type="month">`. */
export function maxClosedPrimePeriodInputValue(now: Date = new Date()): string {
  const d = defaultClosedPrimePeriod(now);
  return formatPrimePeriodLabel(d.year, d.month);
}

export function clampToLatestClosedPeriod(
  year: number,
  month: number,
  now: Date = new Date(),
): { year: number; month: number } {
  if (isPrimePeriodClosed(year, month, now)) return { year, month };
  const latest = defaultClosedPrimePeriod(now);
  if (year > latest.year) return latest;
  if (year === latest.year && month > latest.month) return latest;
  const closedInYear = closedMonthsForYear(year, now);
  if (closedInYear.length === 0) return latest;
  const last = closedInYear[closedInYear.length - 1]!;
  return { year, month: last.value };
}

export function closedPrimePeriodYearChoices(now: Date = new Date(), spanYears = 4): number[] {
  const latest = defaultClosedPrimePeriod(now);
  const minYear = latest.year - spanYears + 1;
  const years: number[] = [];
  for (let y = latest.year; y >= minYear; y--) years.push(y);
  return years;
}

export function closedMonthsForYear(
  year: number,
  now: Date = new Date(),
): { value: number; label: string }[] {
  const out: { value: number; label: string }[] = [];
  for (let m = 1; m <= 12; m++) {
    if (isPrimePeriodClosed(year, m, now)) {
      out.push({ value: m, label: MONTH_LABELS_FR[m - 1]! });
    }
  }
  return out;
}

export function primePeriodClosedErrorMessage(period: string): string {
  return (
    `Les primes se calculent uniquement pour un mois déjà terminé. ` +
    `La période « ${period} » correspond au mois en cours ou à une période future.`
  );
}
