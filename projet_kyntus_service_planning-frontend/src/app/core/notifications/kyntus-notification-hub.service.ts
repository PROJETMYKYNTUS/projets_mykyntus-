import { Injectable, computed, inject, signal, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { NotificationService, type PlanningNotification } from '../services/notification.service';
import { ContractService, type ContractNotification } from '../../features/contract/services/contract.service';
import { NotificationUiService } from '../../features/prime/state/notification-ui.service';
import { ParrainageStoreService } from '../../features/parrainage/services/parrainage-store.service';
import { Router } from '@angular/router';
import { NavigationActionsService } from '../navigation/navigation-actions.service';

export type KyntusNotificationSource =
  | 'planning'
  | 'contract'
  | 'reclamation'
  | 'prime'
  | 'parrainage';

export interface KyntusNotification {
  id: string;
  source: KyntusNotificationSource;
  title: string;
  body: string;
  read: boolean;
  createdAt: Date;
  action?: { route: string } | { primePath: string } | { parrainageView: string };
}

const PRIME_LABELS: Record<string, string> = {
  primeValidated: 'Prime validée',
  primeRejected: 'Prime rejetée',
  newPrimeRule: 'Nouvelle règle PRIME',
  teamPerformanceUpdated: 'Performance équipe mise à jour',
};

@Injectable({ providedIn: 'root' })
export class KyntusNotificationHubService {
  private readonly destroyRef = inject(DestroyRef);
  private readonly planningNotif = inject(NotificationService);
  private readonly contractService = inject(ContractService);
  private readonly primeUi = inject(NotificationUiService);
  private readonly parrainageStore = inject(ParrainageStoreService);
  private readonly router = inject(Router);
  private readonly navActions = inject(NavigationActionsService);

  private readonly contractItems = signal<ContractNotification[]>([]);
  private readonly planningItems = signal<PlanningNotification[]>([]);

  readonly notifications = computed<KyntusNotification[]>(() => {
    const items: KyntusNotification[] = [];

    for (const n of this.primeUi.notifications()) {
      items.push({
        id: `prime-${n.id}`,
        source: 'prime',
        title: 'PRIME',
        body: PRIME_LABELS[n.type] ?? n.type,
        read: n.read,
        createdAt: n.createdAt,
        action: { primePath: '/notifications' },
      });
    }

    for (const n of this.parrainageStore.notifications()) {
      items.push({
        id: `parrainage-${n.id}`,
        source: 'parrainage',
        title: 'Parrainage',
        body: n.message,
        read: n.read,
        createdAt: n.createdAt instanceof Date ? n.createdAt : new Date(n.createdAt),
        action: { parrainageView: 'notifications' },
      });
    }

    for (const n of this.contractItems()) {
      items.push({
        id: `contract-${n.id}`,
        source: 'contract',
        title: 'Contrat',
        body: n.message ?? n.type,
        read: n.isRead,
        createdAt: new Date(n.createdAt ?? Date.now()),
        action: { route: `/contracts/${n.contractId}` },
      });
    }

    for (const n of this.planningItems()) {
      const src = n.type === 'reclamation' ? 'reclamation' : 'planning';
      items.push({
        id: `${src}-${n.weekCode}-${n.receivedAt}`,
        source: src,
        title: src === 'reclamation' ? 'Réclamation' : 'Planning',
        body: n.message,
        read: n.read,
        createdAt: n.receivedAt instanceof Date ? n.receivedAt : new Date(n.receivedAt),
        action: {
          route: src === 'reclamation' ? '/reclamations-admin' : '/planning',
        },
      });
    }

    return items.sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());
  });

  readonly unreadCount = computed(
    () => this.notifications().filter((n) => !n.read).length,
  );

  constructor() {
    this.planningNotif.notifications$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((list) => {
        this.planningItems.set(list);
      });

    interval(30_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshContracts());

    this.refreshContracts();
  }

  refreshContracts(): void {
    this.contractService.getNotifications().subscribe({
      next: (data) => this.contractItems.set(data.slice(0, 10)),
      error: () => {},
    });
  }

  markAsRead(id: string): void {
    if (id.startsWith('prime-')) {
      const num = Number(id.replace('prime-', ''));
      this.primeUi.markAsRead(num);
      return;
    }
    if (id.startsWith('parrainage-')) {
      const pid = id.replace('parrainage-', '');
      this.parrainageStore.notifications.update((list) =>
        list.map((n) => (n.id === pid ? { ...n, read: true } : n)),
      );
      return;
    }
    if (id.startsWith('reclamation-') || id.startsWith('planning-')) {
      const item = this.planningItems().find(
        (n) => `${n.type === 'reclamation' ? 'reclamation' : 'planning'}-${n.weekCode}-${n.receivedAt}` === id,
      );
      if (item) {
        item.read = true;
        this.planningNotif.markAllRead();
      }
    }
  }

  markAllAsRead(): void {
    this.primeUi.markAllAsRead();
    this.parrainageStore.notifications.update((list) =>
      list.map((n) => ({ ...n, read: true })),
    );
    this.planningItems.update((list) => list.map((n) => ({ ...n, read: true })));
    this.planningNotif.markAllRead();
  }

  async openNotification(n: KyntusNotification): Promise<void> {
    this.markAsRead(n.id);
    if (n.action && 'route' in n.action) {
      await this.router.navigateByUrl(n.action.route);
      return;
    }
    if (n.action && 'primePath' in n.action) {
      await this.navActions.openPrimeNotifications();
      return;
    }
    if (n.action && 'parrainageView' in n.action) {
      await this.navActions.openParrainageNotifications();
    }
  }

  async openNotificationsCenter(): Promise<void> {
    const path = this.router.url.split('?')[0];
    if (path.startsWith('/prime')) {
      await this.navActions.openPrimeNotifications();
    } else if (path.startsWith('/parrainage')) {
      await this.navActions.openParrainageNotifications();
    } else if (path.startsWith('/documentation')) {
      await this.navActions.openDocumentationTab('notifications');
    } else {
      await this.router.navigateByUrl('/contracts/notifications');
    }
  }
}
