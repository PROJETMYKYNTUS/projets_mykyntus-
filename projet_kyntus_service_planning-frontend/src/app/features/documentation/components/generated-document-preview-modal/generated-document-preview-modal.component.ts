import { CommonModule } from '@angular/common';
import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges } from '@angular/core';
import { Subscription } from 'rxjs';
import mammoth from 'mammoth';

import { DocumentationDataApiService } from '../../../../core/services/documentation-data-api.service';
import { formatDocumentationHttpError } from '../../lib/documentation-download.util';
import { GeneratedDocumentFormatMenuComponent } from '../generated-document-format-menu/generated-document-format-menu.component';

@Component({
  standalone: true,
  selector: 'app-generated-document-preview-modal',
  imports: [CommonModule, GeneratedDocumentFormatMenuComponent],
  templateUrl: './generated-document-preview-modal.component.html',
})
export class GeneratedDocumentPreviewModalComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) open = false;
  @Input() title = 'Document';
  @Input() subtitle: string | null = null;
  /** Identifiant du document gÃ©nÃ©rÃ© (PDF stockÃ© + export). */
  @Input() generatedDocumentId: string | null = null;
  /** Base de nom de fichier pour les exports (sans extension). */
  @Input() exportFileNameBase: string | null = null;

  @Output() closed = new EventEmitter<void>();

  pdfObjectUrl: string | null = null;
  loadingPdf = false;
  pdfError: string | null = null;
  previewMimeType: string | null = null;
  previewKind: 'pdf' | 'docx' | 'html' | 'text' | 'other' | null = null;
  previewHtml: SafeHtml | null = null;
  previewText: string | null = null;

  private sub = new Subscription();

  constructor(
    private readonly data: DocumentationDataApiService,
    private readonly sanitizer: DomSanitizer,
  ) {}

  get safePdfSrc(): SafeResourceUrl | null {
    const u = this.pdfObjectUrl;
    return u ? this.sanitizer.bypassSecurityTrustResourceUrl(u) : null;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] || changes['generatedDocumentId']) {
      if (this.open && this.generatedDocumentId?.trim()) {
        this.loadPdfPreview();
      }
      if (!this.open) {
        this.teardownPreview();
      }
    }
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
    this.revokePdfUrl();
  }

  closeModal(): void {
    this.closed.emit();
  }

  private loadPdfPreview(): void {
    const id = this.generatedDocumentId?.trim();
    if (!id) return;
    this.loadingPdf = true;
    this.pdfError = null;
    this.previewMimeType = null;
    this.previewKind = null;
    this.previewHtml = null;
    this.previewText = null;
    this.revokePdfUrl();
    this.sub.add(
      this.data.downloadGeneratedDocument(id).subscribe({
        next: async (resp: HttpResponse<Blob>) => {
          this.loadingPdf = false;
          const blob = resp.body;
          if (!blob || blob.size === 0) {
            this.pdfError = 'AperÃ§u indisponible.';
            return;
          }
          const mime = resp.headers.get('content-type')?.split(';')[0]?.trim().toLowerCase() || blob.type?.toLowerCase() || '';
          this.previewMimeType = mime || null;
          await this.applyPreviewBlob(blob, mime);
        },
        error: (e: unknown) => {
          this.loadingPdf = false;
          if (e instanceof HttpErrorResponse && e.error instanceof Blob) {
            void formatDocumentationHttpError(e).then((m) => (this.pdfError = m));
          } else {
            this.pdfError = 'Impossible de charger le PDF.';
          }
        },
      }),
    );
  }

  private teardownPreview(): void {
    this.loadingPdf = false;
    this.pdfError = null;
    this.previewMimeType = null;
    this.previewKind = null;
    this.previewHtml = null;
    this.previewText = null;
    this.revokePdfUrl();
  }

  private revokePdfUrl(): void {
    if (this.pdfObjectUrl) {
      URL.revokeObjectURL(this.pdfObjectUrl);
      this.pdfObjectUrl = null;
    }
  }

  private async applyPreviewBlob(blob: Blob, mime: string): Promise<void> {
    if (mime === 'application/pdf') {
      const pdfBlob = new Blob([blob], { type: 'application/pdf' });
      this.pdfObjectUrl = URL.createObjectURL(pdfBlob);
      this.previewKind = 'pdf';
      return;
    }

    if (mime.includes('wordprocessingml') || mime === 'application/msword') {
      try {
        const buf = await blob.arrayBuffer();
        const result = await mammoth.convertToHtml({ arrayBuffer: buf });
        this.previewHtml = this.sanitizer.bypassSecurityTrustHtml(result.value);
        this.previewKind = 'docx';
        return;
      } catch {
        this.previewKind = 'other';
        this.pdfError = 'Le document Word est disponible mais son aperÃ§u intÃ©grÃ© a Ã©chouÃ©. Utilisez TÃ©lÃ©charger.';
        return;
      }
    }

    if (mime.includes('html')) {
      this.previewHtml = this.sanitizer.bypassSecurityTrustHtml(await blob.text());
      this.previewKind = 'html';
      return;
    }

    if (mime.startsWith('text/')) {
      this.previewText = await blob.text();
      this.previewKind = 'text';
      return;
    }

    this.previewKind = 'other';
    this.pdfError = 'AperÃ§u intÃ©grÃ© non disponible pour ce format, mais le document reste tÃ©lÃ©chargeable.';
  }
}
