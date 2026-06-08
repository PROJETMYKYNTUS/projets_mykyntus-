import type { PrimeFicheGridImportDiagnostics } from '../models/prime-fiche-template.schema';

/** Résumé court pour l’UI (import / template manager). */
export function summarizeGridDiagnostics(diag: PrimeFicheGridImportDiagnostics): {
  hasBlockingErrors: boolean;
  summaryWarnings: string[];
  detailWarnings: string[];
  detailErrors: string[];
} {
  const detailErrors = diag.errors;
  const detailWarnings = diag.warnings;
  const summaryWarnings: string[] = [];
  const grouped = detailWarnings.filter((w) => w.startsWith('▸ '));
  const other = detailWarnings.filter((w) => !w.startsWith('▸ '));

  if (grouped.length) {
    summaryWarnings.push(...grouped);
  }
  if (other.length <= 4) {
    summaryWarnings.push(...other);
  } else {
    summaryWarnings.push(...other.slice(0, 3));
    summaryWarnings.push(`… ${other.length - 3} autre(s) avertissement(s) — voir le détail.`);
  }

  return {
    hasBlockingErrors: detailErrors.length > 0,
    summaryWarnings,
    detailWarnings,
    detailErrors,
  };
}
