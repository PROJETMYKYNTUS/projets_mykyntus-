import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { DocumentationIdentityService } from '../../core/services/documentation-identity.service';
import type { DocumentRequestDto } from '../../shared/models/api.models';
import { switchMapOnDocumentationContext } from '../lib/documentation-context-refresh';
import { generatedDocumentDisplayLabel, generatedDocumentExportBaseName } from '../lib/documentation-dto-mappers';
import {
  beneficiaryDisplayNameFromEmployeeName,
  cleanDocumentRequestTypeLabel,
} from '../lib/documentation-request-labels';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';
import { GeneratedDocumentFormatMenuComponent } from '../components/generated-document-format-menu/generated-document-format-menu.component';
import { GeneratedDocumentPreviewModalComponent } from '../components/generated-document-preview-modal/generated-document-preview-modal.component';
import { DocumentationApiService } from '../services/documentation-api.service';
import { StatusBadgeComponent } from '../components/status-badge/status-badge.component';

@Component({
  standalone: true,
  selector: 'app-hr-generated-history-page',
  imports: [
    CommonModule,
    FormsModule,
    StatusBadgeComponent,
    DocIconComponent,
    GeneratedDocumentFormatMenuComponent,
    GeneratedDocumentPreviewModalComponent,
  ],
  templateUrl: './hr-generated-history-page.component.html',
})
export class HrGeneratedHistoryPageComponent implements OnInit, OnDestroy {
  readonly cleanDocumentRequestTypeLabel = cleanDocumentRequestTypeLabel;

  rows: DocumentRequestDto[] = [];
  filtered: DocumentRequestDto[] = [];
  loading = true;
  error: string | null = null;

  searchQuery = '';
  dateFrom = '';
  dateTo = '';
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
    this.reload();
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  reload(): void {
    this.loading = true;
    this.error = null;
    this.sub.add(
      switchMapOnDocumentationContext(this.identity, () =>
        this.api.getAllDocumentRequests({ status: 'generated', sortBy: 'createdAt', sortOrder: 'desc' }),
      ).subscribe({
        next: (rows) => {
          this.rows = rows;
          this.applyFilters();
          this.loading = false;
        },
        error: () => {
          this.rows = [];
          this.filtered = [];
          this.loading = false;
          this.error = 'Impossible de charger l’historique des documents générés.';
        },
      }),
    );
  }

  onFilterChange(): void {
    this.applyFilters();
  }

  applyFilters(): void {
    const q = this.searchQuery.trim().toLowerCase();
    let list = this.rows.slice();

    if (q) {
      list = list.filter((r) => {
        const rawName = (r.employeeName ?? '').toLowerCase();
        const beneficiary = beneficiaryDisplayNameFromEmployeeName(r.employeeName, '').toLowerCase();
        const typeHaystack = cleanDocumentRequestTypeLabel(r.type).toLowerCase();
        return (
          r.internalId.toLowerCase().includes(q) ||
          r.id.toLowerCase().includes(q) ||
          rawName.includes(q) ||
          beneficiary.includes(q) ||
          typeHaystack.includes(q) ||
          (r.type ?? '').toLowerCase().includes(q)
        );
      });
    }

    const from = this.parseDay(this.dateFrom);
    const to = this.parseDay(this.dateTo, true);
    if (from) {
      list = list.filter((r) => {
        const g = r.generatedAt ? new Date(r.generatedAt) : null;
        return g && !Number.isNaN(g.getTime()) && g >= from;
      });
    }
    if (to) {
      list = list.filter((r) => {
        const g = r.generatedAt ? new Date(r.generatedAt) : null;
        return g && !Number.isNaN(g.getTime()) && g <= to;
      });
    }

    list.sort((a, b) => (b.generatedAt ?? '').localeCompare(a.generatedAt ?? ''));
    this.filtered = list;
  }

  private parseDay(iso: string, endOfDay = false): Date | null {
    const s = iso?.trim();
    if (!s) return null;
    const d = new Date(s + (s.length === 10 ? 'T00:00:00' : ''));
    if (Number.isNaN(d.getTime())) return null;
    if (endOfDay && s.length === 10) {
      d.setHours(23, 59, 59, 999);
    }
    return d;
  }

  beneficiaryLabel(r: DocumentRequestDto): string {
    return beneficiaryDisplayNameFromEmployeeName(r.employeeName);
  }

  formatGeneratedAt(iso: string | null | undefined): string {
    if (!iso?.trim()) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso;
    return d.toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
  }

  canDownload(r: DocumentRequestDto): boolean {
    return !!(r.generatedDocumentId?.trim() && r.status?.toLowerCase() === 'generated');
  }

  openPreview(r: DocumentRequestDto): void {
    const id = r.generatedDocumentId?.trim();
    if (!id) return;
    this.previewGeneratedId = id;
    const emp = beneficiaryDisplayNameFromEmployeeName(r.employeeName);
    this.previewTitle = generatedDocumentDisplayLabel({
      employeeName: emp,
      type: r.type,
      generatedAt: r.generatedAt,
      requestDate: r.requestDate,
    });
    this.previewSubtitle = r.id;
    this.previewExportFileNameBase = generatedDocumentExportBaseName({
      employeeName: emp,
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

  exportFileNameHint(r: DocumentRequestDto): string | null {
    if (!r.generatedDocumentId?.trim()) return null;
    return generatedDocumentExportBaseName({
      employeeName: beneficiaryDisplayNameFromEmployeeName(r.employeeName),
      type: r.type,
      generatedAt: r.generatedAt,
      requestDate: r.requestDate,
    });
  }

  openNativeDatePicker(input: HTMLInputElement, ev?: Event): void {
    ev?.preventDefault();
    ev?.stopPropagation();
    const w = input as unknown as { showPicker?: () => void | Promise<unknown> };
    const r = w.showPicker?.();
    if (r && typeof (r as Promise<unknown>).then === 'function') {
      void (r as Promise<unknown>).catch(() => input.focus());
    } else {
      input.focus();
    }
  }
}
