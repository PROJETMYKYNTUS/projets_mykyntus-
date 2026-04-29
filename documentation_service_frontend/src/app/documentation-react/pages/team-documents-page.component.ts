import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';

import { DocumentationIdentityService } from '../../core/services/documentation-identity.service';
import type { DocumentationRole } from '../interfaces/documentation-role';
import { switchMapOnDocumentationContext } from '../lib/documentation-context-refresh';
import type { DocumentationDocument, DocumentationRequest } from '../interfaces/documentation-entities';
import { filterByEmployeeScope } from '../lib/documentation-filters';
import {
  generatedDocumentExportBaseName,
  mapDocumentRequestDto,
  mapRequestToGeneratedDocument,
} from '../lib/documentation-dto-mappers';
import { GeneratedDocumentFormatMenuComponent } from '../components/generated-document-format-menu/generated-document-format-menu.component';
import { GeneratedDocumentPreviewModalComponent } from '../components/generated-document-preview-modal/generated-document-preview-modal.component';
import type { HierarchyDrillSelection } from '../lib/documentation-org-hierarchy';
import { DocumentationApiService } from '../services/documentation-api.service';
import { DocumentationHierarchyDrillService } from '../services/documentation-hierarchy-drill.service';
import { DocumentationNavigationService } from '../services/documentation-navigation.service';
import { DocDrillBarComponent } from '../components/doc-drill-bar/doc-drill-bar.component';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';
import { StatusBadgeComponent } from '../components/status-badge/status-badge.component';

@Component({
  standalone: true,
  selector: 'app-team-documents-page',
  imports: [
    CommonModule,
    DocIconComponent,
    StatusBadgeComponent,
    DocDrillBarComponent,
    GeneratedDocumentFormatMenuComponent,
    GeneratedDocumentPreviewModalComponent,
  ],
  templateUrl: './team-documents-page.component.html',
})
export class TeamDocumentsPageComponent implements OnInit, OnDestroy {
  readonly role$ = this.nav.role$;
  drill: HierarchyDrillSelection = {};
  private sub = new Subscription();

  private all: DocumentationRequest[] = [];
  loading = true;
  error: string | null = null;
  previewOpen = false;
  previewGeneratedId: string | null = null;
  previewTitle = '';
  previewSubtitle: string | null = null;
  previewExportFileNameBase: string | null = null;

  constructor(
    private readonly nav: DocumentationNavigationService,
    private readonly hierarchy: DocumentationHierarchyDrillService,
    private readonly api: DocumentationApiService,
    private readonly identity: DocumentationIdentityService,
  ) {}

  ngOnInit(): void {
    this.sub.add(this.hierarchy.drill$.subscribe((d) => (this.drill = d)));
    this.sub.add(
      switchMapOnDocumentationContext(this.identity, () => this.api.getAllDocumentRequests()).subscribe({
        next: (rows) => {
          this.all = rows.map(mapDocumentRequestDto);
          this.loading = false;
          this.error = null;
        },
        error: () => {
          this.all = [];
          this.loading = false;
          this.error = 'Impossible de charger les documents.';
        },
      }),
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  docs(role: DocumentationRole): DocumentationDocument[] {
    const scopedReq = filterByEmployeeScope(
      this.all,
      role,
      this.identity.profile$.value,
      this.identity.directoryUsers$.value,
      role === 'Manager' || role === 'RP' ? this.drill : undefined,
    );
    return scopedReq
      .map((r) => mapRequestToGeneratedDocument(r))
      .filter((d): d is DocumentationDocument => d !== null);
  }

  initials(name?: string): string {
    return (name ?? '')
      .split(' ')
      .map((n) => n[0])
      .join('');
  }

  canOpenOrDownload(doc: DocumentationDocument): boolean {
    return !!(doc.generatedDocumentId?.trim() || doc.status === 'Generated');
  }

  openPreview(doc: DocumentationDocument): void {
    const id = doc.generatedDocumentId?.trim();
    if (!id) return;
    this.previewGeneratedId = id;
    this.previewTitle = doc.name?.trim() || 'Document généré';
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
