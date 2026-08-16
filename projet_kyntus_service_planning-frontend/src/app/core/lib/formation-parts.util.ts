import type { TrainingResourceDto } from '../models/formation-training.models';

/** Une « partie » = texte + pièces jointes regroupées (sortOrder = index*100 + rang). */
export interface ResourcePartGroup {
  /** Index de partie (floor(sortOrder/100) ou 0). */
  partIndex: number;
  /** Titre affiché (préfixe commun ou titre de la ressource texte). */
  title: string;
  /** Ressources de la partie, triées. */
  resources: TrainingResourceDto[];
  text?: TrainingResourceDto;
  pdf?: TrainingResourceDto;
  video?: TrainingResourceDto;
  image?: TrainingResourceDto;
  link?: TrainingResourceDto;
}

const PART_SEP = ' — ';

function typeKey(type: string | number): string {
  const t = String(type);
  if (t === 'Video' || t === '1') return 'Video';
  if (t === 'Link' || t === '2') return 'Link';
  if (t === 'Text' || t === '3') return 'Text';
  if (t === 'Image' || t === '4') return 'Image';
  return 'Pdf';
}

function titlePrefix(title: string): string {
  const i = title.indexOf(PART_SEP);
  return i > 0 ? title.slice(0, i).trim() : title.trim();
}

function partRank(type: string | number): number {
  switch (typeKey(type)) {
    case 'Text':
      return 0;
    case 'Pdf':
      return 1;
    case 'Video':
      return 2;
    case 'Image':
      return 3;
    case 'Link':
      return 4;
    default:
      return 5;
  }
}

/** sortOrder = partIndex * 100 + rang (texte=0, pdf=1, vidéo=2, image=3, lien=4). */
export function partSortOrder(partIndex: number, type: string | number): number {
  return partIndex * 100 + partRank(type);
}

/**
 * Regroupe les ressources d'une leçon en parties.
 * Priorité : floor(sortOrder/100) ; repli sur le préfixe de titre avant « — ».
 */
export function groupResourcesByPart(resources: TrainingResourceDto[] | null | undefined): ResourcePartGroup[] {
  const list = [...(resources ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
  if (!list.length) return [];

  const usesPartEncoding = list.some((r) => r.sortOrder >= 100 || (r.sortOrder % 100) <= 4 && list.length > 1);

  const buckets = new Map<string, TrainingResourceDto[]>();

  for (const r of list) {
    let key: string;
    if (usesPartEncoding || list.every((x) => x.sortOrder < 1000)) {
      const idx = Math.floor(r.sortOrder / 100);
      // Heuristique : si tous les sortOrder sont 0..N sans gaps de 100, grouper par préfixe de titre.
      const dense = list.every((x) => x.sortOrder < 100) && list.length > 1;
      if (dense) {
        key = `t:${titlePrefix(r.title) || r.id}`;
      } else {
        key = `i:${idx}`;
      }
    } else {
      key = `t:${titlePrefix(r.title) || r.id}`;
    }
    const arr = buckets.get(key) ?? [];
    arr.push(r);
    buckets.set(key, arr);
  }

  const groups: ResourcePartGroup[] = [];
  let fallbackIndex = 0;
  for (const [, items] of buckets) {
    const sorted = items.sort((a, b) => a.sortOrder - b.sortOrder);
    const text = sorted.find((r) => typeKey(r.type) === 'Text');
    const title =
      text?.title?.trim() ||
      titlePrefix(sorted[0]?.title ?? '') ||
      `Partie ${fallbackIndex + 1}`;
    const partIndex = Math.floor(sorted[0].sortOrder / 100);
    const group: ResourcePartGroup = {
      partIndex: Number.isFinite(partIndex) ? partIndex : fallbackIndex,
      title,
      resources: sorted,
      text,
      pdf: sorted.find((r) => typeKey(r.type) === 'Pdf'),
      video: sorted.find((r) => typeKey(r.type) === 'Video'),
      image: sorted.find((r) => typeKey(r.type) === 'Image'),
      link: sorted.find((r) => typeKey(r.type) === 'Link'),
    };
    groups.push(group);
    fallbackIndex++;
  }

  return groups.sort((a, b) => a.partIndex - b.partIndex || a.title.localeCompare(b.title));
}

export function newClientKey(prefix = 'k'): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}_${Date.now().toString(36)}`;
}
