import { Injectable, inject } from '@angular/core';
import { KyntusSessionService } from '../session/kyntus-session.service';
import { NotificationService } from '../services/notification.service';

const MANAGER_ROLES = new Set(['manager', 'rh', 'admin', 'rp', 'pilote', 'coach']);

@Injectable({ providedIn: 'root' })
export class KyntusNotificationInitService {
  private readonly session = inject(KyntusSessionService);
  private readonly planningNotif = inject(NotificationService);
  private connected = false;

  connectIfAuthenticated(): void {
    if (this.connected || !this.session.isAuthenticated()) return;
    const userId = this.session.getAuthUserId();
    if (!userId) return;

    const role = (this.session.getRole() || '').toLowerCase().trim();
    if (MANAGER_ROLES.has(role)) {
      this.planningNotif.connectAsManager(userId);
    } else {
      this.planningNotif.connect(userId);
    }
    this.connected = true;
  }

  disconnect(): void {
    if (!this.connected) return;
    this.planningNotif.disconnect();
    this.connected = false;
  }
}

export function kyntusNotificationInitFactory(init: KyntusNotificationInitService): () => void {
  return () => init.connectIfAuthenticated();
}
