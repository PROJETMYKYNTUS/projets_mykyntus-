import { Injectable, computed, signal } from '@angular/core';
import { PrimeNotificationService } from '../services/notification.service';
import type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';

export type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationUiService {
  readonly notifications = signal<PrimeNotification[]>(PrimeNotificationService.seed());
  readonly dropdownOpen = signal(false);
  readonly settingsOpen = signal(false);

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
    this.dropdownOpen.update((o) => !o);
  }

  openSettings(): void {
    this.settingsOpen.set(true);
  }

  closeSettings(): void {
    this.settingsOpen.set(false);
  }
}
