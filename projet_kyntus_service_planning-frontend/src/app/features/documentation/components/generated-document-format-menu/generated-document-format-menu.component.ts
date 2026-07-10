import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';

import { DocumentationDataApiService } from '../../../../core/services/documentation-data-api.service';
import { DocumentationNotificationService } from '../../../../core/services/documentation-notification.service';
import {
  formatDocumentationHttpError,
  triggerDownloadFromHttpResponse,
} from '../../lib/documentation-download.util';
import type { DocumentationExportFormat } from '../../lib/documentation-export-formats.util';
import { DocIconComponent } from '../doc-icon/doc-icon.component';

@Component({
  selector: 'app-generated-document-format-menu',
  standalone: true,
  imports: [CommonModule, DocIconComponent],
  template: `
    @if (generatedDocumentId?.trim()) {
      <details class="doc-download-details doc-format-menu-host relative inline-block text-left">
        <summary
          class="list-none cursor-pointer [&::-webkit-details-marker]:hidden"
          [class]="appearance === 'compact' ? 'btn-download text-xs px-2.5 py-1.5' : 'btn-download'"
          [attr.aria-label]="'Télécharger ' + (fileNameBase || 'document')"
        >
          <app-doc-icon name="download" klass="w-4 h-4 shrink-0" />
          @if (appearance !== 'compact') {
            <span>Télécharger</span>
          }
          <span class="opacity-90 text-xs" aria-hidden="true">▾</span>
        </summary>
        <div
          class="doc-download-dropdown absolute right-0 mt-2 py-1 z-[70]"
          role="menu"
          (click)="$event.stopPropagation()"
        >
          <p class="px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-muted">Télécharger en</p>
          @for (fmt of visibleFormats; track fmt) {
            <button
              type="button"
              role="menuitem"
              [disabled]="busy"
              (click)="download(fmt)"
            >
              {{ fmt === 'pdf' ? 'PDF (.pdf)' : 'Word (.docx)' }}
            </button>
          }
        </div>
      </details>
    }
  `,
})
export class GeneratedDocumentFormatMenuComponent {
  private readonly data = inject(DocumentationDataApiService);
  private readonly notify = inject(DocumentationNotificationService);

  @Input() generatedDocumentId: string | null = null;
  @Input() fileNameBase: string | null = null;
  @Input() appearance: 'standard' | 'compact' = 'standard';
  @Input() allowedFormats: DocumentationExportFormat[] = ['pdf', 'docx'];
  @Output() formatSelected = new EventEmitter<DocumentationExportFormat>();

  busy = false;

  get visibleFormats(): DocumentationExportFormat[] {
    const allowed = new Set(this.allowedFormats);
    return (['pdf', 'docx'] as DocumentationExportFormat[]).filter((f) => allowed.has(f));
  }

  download(format: DocumentationExportFormat): void {
    const id = this.generatedDocumentId?.trim();
    if (!id || this.busy) return;
    this.formatSelected.emit(format);
    this.busy = true;
    const rawBase = (this.fileNameBase ?? 'document').trim() || 'document';
    const base = rawBase.replace(/[/\\?%*:|"<>]/g, '-').replace(/\s+/g, '_');
    const fallback = `${base}.${format === 'pdf' ? 'pdf' : 'docx'}`;
    this.data.exportGeneratedDocument(id, format).subscribe({
      next: (resp: HttpResponse<Blob>) => {
        triggerDownloadFromHttpResponse(resp, fallback);
        this.busy = false;
      },
      error: (e: unknown) => {
        this.busy = false;
        void formatDocumentationHttpError(e).then((msg) => this.notify.showError(msg));
      },
    });
  }
}
