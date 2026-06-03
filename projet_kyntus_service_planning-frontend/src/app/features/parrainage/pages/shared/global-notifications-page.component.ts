import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NotificationCenterComponent, NotificationCenterItem, mapReferralNotificationToCenter } from '../../components/notifications/notification-center.component';
import { ReferralService } from '../../services/referral.service';
import { ParrainageRoleService } from '../../state/parrainage-role.service';
import type { NotificationPreferences, ReferralNotification } from '../../models/referral.model';

function applyPrefs(list: ReferralNotification[], prefs: NotificationPreferences): ReferralNotification[] {
  return list.filter((n) => {
    if (n.type === 'NEW_REFERRAL' && prefs.referrals === false) return false;
    if (n.type === 'STATUS_CHANGED' && prefs.approvals === false) return false;
    if (n.type === 'REFERRAL_REWARDED' && prefs.payments === false) return false;
    return true;
  });
}

@Component({
  selector: 'app-global-notifications-page',
  standalone: true,
  imports: [NotificationCenterComponent],
  template: `
    <app-notification-center
      title="Notifications"
      unreadLabel="Non lues"
      markAllLabel="Tout marquer comme lu"
      emptyTitle="Aucune notification"
      emptyDescription="Aucune notification pour ce périmètre."
      [items]="items()"
      (markAllRead)="markAll()"
      (markRead)="markRead($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GlobalNotificationsPageComponent {
  private readonly referrals = inject(ReferralService);
  private readonly role = inject(ParrainageRoleService);
  private readonly tick = signal(0);

  readonly items = computed<NotificationCenterItem[]>(() => {
    this.tick();
    const u = this.role.user();
    const prefs = this.referrals.getNotificationPreferences();
    const raw = this.referrals.getNotificationsForRole(u.role, { id: u.id, projectId: u.projectId });
    return applyPrefs(raw, prefs).map(mapReferralNotificationToCenter);
  });

  markRead(id: string): void {
    void this.referrals.markNotificationAsRead(id);
    this.tick.update((n) => n + 1);
  }

  markAll(): void {
    void this.referrals.markAllNotificationsAsRead();
    this.tick.update((n) => n + 1);
  }
}
