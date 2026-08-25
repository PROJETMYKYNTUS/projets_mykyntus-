import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, SimpleChanges, inject } from '@angular/core';
import { BodyPortalDirective } from '../../directives/body-portal.directive';
import { MediaAsset, MediaService } from '../../../core/services/media.service';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

interface GalleryItem {
  asset: MediaAsset;
  src: SafeResourceUrl | null;
}

@Component({
  selector: 'app-ky-media-gallery',
  standalone: true,
  imports: [CommonModule, BodyPortalDirective],
  template: `
    <div class="ky-mg" *ngIf="resolved.length">
      <article
        class="ky-mg-card"
        *ngFor="let item of resolved"
        (click)="open(item)">
        <ng-container [ngSwitch]="item.asset.kind">
          <img *ngSwitchCase="'Image'" [src]="item.src" [alt]="item.asset.fileName" />
          <div *ngSwitchCase="'Video'" class="ky-mg-video-thumb">
            <video *ngIf="item.src" [src]="item.src" muted></video>
            <span>▶ Vidéo</span>
          </div>
          <div *ngSwitchDefault class="ky-mg-doc">
            <span>📄</span>
            <strong>{{ item.asset.fileName }}</strong>
          </div>
        </ng-container>
      </article>
    </div>

    <div class="ky-mg-lightbox" *ngIf="lightbox" appBodyPortal (click)="lightbox=null">
      <div class="ky-mg-lightbox-inner" (click)="$event.stopPropagation()">
        <button type="button" class="ky-btn-secondary" (click)="lightbox=null">Fermer</button>
        <img *ngIf="lightbox.asset.kind==='Image'" [src]="lightbox.src" [alt]="lightbox.asset.fileName" />
        <video *ngIf="lightbox.asset.kind==='Video' && lightbox.src" [src]="lightbox.src" controls autoplay></video>
        <a *ngIf="lightbox.asset.kind==='Document'" class="ky-btn-primary" [href]="lightbox.src" download>
          Télécharger {{ lightbox.asset.fileName }}
        </a>
      </div>
    </div>
  `,
  styles: [`
    .ky-mg {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 10px;
    }
    @media (max-width: 768px) {
      .ky-mg { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    .ky-mg-card {
      aspect-ratio: 16 / 10;
      border-radius: var(--radius-md, 0.5rem);
      overflow: hidden;
      border: 1px solid var(--border-color);
      background: var(--bg-input);
      cursor: pointer;
      display: flex; align-items: center; justify-content: center;
    }
    .ky-mg-card img, .ky-mg-card video {
      width: 100%; height: 100%; object-fit: cover;
    }
    .ky-mg-video-thumb, .ky-mg-doc {
      position: relative; width: 100%; height: 100%;
      display: grid; place-items: center; color: var(--text-primary); gap: 6px; padding: 8px; text-align: center;
    }
    .ky-mg-video-thumb video { position: absolute; inset: 0; opacity: 0.55; }
    .ky-mg-video-thumb span { position: relative; font-weight: 700; background: color-mix(in srgb, var(--bg-card) 80%, transparent); padding: 4px 10px; border-radius: 999px; }
    .ky-mg-doc strong { font-size: 0.78rem; word-break: break-all; }
    .ky-mg-lightbox {
      position: fixed; inset: 0; z-index: 10000;
      background: color-mix(in srgb, #000 55%, transparent);
      display: grid; place-items: center; padding: 20px;
    }
    .ky-mg-lightbox-inner {
      max-width: min(960px, 100%); max-height: 90vh; overflow: auto;
      background: var(--bg-card); border-radius: var(--radius-card);
      padding: 16px; display: grid; gap: 12px;
    }
    .ky-mg-lightbox-inner img, .ky-mg-lightbox-inner video {
      max-width: 100%; max-height: 75vh; border-radius: var(--radius-md);
    }
  `]
})
export class KyMediaGalleryComponent implements OnChanges {
  private readonly mediaSvc = inject(MediaService);
  private readonly sanitizer = inject(DomSanitizer);

  @Input() media: MediaAsset[] = [];
  resolved: GalleryItem[] = [];
  lightbox: GalleryItem | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['media']) this.load();
  }

  open(item: GalleryItem): void {
    this.lightbox = item;
  }

  private load(): void {
    this.resolved = [];
    for (const asset of this.media ?? []) {
      this.mediaSvc.blobUrl(asset.id).subscribe(url => {
        const safe = this.sanitizer.bypassSecurityTrustResourceUrl(url);
        const entry = { asset, src: safe };
        const idx = this.resolved.findIndex(r => r.asset.id === asset.id);
        if (idx >= 0) this.resolved[idx] = entry;
        else this.resolved = [...this.resolved, entry];
      });
    }
  }
}
