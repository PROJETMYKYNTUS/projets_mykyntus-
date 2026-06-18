import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { KyntusEmptyStateComponent } from './kyntus-empty-state.component';
import { KyntusLoadingStateComponent } from './kyntus-loading-state.component';

export interface KyntusTableColumn {
  key: string;
  label: string;
}

@Component({
  selector: 'app-kyntus-data-table',
  standalone: true,
  imports: [KyntusLoadingStateComponent, KyntusEmptyStateComponent],
  template: `
    <div class="kyntus-data-table-wrap">
      @if (loading) {
        <app-kyntus-loading-state [message]="loadingMessage" />
      } @else {
        <div class="kyntus-table-scroll">
          <table class="kyntus-data-table">
            <thead>
              <tr>
                @for (col of columns; track col.key) {
                  <th scope="col">{{ col.label }}</th>
                }
              </tr>
            </thead>
            <tbody>
              <ng-content select="[rows]" />
              @if (!hasProjectedRows) {
                @for (row of rows; track trackRow(row)) {
                  <tr>
                    @for (col of columns; track col.key) {
                      <td>{{ cellValue(row, col.key) }}</td>
                    }
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
        @if (!loading && isEmpty) {
          <app-kyntus-empty-state [title]="emptyMessage" />
        }
      }
    </div>
  `,
  styles: [`
    .kyntus-data-table-wrap {
      border-radius: 0.75rem;
      border: 1px solid var(--border-default, #1e293b);
      background: var(--bg-card, #0f172a);
      overflow: hidden;
    }
    .kyntus-table-scroll {
      overflow-x: auto;
    }
    .kyntus-data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.8125rem;
    }
    .kyntus-data-table th {
      text-align: left;
      padding: 0.75rem 1rem;
      font-size: 0.6875rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--text-muted, #94a3b8);
      border-bottom: 1px solid var(--border-default, #1e293b);
      background: color-mix(in srgb, var(--bg-card, #0f172a) 80%, #1e293b);
    }
    .kyntus-data-table td {
      padding: 0.75rem 1rem;
      color: var(--text-primary, #f8fafc);
      border-bottom: 1px solid var(--border-default, #1e293b);
      vertical-align: top;
    }
    .kyntus-data-table tbody tr:last-child td {
      border-bottom: none;
    }
    .kyntus-data-table tbody tr:hover td {
      background: color-mix(in srgb, #3b82f6 4%, transparent);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusDataTableComponent {
  @Input({ required: true }) columns: KyntusTableColumn[] = [];
  @Input() rows: Record<string, unknown>[] = [];
  @Input() loading = false;
  @Input() loadingMessage = 'Chargement…';
  @Input() emptyMessage = 'Aucune donnée à afficher.';
  /** Set true when parent projects custom [rows] content. */
  @Input() hasProjectedRows = false;

  get isEmpty(): boolean {
    if (this.hasProjectedRows) return false;
    return this.rows.length === 0;
  }

  trackRow(row: Record<string, unknown>): string {
    const id = row['id'];
    return id != null ? String(id) : JSON.stringify(row);
  }

  cellValue(row: Record<string, unknown>, key: string): string {
    const v = row[key];
    return v == null ? '—' : String(v);
  }
}
