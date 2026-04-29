import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';

import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import type { DocumentationRequest } from '../interfaces/documentation-entities';
import { switchMapOnDocumentationContext } from '../lib/documentation-context-refresh';
import {
  generatedDocumentDisplayLabel,
  generatedDocumentExportBaseName,
  mapDocumentRequestDto,
} from '../lib/documentation-dto-mappers';
import { DocumentationApiService } from '../services/documentation-api.service';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';
import { GeneratedDocumentFormatMenuComponent } from '../components/generated-document-format-menu/generated-document-format-menu.component';
import { GeneratedDocumentPreviewModalComponent } from '../components/generated-document-preview-modal/generated-document-preview-modal.component';
import { StatusBadgeComponent } from '../components/status-badge/status-badge.component';

@Component({
  standalone: true,
  selector: 'app-request-tracking-page',
  imports: [
    CommonModule,
    StatusBadgeComponent,
    DocIconComponent,
    GeneratedDocumentFormatMenuComponent,
    GeneratedDocumentPreviewModalComponent,
  ],
  templateUrl: './request-tracking-page.component.html',
})
export class RequestTrackingPageComponent implements OnInit, OnDestroy {
  /** Demandes visibles pour le pilote (demandeur ou bÃ©nÃ©ficiaire), via API filtrÃ©e par rÃ´le. */
  requests: DocumentationRequest[] = [];
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
  ) {}

  ngOnInit(): void {
    this.sub.add(
      switchMapOnDocumentationContext(this.identity, () => this.api.getAllDocumentRequests()).subscribe({
        next: (rows) => {
          this.requests = rows.map(mapDocumentRequestDto);
          this.loading = false;
          this.error = null;
        },
        error: () => {
          this.requests = [];
          this.loading = false;
          this.error = 'Impossible de charger le suivi des demandes.';
        },
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  hasDocument(r: DocumentationRequest): boolean {
    return !!(r.generatedDocumentId?.trim() || r.status === 'Generated');
  }

  showProcessingHint(r: DocumentationRequest): boolean {
    return !this.hasDocument(r) && (r.status === 'Pending' || r.status === 'Approved');
  }

  openPreview(r: DocumentationRequest): void {
    const id = r.generatedDocumentId?.trim();
    if (!id) return;
    this.previewGeneratedId = id;
    this.previewTitle = generatedDocumentDisplayLabel({
      employeeName: r.employeeName,
      type: r.type,
      generatedAt: r.generatedAt,
      requestDate: r.requestDate,
    });
    this.previewSubtitle = `${r.id}`;
    this.previewExportFileNameBase = generatedDocumentExportBaseName({
      employeeName: r.employeeName,
      type: r.type,
      generatedAt: r.generatedAt,
      requestDate: r.requestDate,
    });
    this.previewOpen = true;
  }

  closePreview(): void {
    this.previewOpen = false;
    this.previewGeneratedId = null;
    this.previewExportFileNameBase = null;
  }

  exportFileNameHint(r: DocumentationRequest): string | null {
    if (!r.generatedDocumentId?.trim()) return null;
    return generatedDocumentExportBaseName({
      employeeName: r.employeeName,
      type: r.type,
      generatedAt: r.generatedAt,
      requestDate: r.requestDate,
    });
  }

}
