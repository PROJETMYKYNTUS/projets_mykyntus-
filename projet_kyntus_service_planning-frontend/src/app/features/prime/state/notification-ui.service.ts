import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { PrimeNotificationService } from '../services/notification.service';
import type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';
import { KyntusShellUiService } from '../../../core/notifications/kyntus-shell-ui.service';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { mapJwtRoleToPrimeRole } from '../../../core/session/kyntus-role-ui.config';
import { PrimeAdminService, type AnomalyDto } from '../services/prime-admin.service';

export type { PrimeNotification, PrimeNotificationType } from '../models/notification.model';

export interface PrimeApiNotification {
  id: string;
  title: string;
  body: string;
  createdAt: Date;
  read: boolean;
  severity: 'info' | 'warning';
  adminSection: 'anomalies';
}

/** Admin anomalies API — do not call for roles without access (avoids noisy 401s). */
const ANOMALY_API_ROLES = new Set(['Admin']);

@Injectable({ providedIn: 'root' })
export class NotificationUiService {
  private readonly shellUi = inject(KyntusShellUiService);
  private readonly session = inject(KyntusSessionService);
  private readonly primeAdmin = inject(PrimeAdminService);

  /** Notifications client (localStorage) — événements UI poussés localement. */
  readonly localNotifications = signal<PrimeNotification[]>(PrimeNotificationService.load());

  /** Notifications métier issues de l’API Prime (anomalies ouvertes). */
  readonly apiNotifications = signal<PrimeApiNotification[]>([]);

  /** Compat : signal historique consommé par le hub pour les push locaux. */
  readonly notifications = this.localNotifications;

  readonly dropdownOpen = this.shellUi.dropdownOpen;
  readonly settingsOpen = this.shellUi.settingsOpen;

  readonly unreadCount = computed(() =>
    PrimeNotificationService.unreadCount(this.localNotifications()) +
    this.apiNotifications().filter((n) => !n.read).length,
  );

  push(type: PrimeNotificationType): void {
    this.localNotifications.update((prev) => PrimeNotificationService.push(prev, type));
  }

  markAllAsRead(): void {
    this.localNotifications.update((prev) => PrimeNotificationService.markAllAsRead(prev));
    this.apiNotifications.update((list) => list.map((n) => ({ ...n, read: true })));
  }

  markAsRead(id: number): void {
    this.localNotifications.update((prev) => PrimeNotificationService.markAsRead(prev, id));
  }

  markApiAsRead(id: string): void {
    this.apiNotifications.update((list) =>
      list.map((n) => (n.id === id ? { ...n, read: true } : n)),
    );
  }

  /** Charge les anomalies ouvertes depuis l’API Prime (Admin uniquement). */
  async refreshFromApi(): Promise<void> {
    const primeRole = mapJwtRoleToPrimeRole(this.session.getRole()) ?? this.session.getRole();
    if (!ANOMALY_API_ROLES.has(primeRole)) {
      this.apiNotifications.set([]);
      return;
    }
    try {
      const rows = await firstValueFrom(
        this.primeAdmin.listAnomalies({ status: 'Open' }).pipe(catchError(() => of<AnomalyDto[]>([]))),
      );
      const existingRead = new Set(
        this.apiNotifications().filter((n) => n.read).map((n) => n.id),
      );
      this.apiNotifications.set(
        (rows ?? []).slice(0, 30).map((a) => ({
          id: a.id,
          title: 'Anomalie PRIME',
          body: a.description || String(a.type),
          createdAt: new Date(a.detectedAt),
          read: existingRead.has(a.id),
          severity: String(a.severity).toLowerCase() === 'critical' || String(a.severity).toLowerCase() === 'high'
            ? 'warning'
            : 'info',
          adminSection: 'anomalies' as const,
        })),
      );
    } catch {
      /* hors droits ou API indisponible */
    }
  }

  toggleDropdown(): void {
    this.shellUi.toggleDropdown();
  }

  openDropdown(): void {
    this.shellUi.openDropdown();
  }

  closeDropdown(): void {
    this.shellUi.closeDropdown();
  }

  openSettings(): void {
    this.shellUi.openSettings();
  }

  closeSettings(): void {
    this.shellUi.closeSettings();
  }
}
