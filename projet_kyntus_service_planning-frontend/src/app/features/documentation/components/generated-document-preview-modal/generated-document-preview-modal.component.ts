import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  inject,
} from '@angular/core';
import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import { HttpResponse } from '@angular/common/http';
import mammoth from 'mammoth';

import { DocumentationDataApiService } from '../../../../core/services/documentation-data-api.service';
import {
  formatDocumentationHttpError,
  triggerBlobDownload,
  triggerDownloadFromHttpResponse,
} from '../../lib/documentation-download.util';
import { DocIconComponent } from '../doc-icon/doc-icon.component';

@Component({
  selector: 'app-generated-document-preview-modal',
  standalone: true,
  imports: [CommonModule, DocIconComponent],
  template: `
    @if (open) {
      <div
        class="fixed inset-0 z-[80] flex items-center justify-center bg-black/60 p-4"
        role="dialog"
        aria-modal="true"
        (click)="onClose()"
      >
        <div
          class="flex max-h-[90vh] w-full max-w-5xl flex-col overflow-hidden rounded-xl border border-default bg-card shadow-2xl"
          (click)="$event.stopPropagation()"
        >
          <div class="flex items-start justify-between gap-3 border-b border-default px-4 py-3">
            <div class="min-w-0">
              <h2 class="truncate text-base font-semibold text-primary">{{ title || 'Aperçu' }}</h2>
              @if (subtitle) {
                <p class="truncate text-xs text-muted">{{ subtitle }}</p>
              }
            </div>
            <div class="flex shrink-0 items-center gap-2">
              @if (canDownloadPdf) {
                <button
                  type="button"
                  class="btn-download text-xs"
                  [disabled]="loading || !!error"
                  (click)="downloadPdf()"
                >
                  <app-doc-icon name="download" klass="w-4 h-4" />
                  PDF
                </button>
              }
              <button
                type="button"
                class="rounded-lg p-2 text-muted hover:bg-input hover:text-primary"
                title="Fermer"
                (click)="onClose()"
              >
                <app-doc-icon name="x" klass="w-5 h-5" />
              </button>
            </div>
          </div>

          <div class="relative min-h-[60vh] flex-1 bg-navy-950">
            @if (loading) {
              <div class="absolute inset-0 flex items-center justify-center text-sm text-muted">
                Chargement de l’aperçu…
              </div>
            } @else if (error) {
              <div class="absolute inset-0 flex flex-col items-center justify-center gap-2 px-6 text-center">
                <p class="text-sm text-red-400">{{ error }}</p>
                <button type="button" class="btn-secondary text-xs" (click)="reload()">Réessayer</button>
              </div>
            } @else if (previewKind === 'pdf' && pdfSafeUrl) {
              <iframe class="h-full min-h-[60vh] w-full border-0" title="Aperçu PDF" [src]="pdfSafeUrl"></iframe>
            } @else if (previewKind === 'docx' && docxHtml) {
              <div class="h-full max-h-[70vh] overflow-auto bg-white p-6 text-slate-900" [innerHTML]="docxHtml"></div>
            } @else {
              <div class="absolute inset-0 flex items-center justify-center text-sm text-muted">
                Aperçu indisponible pour ce format.
              </div>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class GeneratedDocumentPreviewModalComponent implements OnChanges, OnDestroy {
  private readonly data = inject(DocumentationDataApiService);
  private readonly sanitizer = inject(DomSanitizer);

  @Input() open = false;
  @Input() title: string | null = null;
  @Input() subtitle: string | null = null;
  @Input() generatedDocumentId: string | null = null;
  @Input() exportFileNameBase: string | null = null;
  /** Émis aussi sous le nom historique `closed` via alias dans les templates. */
  @Output() close = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  loading = false;
  error: string | null = null;
  previewKind: 'pdf' | 'docx' | null = null;
  pdfSafeUrl: SafeResourceUrl | null = null;
  docxHtml: SafeHtml | null = null;
  private blobUrl: string | null = null;
  private cachedPdfBlob: Blob | null = null;

  get canDownloadPdf(): boolean {
    return !!this.cachedPdfBlob || this.previewKind === 'pdf';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] || changes['generatedDocumentId']) {
      if (this.open && this.generatedDocumentId?.trim()) {
        this.reload();
      } else if (!this.open) {
        this.resetPreview();
      }
    }
  }

  ngOnDestroy(): void {
    this.resetPreview();
  }

  onClose(): void {
    this.close.emit();
    this.closed.emit();
  }

  reload(): void {
    const id = this.generatedDocumentId?.trim();
    if (!id) return;
    this.resetPreview();
    this.loading = true;
    this.error = null;
    this.data.downloadGeneratedDocument(id).subscribe({
      next: (resp) => void this.handleFileResponse(resp),
      error: (e: unknown) => {
        // Fallback : tenter l’export PDF si le fichier brut échoue (ex. conversion).
        this.data.exportGeneratedDocument(id, 'pdf').subscribe({
          next: (resp) => void this.handleFileResponse(resp),
          error: (e2: unknown) => {
            this.loading = false;
            void formatDocumentationHttpError(e2 ?? e).then((msg) => {
              this.error = msg;
            });
          },
        });
      },
    });
  }

  downloadPdf(): void {
    const base = (this.exportFileNameBase ?? this.title ?? 'document').trim() || 'document';
    const safe = base.replace(/[/\\?%*:|"<>]/g, '-').replace(/\s+/g, '_');
    if (this.cachedPdfBlob) {
      triggerBlobDownload(this.cachedPdfBlob, `${safe}.pdf`);
      return;
    }
    const id = this.generatedDocumentId?.trim();
    if (!id) return;
    this.data.exportGeneratedDocument(id, 'pdf').subscribe({
      next: (resp: HttpResponse<Blob>) => triggerDownloadFromHttpResponse(resp, `${safe}.pdf`),
      error: (e: unknown) => {
        void formatDocumentationHttpError(e).then((msg) => {
          this.error = msg;
        });
      },
    });
  }

  private async handleFileResponse(resp: HttpResponse<Blob>): Promise<void> {
    const body = resp.body;
    if (!body) {
      this.loading = false;
      this.error = 'Fichier vide.';
      return;
    }
    const mime = (body.type || resp.headers.get('Content-Type') || '').toLowerCase();
    try {
      if (mime.includes('pdf') || mime === 'application/octet-stream') {
        // Heuristique : si le backend envoie octet-stream, on essaie PDF d’abord.
        if (mime.includes('wordprocessingml') || mime.includes('officedocument')) {
          await this.showDocx(body);
        } else {
          this.showPdf(body);
        }
      } else if (mime.includes('wordprocessingml') || mime.includes('officedocument') || mime.includes('msword')) {
        await this.showDocx(body);
      } else {
        // Contenu ambigu : tenter PDF.
        this.showPdf(body);
      }
    } catch (err) {
      this.error = err instanceof Error ? err.message : 'Impossible d’afficher l’aperçu.';
    } finally {
      this.loading = false;
    }
  }

  private showPdf(blob: Blob): void {
    this.resetBlobUrl();
    const pdfBlob = blob.type.includes('pdf') ? blob : new Blob([blob], { type: 'application/pdf' });
    this.cachedPdfBlob = pdfBlob;
    this.blobUrl = URL.createObjectURL(pdfBlob);
    this.pdfSafeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.blobUrl);
    this.previewKind = 'pdf';
    this.docxHtml = null;
  }

  private async showDocx(blob: Blob): Promise<void> {
    const buf = await blob.arrayBuffer();
    const result = await mammoth.convertToHtml(
      { arrayBuffer: buf },
      { convertImage: mammoth.images.dataUri },
    );
    this.docxHtml = this.sanitizer.bypassSecurityTrustHtml(result.value);
    this.previewKind = 'docx';
    this.pdfSafeUrl = null;
    this.cachedPdfBlob = null;
  }

  private resetPreview(): void {
    this.resetBlobUrl();
    this.loading = false;
    this.error = null;
    this.previewKind = null;
    this.pdfSafeUrl = null;
    this.docxHtml = null;
    this.cachedPdfBlob = null;
  }

  private resetBlobUrl(): void {
    if (this.blobUrl) {
      URL.revokeObjectURL(this.blobUrl);
      this.blobUrl = null;
    }
  }
}
