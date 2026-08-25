import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { StoredPrimeTemplate } from '../models/prime-template.model';
import { computePreviewGridWithFormulas } from '../lib/prime-fiche-formula-eval';

/** Libellé colonne Excel (0 → A, 25 → Z, 26 → AA). */
export function excelColumnLabel(index: number): string {
  let n = index;
  let label = '';
  while (n >= 0) {
    label = String.fromCharCode(65 + (n % 26)) + label;
    n = Math.floor(n / 26) - 1;
  }
  return label;
}

@Component({
  selector: 'app-prime-template-preview',
  standalone: true,
  template: `
    @if (tpl(); as t) {
      <div class="space-y-3">
        <p class="text-xs text-muted">
          Feuille : <span class="font-medium text-primary">{{ t.previewSheetName }}</span> ·
          {{ resolved().rows.length }} ligne(s) · {{ t.formulas.length }} formule(s) · aperçu style feuille Excel (valeurs
          recalculées lorsque possible)
        </p>
        @if (resolved().errors.length) {
          <div
            class="rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-[11px] text-amber-900 dark:text-amber-100"
          >
            @for (e of resolved().errors; track e) {
              <div>{{ e }}</div>
            }
          </div>
        }
        <div
          class="excel-sheet-shell max-h-[min(62vh,640px)] overflow-auto rounded-md shadow-lg"
        >
          <table class="excel-sheet-table border-collapse text-left" style="font-family: Calibri, 'Segoe UI', system-ui, sans-serif">
            <thead>
              <tr>
                <th
                  class="excel-corner h-7 w-10 min-w-[2.5rem] sticky left-0 z-[2] p-0 text-center text-[10px] font-normal"
                ></th>
                @for (ci of columnIndices(); track ci) {
                  <th
                    class="excel-col-head h-7 min-w-[4.5rem] px-1 py-0.5 text-center text-[11px] font-semibold tracking-wide"
                  >
                    {{ excelColumnLabel(ci) }}
                  </th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of resolved().rows; track $index; let ri = $index) {
                <tr>
                  <th
                    class="excel-row-head sticky left-0 z-[1] w-10 min-w-[2.5rem] py-0.5 text-center text-[11px] font-normal tabular-nums"
                  >
                    {{ ri + 1 }}
                  </th>
                  @for (cell of row; track $index) {
                    <td
                      class="excel-cell min-w-[4.5rem] px-1.5 py-0.5 text-[11px] leading-snug align-top whitespace-pre-wrap break-words"
                      [attr.title]="cell.length > 40 ? cell : null"
                    >
                      {{ cell }}
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    } @else {
      <p class="text-sm text-muted">Aucun template sélectionné.</p>
    }
  `,
  styles: `
    .excel-sheet-shell {
      background: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-md);
    }
    .excel-sheet-table {
      table-layout: auto;
    }
    .excel-corner,
    .excel-col-head,
    .excel-row-head {
      border: 1px solid var(--border-color);
      background: var(--bg-input);
      color: var(--text-primary);
    }
    .excel-cell {
      border: 1px solid var(--border-color);
      background: var(--bg-card);
      color: var(--text-primary);
    }
    .excel-cell:hover {
      outline: 2px solid var(--success);
      outline-offset: -1px;
      z-index: 1;
      position: relative;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeTemplatePreviewComponent {
  readonly tpl = input<StoredPrimeTemplate | null>(null);

  readonly excelColumnLabel = excelColumnLabel;

  readonly resolved = computed(() => {
    const t = this.tpl();
    if (!t) return { rows: [] as string[][], errors: [] as string[] };
    return computePreviewGridWithFormulas(t);
  });

  readonly columnIndices = computed(() => {
    const rows = this.resolved().rows;
    if (!rows.length) return [] as number[];
    const w = Math.max(...rows.map((r) => r.length), 0);
    return Array.from({ length: w }, (_, i) => i);
  });
}
