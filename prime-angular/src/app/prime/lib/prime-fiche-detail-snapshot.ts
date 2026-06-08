import type { MergedEmployeeFichePreviewResult, MergedFicheTotals } from './prime-employee-fiche-merged-preview';

export const FICHE_DETAIL_SNAPSHOT_VERSION = 1;

export interface FicheDetailSnapshotV1 {
  version: number;
  previewSheetName?: string | null;
  templateVersionRef?: string | null;
  rows: string[][];
  errors: string[];
  computedAt?: string | null;
  primeAmount?: number | null;
  challengeAmount?: number | null;
  totalAmount?: number | null;
}

export interface FicheDetailSnapshotResponseDto {
  ficheId: string;
  version: number;
  previewSheetName?: string | null;
  templateVersionRef?: string | null;
  rows: string[][];
  errors: string[];
  primeAmount?: number | null;
  challengeAmount?: number | null;
  totalAmount?: number | null;
  frozenAt?: string | null;
  updatedAt: string;
}

export function buildTemplateVersionRef(templateId: string, formatVersion: number): string {
  return `${templateId.trim()}:v${formatVersion}`;
}

export function parseDetailSnapshotV1(json: string | null | undefined): FicheDetailSnapshotV1 | null {
  const t = (json ?? '').trim();
  if (!t) return null;
  try {
    const raw = JSON.parse(t) as FicheDetailSnapshotV1;
    if (!raw || raw.version !== FICHE_DETAIL_SNAPSHOT_VERSION || !Array.isArray(raw.rows) || raw.rows.length === 0) {
      return null;
    }
    return raw;
  } catch {
    return null;
  }
}

function totalsFromSnapshot(
  snap: FicheDetailSnapshotV1 | FicheDetailSnapshotResponseDto,
): MergedFicheTotals | null {
  const prime = snap.primeAmount;
  const challenge = snap.challengeAmount;
  const total = snap.totalAmount;
  if (
    typeof prime === 'number' &&
    Number.isFinite(prime) &&
    typeof challenge === 'number' &&
    Number.isFinite(challenge) &&
    typeof total === 'number' &&
    Number.isFinite(total)
  ) {
    return { primeAmount: prime, challengeAmount: challenge, totalAmount: total };
  }
  return null;
}

/** Reconstruit un aperçu fusionné depuis le snapshot DB (sans recalcul HyperFormula). */
export function mergedPreviewFromStoredSnapshot(
  snap: FicheDetailSnapshotV1 | FicheDetailSnapshotResponseDto,
): MergedEmployeeFichePreviewResult {
  const rows = snap.rows ?? [];
  if (!rows.length) {
    return {
      rows: [],
      errors: ['Snapshot détaillé vide.'],
      missingSnapshot: false,
      missingGridPositions: false,
      previewSheetName: null,
      effectiveSchema: null,
      parsedCell: null,
      totals: null,
    };
  }
  return {
    rows,
    errors: snap.errors ?? [],
    missingSnapshot: false,
    missingGridPositions: false,
    previewSheetName: (snap.previewSheetName ?? '').trim() || null,
    effectiveSchema: null,
    parsedCell: null,
    totals: totalsFromSnapshot(snap),
    fromStoredSnapshot: true,
  };
}

export function buildDetailSnapshotPayload(params: {
  previewSheetName: string | null;
  templateVersionRef: string;
  rows: string[][];
  errors: string[];
  totals: MergedFicheTotals | null;
}): FicheDetailSnapshotV1 {
  return {
    version: FICHE_DETAIL_SNAPSHOT_VERSION,
    previewSheetName: params.previewSheetName,
    templateVersionRef: params.templateVersionRef,
    rows: params.rows,
    errors: params.errors,
    computedAt: new Date().toISOString(),
    primeAmount: params.totals?.primeAmount ?? null,
    challengeAmount: params.totals?.challengeAmount ?? null,
    totalAmount: params.totals?.totalAmount ?? null,
  };
}
