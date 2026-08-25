import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, SimpleChanges, inject } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { MediaService } from '../../../core/services/media.service';

/** Affiche une image media authentifiée (Bearer) via blob URL. */
@Component({
  selector: 'app-ky-auth-media-img',
  standalone: true,
  imports: [CommonModule],
  template: `<img *ngIf="safeSrc" [src]="safeSrc" [alt]="alt" [class]="className" />`,
})
export class KyAuthMediaImgComponent implements OnChanges {
  private readonly media = inject(MediaService);
  private readonly sanitizer = inject(DomSanitizer);

  @Input() mediaId: number | null | undefined;
  @Input() url: string | null | undefined;
  @Input() alt = '';
  @Input() className = '';

  safeSrc: SafeUrl | null = null;

  ngOnChanges(_changes: SimpleChanges): void {
    const id = this.mediaId ?? this.parseId(this.url);
    if (!id) {
      // legacy data-URL or external URL
      if (this.url && (this.url.startsWith('data:') || this.url.startsWith('http'))) {
        this.safeSrc = this.sanitizer.bypassSecurityTrustUrl(this.url);
      } else {
        this.safeSrc = null;
      }
      return;
    }
    this.media.blobUrl(id).subscribe(blob => {
      this.safeSrc = this.sanitizer.bypassSecurityTrustUrl(blob);
    });
  }

  private parseId(url?: string | null): number | null {
    if (!url) return null;
    const m = url.match(/\/api\/media\/(\d+)/i);
    return m ? +m[1] : null;
  }
}
