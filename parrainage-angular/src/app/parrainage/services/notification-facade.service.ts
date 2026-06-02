import { Injectable, inject } from '@angular/core';
import type { RoleFilter } from '../models/referral.model';
import { ReferralService } from './referral.service';

@Injectable({ providedIn: 'root' })
export class NotificationFacadeService {
  private readonly referrals = inject(ReferralService);

  getUnreadCount(role: RoleFilter, user: { id: string; projectId?: string }): number {
    return this.referrals.getNotificationsForRole(role, user).filter((n) => !n.read).length;
  }
}
