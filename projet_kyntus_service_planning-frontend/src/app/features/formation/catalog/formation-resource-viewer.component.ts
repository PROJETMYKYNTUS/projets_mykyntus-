import { CommonModule } from '@angular/common';
import {
  Component,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  inject,
} from '@angular/core';
import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import type { TrainingResourceDto } from '../../../core/models/formation-training.models';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import {
  isExternalVideoUrl,
  isImageResource,
  isPdfResource,
  isTextResource,
  isVideoResource,
  trustLearningHtml,
  trustResourceEmbed,
} from '../../../core/lib/formation-learning-html.util';

const tokenCache = new Map<string, { url: string; expiresAt: number }>();

/** Rendu unique d’une ressource catalogue (player + prévisualisation admin). */
@Component({
  selector: 'app-formation-resource-viewer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './formation-resource-viewer.component.html',
  styleUrls: ['./formation-resource-viewer.component.css'],
})
export class FormationResourceViewerComponent implements OnChanges, OnDestroy {
  private readonly sanitizer = inject(DomSanitizer);
  private readonly api = inject(FormationTrainingService);

  @Input({ required: true }) resource!: TrainingResourceDto;
  @Input() textOverride: string | null = null;
  @Input() urlOverride: string | null = null;
  /**
   * Si true : PDF/vidéo/image ne se chargent qu’après clic « Afficher ».
   * Défaut false = affichage direct (préféré).
   */
  @Input() lazyMedia = false;

  safeHtml: SafeHtml | null = null;
  embed: SafeResourceUrl | null = null;
  mediaSrc: string | null = null;
  mediaLoading = false;
  mediaError: string | null = null;
  imageZoomed = false;
  mediaRevealed = false;

  private loadToken = 0;
  private lastResourceId = '';

  ngOnChanges(changes: SimpleChanges): void {
    const id = this.resource?.id ?? '';
    if (id !== this.lastResourceId) {
      this.lastResourceId = id;
      this.mediaRevealed = !this.lazyMedia;
    }
    if (changes['lazyMedia'] && !this.lazyMedia) {
      this.mediaRevealed = true;
    }
    void this.refresh();
  }

  ngOnDestroy(): void {}

  get effective(): TrainingResourceDto {
    if (!this.resource) {
      return {
        id: '',
        lessonId: '',
        type: 'Text',
        title: '',
        sortOrder: 0,
        textContent: this.textOverride,
        url: this.urlOverride,
      };
    }
    return {
      ...this.resource,
      textContent: this.textOverride ?? this.resource.textContent,
      url: this.urlOverride ?? this.resource.url,
    };
  }

  isPdf(): boolean {
    return isPdfResource(this.effective);
  }

  isVideo(): boolean {
    return isVideoResource(this.effective);
  }

  isText(): boolean {
    return isTextResource(this.effective);
  }

  isImage(): boolean {
    return isImageResource(this.effective);
  }

  isExternalVideo(): boolean {
    return isExternalVideoUrl(this.effective.url || this.effective.downloadPath || '');
  }

  isHeavyMedia(): boolean {
    return (this.isPdf() || this.isVideo() || this.isImage()) && !this.isExternalVideo();
  }

  needsUserReveal(): boolean {
    return this.lazyMedia && this.isHeavyMedia() && !this.mediaRevealed;
  }

  revealMedia(): void {
    this.mediaRevealed = true;
    void this.refresh();
  }

  directMediaSrc(): string {
    const u = this.effective.url || this.effective.downloadPath || '';
    if (!u) return '';
    if (u.startsWith('blob:') || u.startsWith('data:') || /^https?:\/\//i.test(u)) {
      if (u.includes('/api/formations/catalog/resources/file/')) return '';
      return u;
    }
    return '';
  }

  needsAuthMedia(): boolean {
    const id = this.effective.id;
    if (!id || id.startsWith('prev-') || id === 'preview') return false;
    const path = this.effective.downloadPath || this.effective.url || '';
    return path.includes('/api/formations/catalog/resources/file/') || !!this.effective.downloadPath;
  }

  displaySrc(): string {
    return this.mediaSrc || this.directMediaSrc();
  }

  mediaKindLabel(): string {
    if (this.isPdf()) return 'PDF';
    if (this.isVideo()) return 'Vidéo';
    if (this.isImage()) return 'Image';
    return 'Média';
  }

  toggleZoom(): void {
    this.imageZoomed = !this.imageZoomed;
  }

  private async resolveAccessUrl(resourceId: string): Promise<string> {
    const cached = tokenCache.get(resourceId);
    if (cached && cached.expiresAt > Date.now() + 60_000) {
      return cached.url;
    }
    const access = await this.api.issueResourceAccess(resourceId);
    const expiresAt = Date.parse(access.expiresAt) || Date.now() + 2 * 3600_000;
    tokenCache.set(resourceId, { url: access.url, expiresAt });
    return access.url;
  }

  private async refresh(): Promise<void> {
    const token = ++this.loadToken;
    this.mediaError = null;
    this.imageZoomed = false;
    this.mediaSrc = null;
    this.embed = null;
    this.safeHtml = null;
    const r = this.effective;

    if (this.isText()) {
      this.safeHtml = trustLearningHtml(this.sanitizer, r.textContent);
      return;
    }

    if (this.needsUserReveal()) {
      return;
    }

    if (this.isVideo() && this.isExternalVideo()) {
      this.embed = trustResourceEmbed(this.sanitizer, r);
      return;
    }

    const direct = this.directMediaSrc();
    if (direct) {
      this.mediaSrc = direct;
      if (this.isPdf()) {
        this.embed = this.sanitizer.bypassSecurityTrustResourceUrl(direct);
      }
      return;
    }

    if (this.needsAuthMedia() && (this.isPdf() || this.isVideo() || this.isImage())) {
      this.mediaLoading = true;
      try {
        const url = await this.resolveAccessUrl(r.id);
        if (token !== this.loadToken) return;
        this.mediaSrc = url;
        if (this.isPdf()) {
          this.embed = this.sanitizer.bypassSecurityTrustResourceUrl(url);
        }
      } catch (e) {
        if (token !== this.loadToken) return;
        try {
          const blob = await this.api.downloadCatalogResourceBlob(r.id);
          if (token !== this.loadToken) return;
          this.mediaSrc = URL.createObjectURL(blob);
          if (this.isPdf()) {
            this.embed = this.sanitizer.bypassSecurityTrustResourceUrl(this.mediaSrc);
          }
        } catch {
          this.mediaError = e instanceof Error ? e.message : 'Chargement média impossible';
        }
      } finally {
        if (token === this.loadToken) this.mediaLoading = false;
      }
      return;
    }

    if (this.isPdf()) {
      this.embed = trustResourceEmbed(this.sanitizer, r);
    }
  }
}
