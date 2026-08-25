import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
  TemplateRef,
} from '@angular/core';
import { ChevronDown, ChevronRight } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

export interface PrimeOrgTreeServiceNode {
  id: string;
  name: string;
}

export interface PrimeOrgTreeCelluleNode {
  id: string;
  name: string;
  services: PrimeOrgTreeServiceNode[];
}

@Component({
  selector: 'app-prime-org-tree',
  standalone: true,
  imports: [LucideIconComponent, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pot-scroll rounded-lg border border-default max-h-[min(75vh,42rem)] overflow-y-auto">
      <div class="pot p-2">
        @for (c of cellules(); track c.id) {
          <div class="pot-block">
            <div class="pot-row-wrap">
              <button
                type="button"
                class="pot-row"
                [class.is-selected]="isCelluleSelected(c.id)"
                (click)="celluleSelect.emit(c.id)"
              >
                <span
                  class="pot-chev"
                  role="button"
                  tabindex="0"
                  (click)="onChevronClick($event, c.id)"
                  (keydown.enter)="onChevronClick($event, c.id)"
                >
                  <app-lucide-icon
                    [icon]="isExpanded(c.id) ? icons.chevronDown : icons.chevronRight"
                    className="w-3.5 h-3.5 text-muted"
                  />
                </span>
                <span class="pot-label text-sm font-medium text-primary">
                  <span class="pot-badge pot-badge--cell">CELL.</span>
                  <span class="pot-name" [title]="c.name">{{ c.name }}</span>
                </span>
                @if (celluleTrailing(); as tpl) {
                  <ng-container *ngTemplateOutlet="tpl; context: { $implicit: c }" />
                }
              </button>
            </div>
            @if (isExpanded(c.id)) {
              <div class="pot-children">
                @for (s of c.services; track s.id) {
                  <button
                    type="button"
                    class="pot-row pot-row--svc"
                    [class.is-selected]="isServiceSelected(s.id)"
                    (click)="serviceSelect.emit({ celluleId: c.id, serviceId: s.id })"
                  >
                    <span class="pot-chev" aria-hidden="true"></span>
                    <span class="pot-label text-sm text-muted">
                      <span class="pot-badge pot-badge--svc">svc.</span>
                      <span class="pot-name text-primary" [title]="s.name">{{ s.name }}</span>
                    </span>
                    @if (serviceTrailing(); as tpl) {
                      <ng-container
                        *ngTemplateOutlet="tpl; context: { $implicit: s, cellule: c }"
                      />
                    }
                  </button>
                }
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
  styles: `
    .pot-scroll {
      background: color-mix(in srgb, var(--bg-input) 40%, transparent);
    }
    .pot {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .pot-block {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
    }
    .pot-children {
      margin-left: 0.5rem;
      padding-left: 0.55rem;
      border-left: 1px solid color-mix(in srgb, var(--border-color) 70%, transparent);
      display: flex;
      flex-direction: column;
      gap: 0.1rem;
    }
    .pot-row-wrap {
      display: flex;
      width: 100%;
    }
    .pot-row {
      display: grid;
      grid-template-columns: 1.25rem minmax(0, 1fr) auto;
      column-gap: 0.4rem;
      align-items: start;
      width: 100%;
      text-align: left;
      padding: 0.45rem 0.5rem;
      border-radius: 0.5rem;
      border: 1px solid transparent;
      background: transparent;
      cursor: pointer;
      transition: background 0.12s ease, border-color 0.12s ease;
    }
    .pot-row:hover {
      background: color-mix(in srgb, var(--bg-input) 70%, transparent);
    }
    .pot-row.is-selected {
      border-color: var(--info-border);
      background: var(--info-bg);
    }
    .pot-row--svc {
      font-size: 0.8125rem;
    }
    .pot-chev {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 1rem;
      margin-top: 0.15rem;
    }
    .pot-label {
      min-width: 0;
      display: flex;
      align-items: flex-start;
      gap: 0.35rem;
    }
    .pot-name {
      min-width: 0;
      flex: 1 1 auto;
      white-space: normal;
      overflow: visible;
      text-overflow: unset;
      word-break: break-word;
      line-height: 1.35;
    }
    .pot-badge {
      flex-shrink: 0;
      border-radius: 0.25rem;
      padding: 0.1rem 0.3rem;
      font-size: 0.5625rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      line-height: 1.4;
    }
    .pot-badge--cell {
      background: var(--success-bg);
      color: var(--success-text);
      box-shadow: inset 0 0 0 1px var(--success-border);
    }
    .pot-badge--svc {
      background: color-mix(in srgb, var(--soft-blue) 22%, transparent);
      color: var(--info-text);
      box-shadow: inset 0 0 0 1px var(--info-border);
    }
  `,
})
export class PrimeOrgTreeComponent {
  readonly cellules = input.required<readonly PrimeOrgTreeCelluleNode[]>();
  readonly expandedIds = input<ReadonlySet<string>>(new Set());
  readonly selectedCelluleId = input('');
  readonly selectedServiceId = input('');
  /** When `'cellule'`, only cellule selection highlights; when `'service'`, only service. */
  readonly selectionMode = input<'cellule' | 'service' | 'both'>('both');
  readonly celluleTrailing = input<TemplateRef<{ $implicit: PrimeOrgTreeCelluleNode }> | null>(null);
  readonly serviceTrailing =
    input<TemplateRef<{ $implicit: PrimeOrgTreeServiceNode; cellule: PrimeOrgTreeCelluleNode }> | null>(null);

  readonly celluleSelect = output<string>();
  readonly serviceSelect = output<{ celluleId: string; serviceId: string }>();
  readonly toggleExpand = output<string>();

  readonly icons = {
    chevronDown: ChevronDown,
    chevronRight: ChevronRight,
  };

  isExpanded(celluleId: string): boolean {
    return this.expandedIds().has(celluleId);
  }

  isCelluleSelected(celluleId: string): boolean {
    const mode = this.selectionMode();
    if (mode === 'service') return false;
    return this.selectedCelluleId().trim() === celluleId.trim();
  }

  isServiceSelected(serviceId: string): boolean {
    const mode = this.selectionMode();
    if (mode === 'cellule') return false;
    return this.selectedServiceId().trim() === serviceId.trim();
  }

  onChevronClick(ev: Event, celluleId: string): void {
    ev.stopPropagation();
    ev.preventDefault();
    this.toggleExpand.emit(celluleId);
  }
}
