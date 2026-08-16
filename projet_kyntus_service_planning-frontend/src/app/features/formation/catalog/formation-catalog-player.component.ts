import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ArrowLeft, CheckCircle2, ChevronLeft, ChevronRight, Circle, FileText, Film, Image as ImageIcon, Link2 } from 'lucide';
import { resolveCurrentUserGuid } from '../../../core/lib/user-guid.util';
import {
  isImageResource,
  isPdfResource,
  isTextResource,
  isVideoResource,
} from '../../../core/lib/formation-learning-html.util';
import { groupResourcesByPart, type ResourcePartGroup } from '../../../core/lib/formation-parts.util';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type { CatalogPlayerDto, TrainingLessonDto, TrainingResourceDto } from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { FormationResourceViewerComponent } from './formation-resource-viewer.component';

@Component({
  selector: 'app-formation-catalog-player',
  standalone: true,
  imports: [CommonModule, RouterLink, KyntusPageHeaderComponent, LucideIconComponent, FormationResourceViewerComponent],
  templateUrl: './formation-catalog-player.component.html',
  styleUrls: ['./formation-catalog-player.component.css'],
})
export class FormationCatalogPlayerComponent implements OnInit {
  readonly icons = {
    back: ArrowLeft,
    done: CheckCircle2,
    pending: Circle,
    prev: ChevronLeft,
    next: ChevronRight,
    pdf: FileText,
    video: Film,
    image: ImageIcon,
    link: Link2,
    text: FileText,
  };
  private readonly api = inject(FormationTrainingService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly player = signal<CatalogPlayerDto | null>(null);
  readonly activeLesson = signal<TrainingLessonDto | null>(null);
  readonly activePartIndex = signal(0);

  sessionId = '';
  catalogItemId = '';
  userId = '';

  get isSelfService(): boolean {
    return !!this.catalogItemId && !this.sessionId;
  }

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    this.catalogItemId = this.route.snapshot.paramMap.get('catalogItemId') ?? '';
    this.userId = resolveCurrentUserGuid();
    if (!this.sessionId && !this.catalogItemId) {
      void this.router.navigate(['/mes-formations']);
      return;
    }
    void this.reload();
  }

  async reload(preferLessonId?: string | null): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const p = this.isSelfService
        ? await this.api.getCatalogPlayerByCatalog(this.catalogItemId)
        : await this.api.getCatalogPlayer(this.sessionId, this.userId || undefined);
      this.player.set(p);
      const allLessons = p.modules.flatMap((m) => m.lessons);
      const keepId = preferLessonId ?? this.activeLesson()?.id ?? null;
      const preferred = (keepId ? allLessons.find((l) => l.id === keepId) : null) ?? null;
      const firstIncomplete = allLessons.find((l) => !l.isCompleted) ?? allLessons[0] ?? null;
      this.selectLesson(preferred ?? firstIncomplete);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement impossible');
    } finally {
      this.loading.set(false);
    }
  }

  allLessons(): TrainingLessonDto[] {
    return this.player()?.modules.flatMap((m) => m.lessons) ?? [];
  }

  selectLesson(lesson: TrainingLessonDto | null): void {
    this.activeLesson.set(lesson);
    this.activePartIndex.set(0);
  }

  partsFor(lesson: TrainingLessonDto | null): ResourcePartGroup[] {
    return groupResourcesByPart(lesson?.resources);
  }

  activeParts(): ResourcePartGroup[] {
    return this.partsFor(this.activeLesson());
  }

  selectPart(index: number): void {
    const parts = this.activeParts();
    if (index < 0 || index >= parts.length) return;
    this.activePartIndex.set(index);
  }

  lessonState(lesson: TrainingLessonDto): 'done' | 'current' | 'todo' {
    if (lesson.isCompleted) return 'done';
    if (this.activeLesson()?.id === lesson.id) return 'current';
    return 'todo';
  }

  currentLessonIndex(): number {
    const lesson = this.activeLesson();
    if (!lesson) return -1;
    return this.allLessons().findIndex((l) => l.id === lesson.id);
  }

  canGoPrev(): boolean {
    if (this.activePartIndex() > 0) return true;
    return this.currentLessonIndex() > 0;
  }

  canGoNext(): boolean {
    const parts = this.activeParts();
    if (this.activePartIndex() < parts.length - 1) return true;
    const i = this.currentLessonIndex();
    return i >= 0 && i < this.allLessons().length - 1;
  }

  /** Navigue d’abord entre les parties, puis entre les leçons. */
  goPrev(): void {
    if (this.activePartIndex() > 0) {
      this.selectPart(this.activePartIndex() - 1);
      return;
    }
    const i = this.currentLessonIndex();
    if (i <= 0) return;
    const prev = this.allLessons()[i - 1];
    this.selectLesson(prev);
    const parts = this.partsFor(prev);
    this.activePartIndex.set(Math.max(0, parts.length - 1));
  }

  goNext(): void {
    const parts = this.activeParts();
    if (this.activePartIndex() < parts.length - 1) {
      this.selectPart(this.activePartIndex() + 1);
      return;
    }
    const lessons = this.allLessons();
    const i = this.currentLessonIndex();
    if (i < 0 || i >= lessons.length - 1) return;
    this.selectLesson(lessons[i + 1]);
  }

  resourceIcon(r: TrainingResourceDto) {
    if (isVideoResource(r)) return this.icons.video;
    if (isImageResource(r)) return this.icons.image;
    if (isTextResource(r)) return this.icons.text;
    if (isPdfResource(r)) return this.icons.pdf;
    return this.icons.link;
  }

  hasAssociatedQuiz(): boolean {
    const p = this.player();
    if (!p) return false;
    return !!this.sessionId || !!p.defaultQuizTemplateId;
  }

  showQuizButton(): boolean {
    const p = this.player();
    return !!p?.canTakeQuiz && this.hasAssociatedQuiz();
  }

  quizRouterLink(): string[] | null {
    const p = this.player();
    if (!p?.canTakeQuiz) return null;
    if (this.sessionId) return ['/mes-formations', this.sessionId, 'quiz'];
    if (this.catalogItemId && p.defaultQuizTemplateId) {
      return ['/mes-formations', 'contenu', this.catalogItemId, 'quiz'];
    }
    return null;
  }

  onQuizCtaClick(): void {
    const link = this.quizRouterLink();
    if (link) {
      void this.router.navigate(link);
      return;
    }
    this.error.set('Aucun quiz disponible pour le moment.');
  }

  async completeLesson(): Promise<void> {
    const lesson = this.activeLesson();
    if (!lesson) {
      this.error.set('Aucune leçon sélectionnée.');
      return;
    }
    if (lesson.isCompleted) {
      this.message.set('Cette leçon est déjà marquée comme vue.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      const parts = this.activeParts();
      const rawId =
        parts[this.activePartIndex()]?.resources?.[0]?.id ?? lesson.resources?.[0]?.id ?? null;
      const lastResourceId =
        rawId && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(rawId)
          ? rawId
          : null;
      const body = {
        employeeId: this.userId || undefined,
        lastResourceId,
      };
      const updated = this.isSelfService
        ? await this.api.completeLessonByCatalog(this.catalogItemId, lesson.id, body)
        : await this.api.completeLesson(this.sessionId, lesson.id, body);

      this.patchLessonCompleted(updated);
      this.message.set('Leçon marquée comme vue.');
      // Recharge pour synchroniser canTakeQuiz / progression serveur.
      await this.reload(lesson.id);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Progression impossible');
    } finally {
      this.busy.set(false);
    }
  }

  private patchLessonCompleted(updated: TrainingLessonDto): void {
    const p = this.player();
    if (!p) return;
    const modules = p.modules.map((m) => ({
      ...m,
      lessons: m.lessons.map((l) =>
        l.id === updated.id
          ? {
              ...l,
              isCompleted: true,
              progressPercent: updated.progressPercent ?? 100,
              resources: updated.resources?.length ? updated.resources : l.resources,
            }
          : l,
      ),
    }));
    const required = modules.flatMap((m) => m.lessons).filter((l) => l.isRequired);
    const done = required.filter((l) => l.isCompleted).length;
    const percent =
      required.length === 0 ? 100 : Math.round((done / required.length) * 1000) / 10;
    const canTake =
      !!p.defaultQuizTemplateId || !!this.sessionId ? percent >= 100 : p.canTakeQuiz;
    this.player.set({
      ...p,
      modules,
      requiredLessonsDone: done,
      requiredLessonsTotal: required.length,
      progressPercent: percent,
      canTakeQuiz: canTake || p.canTakeQuiz,
      quizBlockedReason: canTake
        ? null
        : p.quizBlockedReason || 'Terminez les leçons obligatoires avant de passer le quiz.',
    });
    this.activeLesson.set({
      ...updated,
      isCompleted: true,
      progressPercent: updated.progressPercent ?? 100,
      resources: updated.resources?.length
        ? updated.resources
        : (this.activeLesson()?.resources ?? []),
    });
  }
}
