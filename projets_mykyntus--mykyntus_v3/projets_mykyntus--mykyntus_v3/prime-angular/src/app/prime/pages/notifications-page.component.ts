import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
import {
  NotificationCenterComponent,
  mapPrimeNotification,
} from '../components/notifications/notification-center.component';
import { NotificationUiService } from '../state/notification-ui.service';
import { I18nService } from '../state/i18n.service';

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [NotificationCenterComponent],
  template: `
    <app-notification-center
      [items]="items()"
      (markAllRead)="onMarkAllRead()"
      (markRead)="onMarkRead($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsPageComponent {
  readonly notificationUi = inject(NotificationUiService);
  readonly i18n = inject(I18nService);

  readonly items = computed(() =>
    this.notificationUi.notifications().map((n) =>
      mapPrimeNotification({
        ...n,
        label: this.i18n.t(`notifications.${n.type}`),
      }),
    ),
  );

  onMarkAllRead(): void {
    this.notificationUi.markAllAsRead();
  }

  onMarkRead(id: number): void {
    this.notificationUi.markAsRead(id);
  }
}
