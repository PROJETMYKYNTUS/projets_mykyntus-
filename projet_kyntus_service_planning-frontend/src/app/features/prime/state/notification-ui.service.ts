import { Injectable, computed, inject, signal } from '@angular/core';
import { PrimeNotificationService } from '../services/notification.service';
import type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';
import { KyntusShellUiService } from '../../../core/notifications/kyntus-shell-ui.service';

export type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationUiService {
  private readonly shellUi = inject(KyntusShellUiService);

  readonly notifications = signal<PrimeNotification[]>(PrimeNotificationService.seed());
  readonly dropdownOpen = signal(false);
  readonly settingsOpen = this.shellUi.settingsOpen;

  readonly unreadCount = computed(() =>
    PrimeNotificationService.unreadCount(this.notifications()),
  );

  push(type: PrimeNotificationType): void {
    this.notifications.update((prev) => PrimeNotificationService.push(prev, type));
  }

  markAllAsRead(): void {
    this.notifications.update((prev) => PrimeNotificationService.markAllAsRead(prev));
  }

  markAsRead(id: number): void {
    this.notifications.update((prev) => PrimeNotificationService.markAsRead(prev, id));
  }

  toggleDropdown(): void {
    this.shellUi.toggleDropdown();
  }

  openSettings(): void {
    this.shellUi.openSettings();
  }

  closeSettings(): void {
    this.shellUi.closeSettings();
  }
}
