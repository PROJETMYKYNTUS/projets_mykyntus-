import { CommonModule, DatePipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { KyMediaGalleryComponent } from '../../shared/components/ui/ky-media-gallery.component';
import { KyAuthMediaImgComponent } from '../../shared/components/ui/ky-auth-media-img.component';
import { MediaAsset } from '../../core/services/media.service';

@Component({
  selector: 'app-newsletter-reader',
  standalone: true,
  imports: [CommonModule, DatePipe, KyMediaGalleryComponent, KyAuthMediaImgComponent],
  template: `
    <article class="nl-reader ky-card">
      <header class="nl-reader-head">
        <h2>{{ title }}</h2>
        <p class="nl-reader-subject" *ngIf="subject">{{ subject }}</p>
        <p class="nl-reader-meta" *ngIf="meta">{{ meta }}</p>
        <p class="nl-reader-meta" *ngIf="receivedAt">
          Reçue le {{ receivedAt | date:'dd/MM/yyyy à HH:mm' }}
        </p>
      </header>

      <div class="nl-reader-cover" *ngIf="coverImageUrl && !(media?.length)">
        <app-ky-auth-media-img [url]="coverImageUrl" alt="Illustration" className="nl-cover-img"></app-ky-auth-media-img>
      </div>

      <div class="nl-reader-body">
        <div class="nl-reader-text" *ngIf="textContent; else htmlBlock">{{ textContent }}</div>
        <ng-template #htmlBlock>
          <div class="nl-reader-html" [innerHTML]="safeHtml"></div>
        </ng-template>
      </div>

      <app-ky-media-gallery *ngIf="media?.length" [media]="media!"></app-ky-media-gallery>
    </article>
  `,
  styles: [`
    .nl-reader { padding: 22px; display: grid; gap: 16px; }
    .nl-reader-head h2 { margin: 0 0 6px; font-size: 1.45rem; color: var(--text-primary); }
    .nl-reader-subject { margin: 0; color: var(--text-muted); }
    .nl-reader-meta { margin: 6px 0 0; font-size: 0.84rem; color: var(--text-muted); }
    .nl-reader-cover :host ::ng-deep img,
    :host ::ng-deep .nl-cover-img {
      width: 100%; max-height: 320px; object-fit: cover;
      border-radius: var(--radius-card); border: 1px solid var(--border-color);
    }
    .nl-reader-text, .nl-reader-html {
      white-space: pre-wrap; line-height: 1.7; color: var(--text-primary);
      padding-top: 8px; border-top: 1px solid var(--border-color);
    }
    .nl-reader-html { white-space: normal; }
  `]
})
export class NewsletterReaderComponent {
  @Input() title = '';
  @Input() subject = '';
  @Input() meta = '';
  @Input() textContent: string | null | undefined;
  @Input() htmlContent: string | null | undefined;
  @Input() coverImageUrl: string | null | undefined;
  @Input() receivedAt: string | null | undefined;
  @Input() media: MediaAsset[] | null | undefined;

  get safeHtml(): string {
    return this.htmlContent ?? '';
  }
}
