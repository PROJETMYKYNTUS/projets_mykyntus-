import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import type { KyntusDrillLevel } from './kyntus-org-drill-bar.model';

@Component({
  selector: 'app-kyntus-org-drill-bar',
  standalone: true,
  template: `
    @if (levels.length > 0) {
      <div class="kyntus-drill-bar">
        <span class="kyntus-drill-label">Périmètre</span>
        @for (level of levels; track level.key) {
          <select
            class="kyntus-drill-select"
            [value]="level.value"
            [disabled]="level.disabled"
            (change)="onChange(level.key, $any($event.target).value)"
          >
            <option value="">{{ level.placeholder }}</option>
            @for (opt of level.options; track opt.value) {
              <option [value]="opt.value">{{ opt.label }}</option>
            }
          </select>
        }
      </div>
    }
  `,
  styles: [`
    .kyntus-drill-bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1rem;
    }
    .kyntus-drill-label {
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--text-muted, #94a3b8);
    }
    .kyntus-drill-select {
      font-size: 0.875rem;
      padding: 0.5rem 0.75rem;
      border-radius: 0.5rem;
      border: 1px solid var(--border-default, #334155);
      background: var(--bg-input, #0f172a);
      color: var(--text-primary, #f1f5f9);
      min-width: 8rem;
    }
    .kyntus-drill-select:focus {
      outline: none;
      border-color: var(--electric-blue, #3b82f6);
      box-shadow: 0 0 0 2px color-mix(in srgb, var(--electric-blue, #3b82f6) 25%, transparent);
    }
    .kyntus-drill-select:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusOrgDrillBarComponent {
  @Input({ required: true }) levels: KyntusDrillLevel[] = [];
  @Output() levelChange = new EventEmitter<{ key: string; value: string }>();

  onChange(key: string, value: string): void {
    this.levelChange.emit({ key, value });
  }
}
