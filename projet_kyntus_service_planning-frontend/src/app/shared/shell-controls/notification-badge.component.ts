import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-shell-notification-badge',
  standalone: true,
  template: `
    @if (count > 0) {
      <span
        class="ks-notif-badge"
      >
        {{ count > 99 ? '99+' : count }}
      </span>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellNotificationBadgeComponent {
  @Input({ required: true }) count!: number;
}
