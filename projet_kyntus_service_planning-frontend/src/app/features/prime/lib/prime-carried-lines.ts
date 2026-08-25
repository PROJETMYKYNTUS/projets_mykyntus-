import type { PrimeFicheLigneDynamic } from '../models/prime-fiche-template.schema';

const MEASURE_SUFFIXES = [
  'resultatPrime',
  'resultatChallenge',
  'bonusAtteintPrime',
  'bonusAtteintChallenge',
  'montantPrime',
  'montantChallenge',
] as const;

function isMeasureFlatKey(key: string): boolean {
  if (key.includes('_custom_')) return true;
  return MEASURE_SUFFIXES.some((s) => key.endsWith(`_${s}`));
}

function flatLineHasMeasures(flat: Record<string, unknown>): boolean {
  return Object.entries(flat).some(([k, v]) => {
    if (!isMeasureFlatKey(k)) return false;
    if (v === null || v === undefined) return false;
    if (typeof v === 'string') return v.trim() !== '';
    if (typeof v === 'number') return Number.isFinite(v);
    return true;
  });
}

export function isCarriedUnconfirmed(row: PrimeFicheLigneDynamic): boolean {
  if (!row.carriedFrom?.trim()) return false;
  return row.carriedConfirmed !== true;
}

export function countUnconfirmedCarriedLines(
  rows: Readonly<Record<string, PrimeFicheLigneDynamic>>,
): number {
  return Object.values(rows).filter((r) => isCarriedUnconfirmed(r) && lineHasMeasures(r)).length;
}

function lineHasMeasures(row: PrimeFicheLigneDynamic): boolean {
  if (!row.secteurValues?.length) return false;
  for (const sv of row.secteurValues) {
    for (const key of MEASURE_SUFFIXES) {
      const v = sv.core[key];
      if (v != null && String(v).trim() !== '') return true;
    }
    for (const v of Object.values(sv.custom)) {
      if (v != null && String(v).trim() !== '') return true;
    }
  }
  return false;
}

export function confirmAllCarriedLines(
  rows: Record<string, PrimeFicheLigneDynamic>,
): Record<string, PrimeFicheLigneDynamic> {
  const next: Record<string, PrimeFicheLigneDynamic> = {};
  for (const [id, row] of Object.entries(rows)) {
    next[id] = row.carriedFrom?.trim() ? { ...row, carriedConfirmed: true } : row;
  }
  return next;
}

export function markLineConfirmedOnMeasureEdit(row: PrimeFicheLigneDynamic): PrimeFicheLigneDynamic {
  if (!row.carriedFrom?.trim() || row.carriedConfirmed === true) return row;
  return { ...row, carriedConfirmed: true };
}

/** Compte les lignes reconduites non confirmées dans un payload aplati (partie commune). */
export function countUnconfirmedCarriedLinesFromPayload(
  payload: Record<string, unknown> | null | undefined,
): number {
  const lignes = payload?.['lignes'];
  if (!lignes || typeof lignes !== 'object' || Array.isArray(lignes)) return 0;
  let count = 0;
  for (const line of Object.values(lignes as Record<string, unknown>)) {
    if (!line || typeof line !== 'object' || Array.isArray(line)) continue;
    const lo = line as Record<string, unknown>;
    const cf = lo['carriedFrom'];
    if (cf == null || String(cf).trim() === '') continue;
    const cc = lo['carriedConfirmed'];
    const confirmed = cc === true || cc === 'true';
    if (!confirmed && flatLineHasMeasures(lo)) count++;
  }
  return count;
}
