import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { KyntusPageHeaderComponent } from './kyntus-page-header.component';

@Component({
  selector: 'app-kyntus-employee-inbox',
  standalone: true,
  imports: [KyntusPageHeaderComponent],
  template: `
    <div class="kyntus-employee-inbox ky-page-shell">
      <app-kyntus-page-header [title]="title" [subtitle]="subtitle" [eyebrow]="eyebrow">
        @if (createLabel) {
          <div actions>
            <button type="button" class="kyntus-inbox-create ky-btn-primary" (click)="createClick.emit()">
              {{ createLabel }}
            </button>
          </div>
        }
        <ng-content select="[headerActions]" actions />
      </app-kyntus-page-header>

      <ng-content select="[kpis]" />

      <div class="kyntus-inbox-filters">
        <ng-content select="[filters]" />
      </div>

      <div class="kyntus-inbox-body">
        <ng-content />
      </div>
    </div>
  `,
  styles: [`
    .kyntus-employee-inbox {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .kyntus-inbox-filters:empty {
      display: none;
    }
    .kyntus-inbox-create {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusEmployeeInboxComponent {
  @Input({ required: true }) title!: string;
  @Input() subtitle = '';
  @Input() eyebrow = '';
  @Input() createLabel = '';
  @Output() createClick = new EventEmitter<void>();
}
