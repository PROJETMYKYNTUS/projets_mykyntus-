import type { PrimeFicheTemplateLine } from '../models/prime-fiche-template.schema';

export type NavBlock =
  | { kind: 'heading'; id: string; title: string }
  | { kind: 'single'; id: string; key: string; label: string }
  | { kind: 'group'; id: string; title: string; items: { key: string; shortLabel: string }[] };

function uniqueNavParts(parts: readonly string[]): string[] {
  const out: string[] = [];
  for (const raw of parts) {
    const t = (raw ?? '').trim();
    if (!t) continue;
    if (out.some((x) => x.toLowerCase() === t.toLowerCase())) continue;
    out.push(t);
  }
  return out;
}

function lineNavLabel(l: PrimeFicheTemplateLine): string {
  return uniqueNavParts([l.indicator, l.bareme, l.groupe]).join(' — ') || l.stableId;
}

function shortVariant(l: PrimeFicheTemplateLine): string {
  const ind = (l.indicator ?? '').trim();
  let bareme = (l.bareme ?? '').trim();
  let groupe = (l.groupe ?? '').trim();
  if (bareme && ind && bareme.toLowerCase() === ind.toLowerCase()) bareme = '';
  if (groupe && ind && groupe.toLowerCase() === ind.toLowerCase()) groupe = '';
  if (groupe && bareme && groupe.toLowerCase() === bareme.toLowerCase()) groupe = '';
  const parts = uniqueNavParts([bareme, groupe]);
  return parts.join(' · ') || ind || l.stableId;
}

/** Navigation latérale pour les lignes d’un même contrat (schéma template v1). */
export function buildNavBlocksForContract(lines: PrimeFicheTemplateLine[], contract: string): NavBlock[] {
  const subset = lines.filter((l) => l.contract === contract);
  const blocks: NavBlock[] = [];
  if (!subset.length) return blocks;

  blocks.push({ kind: 'heading', id: `h-${contract}`, title: contract });

  let i = 0;
  while (i < subset.length) {
    const first = subset[i];
    const group: PrimeFicheTemplateLine[] = [first];
    let j = i + 1;
    while (j < subset.length && subset[j].indicator === first.indicator) {
      group.push(subset[j]);
      j++;
    }
    if (group.length > 1) {
      blocks.push({
        kind: 'group',
        id: `grp-${first.stableId}`,
        title: first.indicator,
        items: group.map((g) => ({ key: g.stableId, shortLabel: shortVariant(g) })),
      });
    } else {
      blocks.push({
        kind: 'single',
        id: first.stableId,
        key: first.stableId,
        label: lineNavLabel(first),
      });
    }
    i = j;
  }
  return blocks;
}

export function firstStableIdForContract(lines: PrimeFicheTemplateLine[], contract: string): string | null {
  const ln = lines.find((l) => l.contract === contract);
  return ln?.stableId ?? null;
}

export function allStableIdsForContract(lines: PrimeFicheTemplateLine[], contract: string): string[] {
  return lines.filter((l) => l.contract === contract).map((l) => l.stableId);
}
