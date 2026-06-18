/** Colonnes sans titre ou placeholders Excel — à ignorer sans bloquer l'import. */
export function isIgnorableHeader(header: string | null | undefined): boolean {
  if (header == null) return true;
  const trimmed = header.trim();
  if (!trimmed) return true;

  const normalized = trimmed
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '')
    .replace(/\*/g, '');

  if (!normalized) return true;
  if (/^(colonne|column|field|champ)\d+$/.test(normalized)) return true;
  if (normalized === 'unnamed' || normalized === 'sansnom') return true;

  return false;
}

export function headerDisplayLabel(header: string | null | undefined, columnIndex: number): string {
  if (isIgnorableHeader(header)) {
    return `Colonne ${columnIndex + 1} (sans titre — ignorée)`;
  }
  return header!.trim();
}

export function suggestedConfidenceForColumn(
  columnIndex: number,
  suggestedMappings: { columnIndex: number; confidence: string }[],
): string {
  return suggestedMappings.find((s) => s.columnIndex === columnIndex)?.confidence ?? 'low';
}
