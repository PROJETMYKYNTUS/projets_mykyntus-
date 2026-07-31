import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ArrowLeft, CheckCircle2 } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type { CatalogPlayerDto, TrainingLessonDto, TrainingResourceDto } from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';

@Component({
  selector: 'app-formation-catalog-player',
  standalone: true,
  imports: [CommonModule, RouterLink, KyntusPageHeaderComponent, LucideIconComponent],
  templateUrl: './formation-catalog-player.component.html',
  styleUrls: ['./formation-catalog-player.component.css'],
})
export class FormationCatalogPlayerComponent implements OnInit {
  readonly icons = { back: ArrowLeft, done: CheckCircle2 };
  private readonly api = inject(FormationTrainingService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly player = signal<CatalogPlayerDto | null>(null);
  readonly activeLesson = signal<TrainingLessonDto | null>(null);
  readonly activeResource = signal<TrainingResourceDto | null>(null);

  sessionId = '';
  userId = '';

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    this.userId = this.resolveUserId();
    if (!this.sessionId || !this.userId) {
      void this.router.navigate(['/mes-formations']);
      return;
    }
    void this.reload();
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const p = await this.api.getCatalogPlayer(this.sessionId, this.userId);
      this.player.set(p);
      const firstIncomplete =
        p.modules.flatMap((m) => m.lessons).find((l) => !l.isCompleted) ??
        p.modules.flatMap((m) => m.lessons)[0] ??
        null;
      this.selectLesson(firstIncomplete);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Chargement impossible');
    } finally {
      this.loading.set(false);
    }
  }

  selectLesson(lesson: TrainingLessonDto | null): void {
    this.activeLesson.set(lesson);
    this.activeResource.set(lesson?.resources?.[0] ?? null);
  }

  selectResource(resource: TrainingResourceDto): void {
    this.activeResource.set(resource);
  }

  async completeLesson(): Promise<void> {
    const lesson = this.activeLesson();
    if (!lesson) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.completeLesson(this.sessionId, lesson.id, {
        employeeId: this.userId,
        lastResourceId: this.activeResource()?.id ?? null,
      });
      await this.reload();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Progression impossible');
    } finally {
      this.busy.set(false);
    }
  }

  embedUrl(resource: TrainingResourceDto): SafeResourceUrl | null {
    const url = resource.url || resource.downloadPath;
    if (!url) return null;
    let embed = url;
    if (/youtube\.com\/watch\?v=/.test(url)) {
      embed = url.replace('watch?v=', 'embed/');
    } else if (/youtu\.be\//.test(url)) {
      embed = url.replace('youtu.be/', 'www.youtube.com/embed/');
    } else if (/vimeo\.com\/(\d+)/.test(url)) {
      embed = url.replace(/vimeo\.com\/(\d+)/, 'player.vimeo.com/video/$1');
    }
    return this.sanitizer.bypassSecurityTrustResourceUrl(embed);
  }

  isPdf(resource: TrainingResourceDto): boolean {
    return resource.type === 'Pdf' || resource.type === 0;
  }

  isVideo(resource: TrainingResourceDto): boolean {
    return resource.type === 'Video' || resource.type === 1;
  }

  isExternalVideo(resource: TrainingResourceDto): boolean {
    const url = resource.url || '';
    return /youtube\.com|youtu\.be|vimeo\.com/.test(url);
  }

  private resolveUserId(): string {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    if (typeof user?.id === 'string' && user.id.includes('-')) return user.id;
    if (user?.guid && String(user.guid).includes('-')) return String(user.guid);
    const padded = String(user?.id ?? '').padStart(12, '0');
    return `00000000-0000-0000-0000-${padded}`;
  }
}
