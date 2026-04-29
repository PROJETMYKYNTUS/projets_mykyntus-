import { CommonModule } from '@angular/common';
import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { Component, ElementRef, HostListener, Input, OnDestroy, ViewChild } from '@angular/core';
import { Subscription } from 'rxjs';

import { DocumentationDataApiService } from '../../../core/services/documentation-data-api.service';
import { DocumentationNotificationService } from '../../../core/services/documentation-notification.service';
import {
  formatDocumentationHttpError,
  triggerDownloadFromHttpResponse,
} from '../../lib/documentation-download.util';
export type GeneratedExportFormat = 'pdf' | 'docx';

@Component({
  standalone: true,
  selector: 'app-generated-document-format-menu',
  imports: [CommonModule],
  templateUrl: './generated-document-format-menu.component.html',
  host: {
    class: 'doc-format-menu-host',
  },
})
export class GeneratedDocumentFormatMenuComponent implements OnDestroy {
  @ViewChild('triggerHost', { read: ElementRef }) triggerHost?: ElementRef<HTMLElement>;
  @ViewChild('dropdownPanel', { read: ElementRef }) dropdownPanel?: ElementRef<HTMLElement>;

  /** Document généré cible (export multi-format). */
  @Input() generatedDocumentId: string | null = null;
  /** Base de nom de fichier sans extension (si l’API n’envoie pas Content-Disposition). */
  @Input() fileNameBase: string | null = null;
  /**
   * <c>button</c> / <c>standard</c> : libellé « ⬇ Télécharger » (<c>.btn-download</c>).
   * <c>icon</c> : bouton compact même style (liste étroite).
   */
  @Input() appearance: 'button' | 'icon' | 'standard' = 'button';
  menuOpen = false;
  busyFormat: GeneratedExportFormat | null = null;
  /** Position du panneau (fixed) pour éviter la coupure par overflow des tableaux / cartes. */
  dropdownTop = 0;
  dropdownLeft = 0;
  readonly dropdownPanelWidth = 260;
  dropdownAppliedWidth = 260;
  private sub = new Subscription();

  constructor(
    private readonly data: DocumentationDataApiService,
    private readonly notify: DocumentationNotificationService,
  ) {}

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(ev: MouseEvent): void {
    const t = ev.target as Node | null;
    if (this.triggerHost?.nativeElement?.contains(t)) return;
    if (this.dropdownPanel?.nativeElement?.contains(t)) return;
    this.menuOpen = false;
  }

  @HostListener('window:resize')
  @HostListener('window:scroll', ['$event'])
  onViewportChange(): void {
    if (this.menuOpen) this.updateDropdownPosition();
  }

  toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    if (!this.generatedDocumentId?.trim() || this.busyFormat) return;
    this.menuOpen = !this.menuOpen;
    if (this.menuOpen) {
      queueMicrotask(() => this.updateDropdownPosition());
      setTimeout(() => this.updateDropdownPosition(), 0);
    }
  }

  private updateDropdownPosition(): void {
    const el = this.triggerHost?.nativeElement;
    if (!el || typeof window === 'undefined') return;
    const r = el.getBoundingClientRect();
    this.dropdownAppliedWidth = Math.min(this.dropdownPanelWidth, Math.max(240, window.innerWidth - 16));
    const menuWidth = this.dropdownAppliedWidth;
    let left = r.right - menuWidth;
    left = Math.max(8, Math.min(left, window.innerWidth - menuWidth - 8));
    const gap = 8;
    this.dropdownTop = r.bottom + gap;
    this.dropdownLeft = left;
    const panel = this.dropdownPanel?.nativeElement;
    if (panel) {
      const h = panel.offsetHeight || 200;
      if (this.dropdownTop + h > window.innerHeight - 8) {
        this.dropdownTop = Math.max(8, r.top - gap - h);
      }
    }
  }

  downloadAs(format: GeneratedExportFormat, event: MouseEvent): void {
    event.stopPropagation();
    const id = this.generatedDocumentId?.trim();
    if (!id || this.busyFormat) return;
    this.busyFormat = format;
    this.menuOpen = false;
    const ext = format === 'pdf' ? 'pdf' : 'docx';
    const base = this.fileNameBase?.trim();
    const fallback =
      base && base.length > 0
        ? `${base.replace(/[/\\?%*:|"<>]/g, '-').replace(/\s+/g, '_')}.${ext}`
        : `document.${ext}`;
    this.sub.add(
      this.data.exportGeneratedDocument(id, format).subscribe({
        next: (resp: HttpResponse<Blob>) => {
          this.busyFormat = null;
          triggerDownloadFromHttpResponse(resp, fallback);
          this.notify.showSuccess('Téléchargement démarré.');
        },
        error: (e: unknown) => {
          this.busyFormat = null;
          const id = this.generatedDocumentId?.trim();
          if (format === 'pdf' && e instanceof HttpErrorResponse && e.status === 422 && id) {
            this.notify.showSuccess('Le PDF générique n’est pas disponible : lancement du téléchargement Word (même mise en page que le modèle).');
            this.busyFormat = 'docx';
            this.sub.add(
              this.data.exportGeneratedDocument(id, 'docx').subscribe({
                next: (resp: HttpResponse<Blob>) => {
                  this.busyFormat = null;
                  const fbDocx =
                    base && base.length > 0
                      ? `${base.replace(/[/\\?%*:|"<>]/g, '-').replace(/\s+/g, '_')}.docx`
                      : 'document.docx';
                  triggerDownloadFromHttpResponse(resp, fbDocx);
                  this.notify.showSuccess('Téléchargement démarré.');
                },
                error: (e2: unknown) => {
                  this.busyFormat = null;
                  void formatDocumentationHttpError(e2).then((m) => this.notify.showError(m));
                },
              }),
            );
            return;
          }
          void formatDocumentationHttpError(e).then((m) => this.notify.showError(m));
        },
      }),
    );
  }
}
