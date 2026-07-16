import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Download, Eye } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { PrimeEmployeeFichePreviewModalComponent } from './prime-employee-fiche-preview-modal.component';
import {
  PrimeEmployeeFichePreviewService,
  previewHttpError,
} from '../services/prime-employee-fiche-preview.service';

@Component({
  selector: 'app-prime-employee-fiche-preview-actions',
  standalone: true,
  imports: [LucideIconComponent, PrimeEmployeeFichePreviewModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="inline-flex flex-nowrap items-center gap-1">
      <button
        type="button"
        [title]="disabledHint() || 'Aperçu fiche PRIME fusionnée'"
        [disabled]="disabled() || !ficheId()?.trim()"
        (click)="openPreview($event)"
        class="p-1.5 text-muted hover:text-blue-400 hover:bg-input rounded-md transition-colors border border-transparent hover:border-blue-500/40 disabled:opacity-30 disabled:pointer-events-none"
      >
        <app-lucide-icon [icon]="icons.eye" className="w-4 h-4" />
      </button>
      <button
        type="button"
        [title]="disabledHint() || 'Télécharger fiche PRIME (.xlsx)'"
        [disabled]="disabled() || !ficheId()?.trim() || downloadBusy()"
        (click)="downloadDirect($event)"
        class="p-1.5 text-muted hover:text-emerald-400 hover:bg-input rounded-md transition-colors border border-transparent hover:border-emerald-500/40 disabled:opacity-30 disabled:pointer-events-none"
      >
        <app-lucide-icon [icon]="icons.download" className="w-4 h-4" />
      </button>
    </div>

    <app-prime-employee-fiche-preview-modal
      [open]="previewOpen()"
      [ficheId]="ficheId()"
      [title]="modalTitle()"
      [subtitle]="period() ? 'Période ' + period() : null"
      [fileNameBase]="fileNameBase()"
      (closed)="previewOpen.set(false)"
    />
  `,
})
export class PrimeEmployeeFichePreviewActionsComponent {
  private readonly previewSvc = inject(PrimeEmployeeFichePreviewService);

  readonly ficheId = input<string | null>(null);
  readonly employeeLabel = input('');
  readonly period = input('');
  readonly disabled = input(false);
  readonly disabledHint = input('');

  readonly icons = {
    eye: Eye,
    download: Download,
  };

  readonly previewOpen = signal(false);
  readonly downloadBusy = signal(false);

  modalTitle(): string {
    const label = (this.employeeLabel() ?? '').trim();
    const per = (this.period() ?? '').trim();
    if (label && per) return `Aperçu — ${label} — ${per}`;
    if (label) return `Aperçu — ${label}`;
    return 'Aperçu fiche PRIME';
  }

  fileNameBase(): string {
    const label = (this.employeeLabel() ?? '').trim().replace(/\s+/g, '_');
    const per = (this.period() ?? '').trim();
    return [label, per].filter(Boolean).join('_') || 'fiche';
  }

  openPreview(ev: Event): void {
    ev.stopPropagation();
    if (this.disabled() || !(this.ficheId() ?? '').trim()) return;
    this.previewOpen.set(true);
  }

  downloadDirect(ev: Event): void {
    ev.stopPropagation();
    const id = (this.ficheId() ?? '').trim();
    if (this.disabled() || !id || this.downloadBusy()) return;
    this.downloadBusy.set(true);
    this.previewSvc.loadContext(id).subscribe({
      next: (ctx) => {
        void this.previewSvc
          .downloadXlsxFromContext(ctx, this.fileNameBase())
          .then((err) => {
            if (err) window.alert(err);
            this.downloadBusy.set(false);
          })
          .catch((e: unknown) => {
            window.alert(previewHttpError(e));
            this.downloadBusy.set(false);
          });
      },
      error: (e) => {
        window.alert(previewHttpError(e));
        this.downloadBusy.set(false);
      },
    });
  }
}
