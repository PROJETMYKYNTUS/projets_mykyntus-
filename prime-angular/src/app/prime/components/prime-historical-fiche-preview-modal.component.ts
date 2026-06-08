import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { Download } from 'lucide';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { downloadRawGridXlsx } from '../lib/prime-fiche-xlsx-export';
import { primeHttpErrorDetail } from '../lib/primeHttpErrorMessage';
import {
  PrimeCellPrimeApiService,
  type PrimeHistoricalFicheDetailSnapshotDto,
} from '../services/prime-cell-prime-api.service';
import { RoleService } from '../state/role.service';

@Component({
  selector: 'app-prime-historical-fiche-preview-modal',
  standalone: true,
  imports: [LucideIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="dialogTitleId"
        (click)="close()"
      >
        <div
          class="max-w-[min(96vw,1400px)] w-full max-h-[min(90vh,900px)] flex flex-col rounded-xl border border-navy-600 bg-navy-950 shadow-xl"
          (click)="$event.stopPropagation()"
        >
          <div class="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-b border-navy-700 shrink-0">
            <div class="min-w-0">
              <h3 [id]="dialogTitleId" class="text-sm font-semibold text-slate-100 truncate">
                {{ title() }}
              </h3>
              @if (subtitle()) {
                <p class="text-[11px] text-slate-500 truncate mt-0.5">{{ subtitle() }}</p>
              }
            </div>
            <div class="flex items-center gap-2 shrink-0">
              <button
                type="button"
                [disabled]="downloadBusy() || !canDownload()"
                (click)="download()"
                class="inline-flex items-center gap-1.5 rounded-lg border border-emerald-500/40 bg-emerald-600/20 px-3 py-1.5 text-xs font-medium text-emerald-200 hover:bg-emerald-600/30 disabled:opacity-40"
              >
                <app-lucide-icon [icon]="icons.download" className="w-3.5 h-3.5" />
                {{ downloadBusy() ? 'Export…' : 'Télécharger .xlsx' }}
              </button>
              <button
                type="button"
                (click)="close()"
                class="rounded-lg border border-navy-600 px-3 py-1.5 text-xs font-medium text-slate-200 hover:bg-navy-800"
              >
                Fermer
              </button>
            </div>
          </div>
          @if (busy()) {
            <div class="flex justify-center py-16 shrink-0">
              <div class="animate-spin rounded-full h-10 w-10 border-2 border-blue-500 border-t-transparent"></div>
            </div>
          } @else {
            @if (banner()) {
              <div
                class="mx-4 mt-3 rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-100 shrink-0"
                role="status"
              >
                {{ banner() }}
              </div>
            }
            @if (errors().length) {
              <div
                class="mx-4 mt-2 rounded-lg border border-rose-500/40 bg-rose-500/10 px-3 py-2 text-xs text-rose-100 max-h-28 overflow-y-auto shrink-0 space-y-0.5"
                role="status"
              >
                @for (er of errors(); track er) {
                  <p>{{ er }}</p>
                }
              </div>
            }
            <div class="flex-1 min-h-0 overflow-auto p-3">
              <table class="text-[11px] border-collapse border border-navy-700 text-slate-200">
                @for (row of rows(); track ri; let ri = $index) {
                  <tr>
                    @for (cell of row; track ci; let ci = $index) {
                      <td class="border border-navy-800 px-1 py-0.5 whitespace-nowrap align-top">{{ cell }}</td>
                    }
                  </tr>
                }
              </table>
            </div>
          }
        </div>
      </div>
    }
  `,
})
export class PrimeHistoricalFichePreviewModalComponent {
  private readonly api = inject(PrimeCellPrimeApiService);
  private readonly role = inject(RoleService);

  readonly open = input(false);
  readonly historicalFicheId = input<string | null>(null);
  readonly title = input('Aperçu fiche historique');
  readonly subtitle = input<string | null>(null);
  readonly fileNameBase = input<string | null>(null);

  readonly closed = output<void>();

  readonly dialogTitleId = 'prime-historical-fiche-preview-modal-title';
  readonly icons = { download: Download };

  readonly busy = signal(false);
  readonly downloadBusy = signal(false);
  readonly rows = signal<string[][]>([]);
  readonly errors = signal<string[]>([]);
  readonly banner = signal<string | null>(null);
  readonly canDownload = signal(false);

  private loadedSnapshot: PrimeHistoricalFicheDetailSnapshotDto | null = null;

  constructor() {
    effect(() => {
      const isOpen = this.open();
      const id = (this.historicalFicheId() ?? '').trim();
      if (isOpen && id) this.load(id);
      if (!isOpen) this.reset();
    });
  }

  close(): void {
    this.closed.emit();
  }

  download(): void {
    if (!this.loadedSnapshot || this.downloadBusy()) return;
    this.downloadBusy.set(true);
    try {
      const snap = this.loadedSnapshot;
      const rows = snap.rows ?? [];
      if (!rows.length) {
        window.alert('Export impossible — grille vide.');
        return;
      }
      const sheetName = snap.previewSheetName ?? 'Fiche_PRIME';
      const safe =
        (this.fileNameBase() ?? `${snap.employeeExternalName}_${snap.period}`)
          .replace(/[<>:"/\\|?*]+/g, '_')
          .trim() || 'fiche';
      const origin = (snap.originFileName ?? '').trim();
      const fileName = origin.toLowerCase().endsWith('.xlsx')
        ? origin.replace(/[<>:"/\\|?*]+/g, '_')
        : `PRIME_fiche_${safe}.xlsx`;
      downloadRawGridXlsx(rows, sheetName, fileName);
    } finally {
      this.downloadBusy.set(false);
    }
  }

  private load(historicalFicheId: string): void {
    this.busy.set(true);
    this.rows.set([]);
    this.errors.set([]);
    this.banner.set(null);
    this.canDownload.set(false);
    this.loadedSnapshot = null;

    const u = this.role.currentUser();
    const role = this.role.currentRole() as string;
    this.api.getHistoricalFicheDetailSnapshot(historicalFicheId, u.id, role).subscribe({
      next: (snap) => {
        this.loadedSnapshot = snap;
        const rows = snap.rows ?? [];
        this.rows.set(rows);
        this.errors.set(snap.errors ?? []);
        if (!rows.length) {
          this.banner.set('Aucune donnée à afficher.');
        }
        this.canDownload.set(rows.length > 0);
        this.busy.set(false);
      },
      error: (e) => {
        this.banner.set(primeHttpErrorDetail(e) ?? 'Impossible de charger la fiche.');
        this.busy.set(false);
      },
    });
  }

  private reset(): void {
    this.busy.set(false);
    this.downloadBusy.set(false);
    this.rows.set([]);
    this.errors.set([]);
    this.banner.set(null);
    this.canDownload.set(false);
    this.loadedSnapshot = null;
  }
}
