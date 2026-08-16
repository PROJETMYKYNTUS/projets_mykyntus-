import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ChevronDown,
  ChevronRight,
  FileText,
  Film,
  Image as ImageIcon,
  Link2,
  Plus,
  Trash2,
  ArrowUp,
  ArrowDown,
  X,
} from 'lucide';
import type {
  TrainingLessonDto,
  TrainingModuleDto,
  TrainingResourceDto,
} from '../../../core/models/formation-training.models';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { FormationRichTextEditorComponent } from './formation-rich-text-editor.component';
import { FormationResourceViewerComponent } from './formation-resource-viewer.component';
import { BodyPortalDirective } from '../../../shared/directives/body-portal.directive';
import { groupResourcesByPart, partSortOrder, newClientKey } from '../../../core/lib/formation-parts.util';
import {
  type DraftLesson,
  type DraftModule,
  type DraftPart,
  emptyDraftLesson,
  emptyDraftModule,
  emptyDraftPart,
} from './formation-catalog-draft.types';

function typeKey(type: string | number): string {
  const t = String(type);
  if (t === 'Video' || t === '1') return 'Video';
  if (t === 'Link' || t === '2') return 'Link';
  if (t === 'Text' || t === '3') return 'Text';
  if (t === 'Image' || t === '4') return 'Image';
  return 'Pdf';
}

/** Convertit l'arbre API → brouillon local (parties regroupées). */
export function modulesToDraft(modules: TrainingModuleDto[] | null | undefined): DraftModule[] {
  const sorted = [...(modules ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
  if (!sorted.length) return [];
  return sorted.map((m) => ({
    clientKey: newClientKey('mod'),
    id: m.id,
    title: m.title,
    description: m.description ?? '',
    lessons: [...(m.lessons ?? [])]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((l) => lessonToDraft(l)),
  }));
}

function lessonToDraft(l: TrainingLessonDto): DraftLesson {
  const groups = groupResourcesByPart(l.resources);
  const parts: DraftPart[] =
    groups.length > 0
      ? groups.map((g) => {
          const existingFiles = sortedNonText(g.resources).filter((r) => {
            const k = typeKey(r.type);
            return (k === 'Pdf' || k === 'Video' || k === 'Image') && !!(r.downloadPath || r.fileName);
          }).map((r) => ({
            id: r.id,
            type: typeKey(r.type) as DraftPart['existingFiles'][0]['type'],
            title: r.title,
            fileName: r.fileName,
            downloadPath: r.downloadPath,
            url: r.url,
          }));

          const videoUrlRes = g.resources.find(
            (r) => typeKey(r.type) === 'Video' && r.url && !r.downloadPath && !r.fileName,
          );
          const linkRes = g.resources.find((r) => typeKey(r.type) === 'Link');

          return {
            clientKey: newClientKey('part'),
            textResourceId: g.text?.id ?? null,
            title: g.title,
            textContent: g.text?.textContent ?? '',
            existingFiles,
            pdfFile: null,
            videoFile: null,
            imageFile: null,
            videoUrl: videoUrlRes?.url ?? '',
            linkUrl: linkRes?.url ?? '',
            existingVideoId: videoUrlRes?.id ?? null,
            existingLinkId: linkRes?.id ?? null,
          } satisfies DraftPart;
        })
      : [emptyDraftPart()];

  return {
    clientKey: newClientKey('les'),
    id: l.id,
    title: l.title,
    description: l.description ?? '',
    isRequired: l.isRequired,
    parts,
  };
}

function sortedNonText(resources: TrainingResourceDto[]): TrainingResourceDto[] {
  return resources.filter((r) => typeKey(r.type) !== 'Text');
}

@Component({
  selector: 'app-formation-catalog-outline',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LucideIconComponent,
    FormationRichTextEditorComponent,
    FormationResourceViewerComponent,
    BodyPortalDirective,
  ],
  templateUrl: './formation-catalog-outline.component.html',
  styleUrls: ['./formation-catalog-outline.component.css'],
})
export class FormationCatalogOutlineComponent implements OnChanges {
  readonly icons = {
    plus: Plus,
    trash: Trash2,
    up: ArrowUp,
    down: ArrowDown,
    chevron: ChevronRight,
    chevronDown: ChevronDown,
    pdf: FileText,
    video: Film,
    link: Link2,
    text: FileText,
    image: ImageIcon,
    close: X,
  };

  /** Brouillon local (two-way). */
  @Input() model: DraftModule[] = [];
  @Output() readonly modelChange = new EventEmitter<DraftModule[]>();

  /** Hydrate depuis l'API uniquement au chargement initial. */
  @Input() seedModules: TrainingModuleDto[] | null = null;

  readonly openModules = signal<Set<string>>(new Set());
  readonly openLessons = signal<Set<string>>(new Set());

  editingModuleKey: string | null = null;
  editingLessonKey: string | null = null;
  editTitle = '';

  panelOpen = false;
  panelTab: 'edit' | 'preview' = 'edit';
  panelLessonKey: string | null = null;
  panelPartKey: string | null = null;
  contentBlocks: DraftPart[] = [emptyDraftPart()];

  private seeded = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seedModules'] && this.seedModules && !this.seeded && !this.model.length) {
      const draft = modulesToDraft(this.seedModules);
      this.seeded = true;
      this.emit(draft);
      this.expandAll(draft);
    }
    if (changes['model'] && this.model?.length) {
      this.expandAll(this.model);
    }
  }

  private expandAll(draft: DraftModule[]): void {
    const openM = new Set(this.openModules());
    const openL = new Set(this.openLessons());
    for (const m of draft) {
      openM.add(m.clientKey);
      for (const l of m.lessons) openL.add(l.clientKey);
    }
    this.openModules.set(openM);
    this.openLessons.set(openL);
  }

  private emit(next: DraftModule[]): void {
    this.modelChange.emit(next);
  }

  private patch(mutator: (draft: DraftModule[]) => void): void {
    const next = structuredClone(this.model);
    mutator(next);
    this.emit(next);
  }

  isModuleOpen(key: string): boolean {
    return this.openModules().has(key);
  }

  isLessonOpen(key: string): boolean {
    return this.openLessons().has(key);
  }

  toggleModule(key: string): void {
    const next = new Set(this.openModules());
    if (next.has(key)) next.delete(key);
    else next.add(key);
    this.openModules.set(next);
  }

  toggleLesson(key: string): void {
    const next = new Set(this.openLessons());
    if (next.has(key)) next.delete(key);
    else next.add(key);
    this.openLessons.set(next);
  }

  addModule(): void {
    const mod = emptyDraftModule();
    const next = [...this.model, mod];
    const open = new Set(this.openModules());
    open.add(mod.clientKey);
    this.openModules.set(open);
    this.emit(next);
    this.startEditModule(mod);
  }

  startEditModule(m: DraftModule): void {
    this.editingModuleKey = m.clientKey;
    this.editingLessonKey = null;
    this.editTitle = m.title;
  }

  commitEditModule(m: DraftModule): void {
    if (this.editingModuleKey !== m.clientKey) return;
    const title = this.editTitle.trim();
    this.editingModuleKey = null;
    if (!title || title === m.title) return;
    this.patch((draft) => {
      const found = draft.find((x) => x.clientKey === m.clientKey);
      if (found) found.title = title;
    });
  }

  deleteModule(m: DraftModule): void {
    this.emit(this.model.filter((x) => x.clientKey !== m.clientKey));
  }

  moveModule(m: DraftModule, dir: -1 | 1): void {
    const i = this.model.findIndex((x) => x.clientKey === m.clientKey);
    const j = i + dir;
    if (i < 0 || j < 0 || j >= this.model.length) return;
    const next = [...this.model];
    [next[i], next[j]] = [next[j], next[i]];
    this.emit(next);
  }

  addLesson(m: DraftModule): void {
    const lesson = emptyDraftLesson();
    this.patch((draft) => {
      const found = draft.find((x) => x.clientKey === m.clientKey);
      if (found) found.lessons.push(lesson);
    });
    const open = new Set(this.openLessons());
    open.add(lesson.clientKey);
    this.openLessons.set(open);
    this.startEditLesson(lesson);
  }

  startEditLesson(l: DraftLesson): void {
    this.editingLessonKey = l.clientKey;
    this.editingModuleKey = null;
    this.editTitle = l.title;
  }

  commitEditLesson(l: DraftLesson): void {
    if (this.editingLessonKey !== l.clientKey) return;
    const title = this.editTitle.trim();
    this.editingLessonKey = null;
    if (!title || title === l.title) return;
    this.patch((draft) => {
      for (const mod of draft) {
        const found = mod.lessons.find((x) => x.clientKey === l.clientKey);
        if (found) {
          found.title = title;
          return;
        }
      }
    });
  }

  toggleRequired(l: DraftLesson): void {
    this.patch((draft) => {
      for (const mod of draft) {
        const found = mod.lessons.find((x) => x.clientKey === l.clientKey);
        if (found) {
          found.isRequired = !found.isRequired;
          return;
        }
      }
    });
  }

  deleteLesson(m: DraftModule, l: DraftLesson): void {
    this.patch((draft) => {
      const found = draft.find((x) => x.clientKey === m.clientKey);
      if (found) found.lessons = found.lessons.filter((x) => x.clientKey !== l.clientKey);
    });
  }

  moveLesson(m: DraftModule, l: DraftLesson, dir: -1 | 1): void {
    this.patch((draft) => {
      const found = draft.find((x) => x.clientKey === m.clientKey);
      if (!found) return;
      const i = found.lessons.findIndex((x) => x.clientKey === l.clientKey);
      const j = i + dir;
      if (i < 0 || j < 0 || j >= found.lessons.length) return;
      [found.lessons[i], found.lessons[j]] = [found.lessons[j], found.lessons[i]];
    });
  }

  openAddResource(l: DraftLesson): void {
    this.panelLessonKey = l.clientKey;
    this.panelPartKey = null;
    this.contentBlocks = [emptyDraftPart()];
    this.panelTab = 'edit';
    this.panelOpen = true;
  }

  openEditPart(l: DraftLesson, part: DraftPart): void {
    this.panelLessonKey = l.clientKey;
    this.panelPartKey = part.clientKey;
    this.contentBlocks = [structuredClone(part)];
    this.panelTab = 'edit';
    this.panelOpen = true;
  }

  closePanel(): void {
    this.panelOpen = false;
    this.panelLessonKey = null;
    this.panelPartKey = null;
    this.contentBlocks = [emptyDraftPart()];
    this.panelTab = 'edit';
  }

  addContentBlock(): void {
    this.contentBlocks = [...this.contentBlocks, emptyDraftPart()];
  }

  removeContentBlock(index: number): void {
    if (this.contentBlocks.length <= 1) {
      this.contentBlocks = [emptyDraftPart()];
      return;
    }
    this.contentBlocks = this.contentBlocks.filter((_, i) => i !== index);
  }

  onBlockFile(block: DraftPart, kind: 'pdf' | 'video' | 'image', ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (kind === 'pdf') block.pdfFile = file;
    if (kind === 'video') block.videoFile = file;
    if (kind === 'image') block.imageFile = file;
    if (file && !block.title.trim()) {
      block.title = file.name.replace(/\.[^.]+$/, '');
    }
  }

  removeExistingFile(block: DraftPart, fileId: string): void {
    block.existingFiles = block.existingFiles.filter((f) => f.id !== fileId);
  }

  previewBlocks(): { title: string; text: string; stubs: TrainingResourceDto[] }[] {
    return this.contentBlocks.map((b, bi) => {
      const stubs: TrainingResourceDto[] = [];
      if (b.textContent?.trim() || b.title.trim()) {
        stubs.push({
          id: `prev-text-${bi}`,
          lessonId: '',
          type: 'Text',
          title: b.title || `Partie ${bi + 1}`,
          textContent: b.textContent || null,
          sortOrder: 0,
        });
      }
      for (const f of b.existingFiles) {
        stubs.push({
          id: f.id,
          lessonId: '',
          type: f.type,
          title: f.title,
          url: f.url,
          fileName: f.fileName,
          downloadPath: f.downloadPath,
          sortOrder: partRank(f.type),
        });
      }
      if (b.pdfFile) {
        stubs.push({
          id: `prev-pdf-${bi}`,
          lessonId: '',
          type: 'Pdf',
          title: b.pdfFile.name,
          url: URL.createObjectURL(b.pdfFile),
          sortOrder: 1,
        });
      }
      if (b.videoFile || b.videoUrl.trim()) {
        stubs.push({
          id: `prev-vid-${bi}`,
          lessonId: '',
          type: 'Video',
          title: b.videoFile?.name || 'Vidéo',
          url: b.videoFile ? URL.createObjectURL(b.videoFile) : b.videoUrl.trim(),
          sortOrder: 2,
        });
      }
      if (b.imageFile) {
        stubs.push({
          id: `prev-img-${bi}`,
          lessonId: '',
          type: 'Image',
          title: b.imageFile.name,
          url: URL.createObjectURL(b.imageFile),
          sortOrder: 3,
        });
      }
      if (b.linkUrl.trim()) {
        stubs.push({
          id: `prev-link-${bi}`,
          lessonId: '',
          type: 'Link',
          title: 'Lien',
          url: b.linkUrl.trim(),
          sortOrder: 4,
        });
      }
      return { title: b.title || `Partie ${bi + 1}`, text: b.textContent, stubs };
    });
  }

  savePanel(): void {
    if (!this.panelLessonKey) return;
    const usable = this.contentBlocks.filter(
      (b) =>
        b.title.trim() ||
        b.textContent.trim() ||
        b.pdfFile ||
        b.videoFile ||
        b.imageFile ||
        b.videoUrl.trim() ||
        b.linkUrl.trim() ||
        b.existingFiles.length,
    );
    if (!usable.length) return;

    this.patch((draft) => {
      for (const mod of draft) {
        const lesson = mod.lessons.find((x) => x.clientKey === this.panelLessonKey);
        if (!lesson) continue;

        if (this.panelPartKey) {
          const idx = lesson.parts.findIndex((p) => p.clientKey === this.panelPartKey);
          if (idx >= 0) lesson.parts[idx] = { ...usable[0], clientKey: this.panelPartKey };
        } else {
          for (const b of usable) {
            lesson.parts.push({ ...b, clientKey: b.clientKey || newClientKey('part') });
          }
        }
        return;
      }
    });
    this.closePanel();
  }

  deletePart(l: DraftLesson, part: DraftPart): void {
    this.patch((draft) => {
      for (const mod of draft) {
        const lesson = mod.lessons.find((x) => x.clientKey === l.clientKey);
        if (!lesson) continue;
        lesson.parts = lesson.parts.filter((p) => p.clientKey !== part.clientKey);
        if (!lesson.parts.length) lesson.parts = [emptyDraftPart()];
        return;
      }
    });
  }

  partAttachmentLabels(part: DraftPart): string[] {
    const labels: string[] = [];
    if (part.textContent?.trim()) labels.push('Texte');
    for (const f of part.existingFiles) labels.push(typeKey(f.type));
    if (part.pdfFile) labels.push('PDF');
    if (part.videoFile || part.videoUrl.trim()) labels.push('Vidéo');
    if (part.imageFile) labels.push('Image');
    if (part.linkUrl.trim()) labels.push('Lien');
    return labels;
  }
}

function partRank(type: string | number): number {
  return partSortOrder(0, type) % 100;
}
