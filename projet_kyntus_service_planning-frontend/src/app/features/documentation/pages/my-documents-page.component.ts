import { CommonModule } from '@angular/common';
import { HttpResponse } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { DocumentationDataApiService } from '../../../core/services/documentation-data-api.service';
import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import { DocumentationNotificationService } from '../../../core/services/documentation-notification.service';
import type { DocumentationDocument } from '../interfaces/documentation-entities';
import { switchMapOnDocumentationContext } from '../lib/documentation-context-refresh';
import {
  generatedDocumentExportBaseName,
  mapAssignedRequestToDocument,
  mapDocumentRequestDto,
} from '../lib/documentation-dto-mappers';
import {
  formatDocumentationHttpError,
  triggerDownloadFromHttpResponse,
} from '../lib/documentation-download.util';
import { DocumentationApiService } from '../services/documentation-api.service';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';
import { GeneratedDocumentFormatMenuComponent } from '../components/generated-document-format-menu/generated-document-format-menu.component';
import { GeneratedDocumentPreviewModalComponent } from '../components/generated-document-preview-modal/generated-document-preview-modal.component';
import { StatusBadgeComponent } from '../components/status-badge/status-badge.component';

@Component({
  standalone: true,
  selector: 'app-my-documents-page',
  imports: [
    CommonModule,
    FormsModule,
    DocIconComponent,
    StatusBadgeComponent,
    GeneratedDocumentFormatMenuComponent,
    GeneratedDocumentPreviewModalComponent,
  ],
  templateUrl: './my-documents-page.component.html',
})
export class MyDocumentsPageComponent implements OnInit, OnDestroy {
  @ViewChild('bulkDownloadDetails') private bulkDownloadDetails?: ElementRef<HTMLDetailsElement>;

  search = '';

  private documents: DocumentationDocument[] = [];
  loading = true;
  error: string | null = null;
  previewOpen = false;
  previewGeneratedId: string | null = null;
  previewTitle = '';
  previewSubtitle: string | null = null;
  previewExportFileNameBase: string | null = null;
  private sub = new Subscription();

  constructor(
    private readonly api: DocumentationApiService,
    private readonly identity: DocumentationIdentityService,
    private readonly data: DocumentationDataApiService,
    private readonly notify: DocumentationNotificationService,
  ) {}

  ngOnInit(): void {
    this.sub.add(
      switchMapOnDocumentationContext(this.identity, () => this.api.getAllAssignedDocumentRequests()).subscribe({
        next: (rows) => {
          const ui = rows.map(mapDocumentRequestDto);
          this.documents = ui
            .filter((r) => (r.status ?? '').trim().toLowerCase() === 'generated' || !!r.generatedDocumentId?.trim())
            .map(mapAssignedRequestToDocument);
          this.loading = false;
          this.error = null;
        },
        error: () => {
          this.documents = [];
          this.loading = false;
          this.error = 'Impossible de charger les documents.';
        },
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  filtered() {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.documents;
    return this.documents.filter(
      (doc) => doc.name.toLowerCase().includes(q) || doc.type.toLowerCase().includes(q),
    );
  }

  canOpenOrDownload(doc: DocumentationDocument): boolean {
    return !!(doc.generatedDocumentId?.trim() || doc.status === 'Generated');
  }

  private closeBulkDownloadMenu(): void {
    const el = this.bulkDownloadDetails?.nativeElement;
    if (el) el.open = false;
  }

  /** TÃ©lÃ©charge chaque document visible (liste filtrÃ©e), un fichier aprÃ¨s lâ€™autre. */
  downloadAllFiltered(format: 'pdf' | 'docx'): void {
    this.closeBulkDownloadMenu();
    const list = this.filtered().filter((doc) => this.canOpenOrDownload(doc));
    if (list.length === 0) {
      this.notify.showError('Aucun document Ã  tÃ©lÃ©charger pour cette liste.');
      return;
    }
    let index = 0;
    const runNext = (): void => {
      if (index >= list.length) {
        this.notify.showSuccess(`${list.length} tÃ©lÃ©chargement(s) lancÃ©(s).`);
        return;
      }
      const doc = list[index++];
      const id = doc.generatedDocumentId?.trim();
      if (!id) {
        runNext();
        return;
      }
      const rawBase = (doc.exportFileBase ?? 'document').trim() || 'document';
      const base = rawBase.replace(/[/\\?%*:|"<>]/g, '-').replace(/\s+/g, '_');
      const ext = format === 'pdf' ? 'pdf' : 'docx';
      const fallback = `${base}.${ext}`;
      this.sub.add(
        this.data.exportGeneratedDocument(id, format).subscribe({
          next: (resp: HttpResponse<Blob>) => {
            triggerDownloadFromHttpResponse(resp, fallback);
            setTimeout(runNext, 450);
          },
          error: (e: unknown) => {
            void formatDocumentationHttpError(e).then((msg) => this.notify.showError(msg));
            setTimeout(runNext, 450);
          },
        }),
      );
    };
    runNext();
  }

  openPreview(doc: DocumentationDocument): void {
    const id = doc.generatedDocumentId?.trim();
    if (!id) return;
    this.previewGeneratedId = id;
    this.previewTitle = doc.name?.trim() || 'Document gÃ©nÃ©rÃ©';
    this.previewSubtitle = doc.type;
    this.previewExportFileNameBase =
      doc.exportFileBase?.trim() ||
      generatedDocumentExportBaseName({
        employeeName: doc.employeeName ?? '',
        type: doc.type,
        generatedAt: doc.status === 'Generated' ? doc.dateCreated : null,
        requestDate: doc.dateCreated,
      });
    this.previewOpen = true;
  }

  closePreview(): void {
    this.previewOpen = false;
    this.previewGeneratedId = null;
    this.previewExportFileNameBase = null;
  }
}
