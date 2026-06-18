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
          {{ t.previewRows.length }} ligne(s) · {{ t.formulas.length }} formule(s) · aperçu style feuille Excel (valeurs
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
          class="excel-sheet-shell max-h-[min(62vh,640px)] overflow-auto rounded-md border border-[#a0a0a0] shadow-lg"
        >
          <table class="excel-sheet-table border-collapse text-left" style="font-family: Calibri, 'Segoe UI', system-ui, sans-serif">
            <thead>
              <tr>
                <th
                  class="excel-corner h-7 w-10 min-w-[2.5rem] border border-[#8a8a8a] bg-[#f3f3f3] p-0 text-center text-[10px] font-normal text-[#333]"
                ></th>
                @for (ci of columnIndices(); track ci) {
                  <th
                    class="excel-col-head h-7 min-w-[5.5rem] max-w-[14rem] border border-[#8a8a8a] bg-[#f3f3f3] px-1 py-0.5 text-center text-[11px] font-semibold tracking-wide text-[#333]"
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
                    class="excel-row-head w-10 min-w-[2.5rem] border border-[#d4d4d4] bg-[#f3f3f3] py-0.5 text-center text-[11px] font-normal tabular-nums text-[#333]"
                  >
                    {{ ri + 1 }}
                  </th>
                  @for (cell of row; track $index) {
                    <td
                      class="excel-cell max-w-[14rem] border border-[#d4d4d4] bg-white px-1.5 py-0.5 text-[11px] leading-snug text-[#111] align-top"
                      [attr.title]="cell.length > 80 ? cell : null"
                    >
                      <span class="block truncate">{{ cell }}</span>
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
      background: #fff;
    }
    .excel-sheet-table {
      table-layout: fixed;
    }
    .excel-cell:hover {
      outline: 2px solid #217346;
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
