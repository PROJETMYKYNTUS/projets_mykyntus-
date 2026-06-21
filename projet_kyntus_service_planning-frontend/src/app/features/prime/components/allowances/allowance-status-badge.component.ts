import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { allowanceStatusBadgeClass, allowanceStatusLabel, type AllowanceStatusViewer } from '../../lib/allowance-status';

@Component({
  selector: 'app-allowance-status-badge',
  standalone: true,
  template: `
    <span
      class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
      [class]="badgeClass()"
    >
      {{ label() }}
    </span>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowanceStatusBadgeComponent {
  readonly status = input.required<string>();
  readonly viewer = input<AllowanceStatusViewer>('stakeholder');

  label(): string {
    return allowanceStatusLabel(this.status(), this.viewer());
  }

  badgeClass(): string {
    return allowanceStatusBadgeClass(this.status());
  }
}
