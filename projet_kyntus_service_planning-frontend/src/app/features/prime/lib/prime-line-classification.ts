/** Libellés d’indicateurs / lignes de synthèse (Somme…, Total…) à exclure des KPI métier. */
export function isSummaryLikeIndicatorLabel(label: string): boolean {
  const t = (label ?? '').trim();
  return /^somme\b/i.test(t) || /^total\b/i.test(t);
}
