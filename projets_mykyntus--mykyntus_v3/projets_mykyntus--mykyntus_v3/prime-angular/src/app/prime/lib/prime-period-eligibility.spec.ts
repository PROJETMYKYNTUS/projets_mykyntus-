import {
  clampToLatestClosedPeriod,
  closedMonthsForYear,
  defaultClosedPrimePeriod,
  isPrimePeriodClosed,
  isPrimePeriodLabelClosed,
  maxClosedPrimePeriodInputValue,
} from './prime-period-eligibility';

describe('prime-period-eligibility', () => {
  const june8_2026 = new Date(2026, 5, 8);

  it('considère le mois précédent comme dernier mois clos par défaut', () => {
    expect(defaultClosedPrimePeriod(june8_2026)).toEqual({ year: 2026, month: 5 });
  });

  it('refuse le mois en cours', () => {
    expect(isPrimePeriodClosed(2026, 6, june8_2026)).toBe(false);
  });

  it('accepte un mois terminé', () => {
    expect(isPrimePeriodClosed(2026, 5, june8_2026)).toBe(true);
    expect(isPrimePeriodLabelClosed('2026-05', june8_2026)).toBe(true);
  });

  it('limite les mois sélectionnables pour une année', () => {
    const months = closedMonthsForYear(2026, june8_2026).map((m) => m.value);
    expect(months).toEqual([1, 2, 3, 4, 5]);
    expect(months).not.toContain(6);
  });

  it('ramène une période non éligible au dernier mois clos', () => {
    expect(clampToLatestClosedPeriod(2026, 6, june8_2026)).toEqual({ year: 2026, month: 5 });
  });

  it('expose la valeur max pour input month', () => {
    expect(maxClosedPrimePeriodInputValue(june8_2026)).toBe('2026-05');
  });
});
