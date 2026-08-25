import { newClientKey, partSortOrder } from '../../../core/lib/formation-parts.util';
import type {
  ReplaceCatalogStructureRequest,
  ReplaceCatalogStructureResponse,
  StructureLessonRequest,
  StructureModuleRequest,
  StructureResourceRequest,
} from '../../../core/models/formation-training.models';
import type { DraftModule } from './formation-catalog-draft.types';

export function catalogResourceTypeNum(type: string): number {
  if (type === 'Video') return 1;
  if (type === 'Link') return 2;
  if (type === 'Text') return 3;
  if (type === 'Image') return 4;
  return 0;
}

export function countDraftLessons(modules: DraftModule[]): number {
  return modules.reduce((n, m) => n + m.lessons.length, 0);
}

export function buildCatalogStructureRequest(modules: DraftModule[]): ReplaceCatalogStructureRequest {
  const mods: StructureModuleRequest[] = modules.map((m, mi) => {
    const lessons: StructureLessonRequest[] = m.lessons.map((l, li) => {
      const resources: StructureResourceRequest[] = [];
      l.parts.forEach((part, pi) => {
        const title = part.title.trim() || `Partie ${pi + 1}`;
        if (part.title.trim() || part.textContent.trim()) {
          resources.push({
            clientKey: part.textResourceId ? `txt_${part.textResourceId}` : newClientKey('txt'),
            id: part.textResourceId || null,
            type: catalogResourceTypeNum('Text'),
            title,
            textContent: part.textContent || null,
            sortOrder: partSortOrder(pi, 'Text'),
          });
        }
        for (const f of part.existingFiles) {
          resources.push({
            clientKey: `ex_${f.id}`,
            id: f.id,
            type: catalogResourceTypeNum(f.type),
            title: f.title || title,
            url: f.url,
            sortOrder: partSortOrder(pi, f.type),
          });
        }
        if (part.videoUrl.trim() && !part.videoFile) {
          resources.push({
            clientKey: part.existingVideoId ? `vid_${part.existingVideoId}` : newClientKey('vidurl'),
            id: part.existingVideoId || null,
            type: catalogResourceTypeNum('Video'),
            title: `${title} — Vidéo`,
            url: part.videoUrl.trim(),
            sortOrder: partSortOrder(pi, 'Video'),
          });
        }
        if (part.linkUrl.trim()) {
          resources.push({
            clientKey: part.existingLinkId ? `lnk_${part.existingLinkId}` : newClientKey('lnk'),
            id: part.existingLinkId || null,
            type: catalogResourceTypeNum('Link'),
            title: `${title} — Lien`,
            url: part.linkUrl.trim(),
            sortOrder: partSortOrder(pi, 'Link'),
          });
        }
      });
      return {
        clientKey: l.clientKey,
        id: l.id || null,
        title: l.title.trim() || 'Nouvelle leçon',
        description: l.description ?? '',
        sortOrder: li,
        isRequired: l.isRequired,
        resources,
      };
    });
    return {
      clientKey: m.clientKey,
      id: m.id || null,
      title: m.title.trim() || 'Nouveau module',
      description: m.description ?? '',
      sortOrder: mi,
      lessons,
    };
  });
  return { modules: mods };
}

export async function uploadCatalogPendingFiles(
  modules: DraftModule[],
  structure: ReplaceCatalogStructureResponse,
  upload: (
    lessonId: string,
    file: File,
    title: string,
    type: string,
    sortOrder: number,
  ) => Promise<unknown>,
): Promise<void> {
  const lessonIdByKey = new Map<string, string>();
  for (const mod of structure.modules) {
    for (const les of mod.lessons) {
      lessonIdByKey.set(les.clientKey, les.id);
    }
  }

  for (const m of modules) {
    for (const l of m.lessons) {
      const lessonId = lessonIdByKey.get(l.clientKey);
      if (!lessonId) continue;
      for (let pi = 0; pi < l.parts.length; pi++) {
        const part = l.parts[pi];
        const title = part.title.trim() || `Partie ${pi + 1}`;
        if (part.pdfFile) {
          await upload(lessonId, part.pdfFile, `${title} — PDF`, 'Pdf', partSortOrder(pi, 'Pdf'));
        }
        if (part.videoFile) {
          await upload(lessonId, part.videoFile, `${title} — Vidéo`, 'Video', partSortOrder(pi, 'Video'));
        }
        if (part.imageFile) {
          await upload(lessonId, part.imageFile, `${title} — Image`, 'Image', partSortOrder(pi, 'Image'));
        }
      }
    }
  }
}
