import { Injectable, computed, inject, signal, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { NotificationService, type PlanningNotification } from '../services/notification.service';
import { ContractService, type ContractNotification } from '../../features/contract/services/contract.service';
import { NotificationUiService } from '../../features/prime/state/notification-ui.service';
import { ParrainageStoreService } from '../../features/parrainage/services/parrainage-store.service';
import { NotificationDataService } from '../../features/documentation/services/notification-data.service';
import { KyntusUserPreferencesService } from '../settings/kyntus-user-preferences.service';
import { Router } from '@angular/router';

export type KyntusNotificationSource =
  | 'planning'
  | 'contract'
  | 'reclamation'
  | 'prime'
  | 'parrainage'
  | 'documentation'
  | 'formation'
  | 'conge';

export interface KyntusNotification {
  id: string;
  source: KyntusNotificationSource;
  title: string;
  body: string;
  read: boolean;
  createdAt: Date;
  action?: { route: string; source?: KyntusNotificationSource };
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
  private readonly docNotif = inject(NotificationDataService);
  private readonly userPrefs = inject(KyntusUserPreferencesService);
  private readonly router = inject(Router);
  private readonly contractItems = signal<ContractNotification[]>([]);
  private readonly planningItems = signal<PlanningNotification[]>([]);
  private readonly docTick = signal(0);

  readonly notifications = computed<KyntusNotification[]>(() => {
    void this.docTick();
    const items: KyntusNotification[] = [];
    const prefs = this.userPrefs.preferences().notifications;

    if (prefs.prime) {
      for (const n of this.primeUi.notifications()) {
        items.push({
          id: `prime-${n.id}`,
          source: 'prime',
          title: 'PRIME',
          body: PRIME_LABELS[n.type] ?? n.type,
          read: n.read,
          createdAt: n.createdAt,
          action: { route: '/notifications', source: 'prime' },
        });
      }
    }

    if (prefs.parrainage) {
      for (const n of this.parrainageStore.notifications()) {
        items.push({
          id: `parrainage-${n.id}`,
          source: 'parrainage',
          title: 'Parrainage',
          body: n.message,
          read: n.read,
          createdAt: n.createdAt instanceof Date ? n.createdAt : new Date(n.createdAt),
          action: { route: '/notifications', source: 'parrainage' },
        });
      }
    }

    if (prefs.contracts) {
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
    }

    if (prefs.planning || prefs.reclamations) {
      for (const n of this.planningItems()) {
        const src = n.type === 'reclamation' ? 'reclamation' : 'planning';
        if (src === 'reclamation' && !prefs.reclamations) continue;
        if (src === 'planning' && !prefs.planning) continue;
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
    }

    if (prefs.documentation) {
      for (const n of this.docNotif.list()) {
        items.push({
          id: `documentation-${n.id}`,
          source: 'documentation',
          title: 'Documentation',
          body: n.title + (n.description ? ` — ${n.description}` : ''),
          read: n.read,
          createdAt: this.parseDocTimestamp(n.timestamp),
          action: { route: '/notifications', source: 'documentation' },
        });
      }
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

    this.docNotif.updated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.docTick.update((v) => v + 1));

    interval(30_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshContracts());

    this.refreshContracts();
  }

  private parseDocTimestamp(ts: string): Date {
    const d = new Date(ts);
    return Number.isNaN(d.getTime()) ? new Date() : d;
  }

  refreshContracts(): void {
    if (!this.userPrefs.isSourceEnabled('contracts')) return;
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
    if (id.startsWith('documentation-')) {
      const did = id.replace('documentation-', '');
      this.docNotif.markRead(did);
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
    this.docNotif.markAllRead();
    this.planningItems.update((list) => list.map((n) => ({ ...n, read: true })));
    this.planningNotif.markAllRead();
  }

  async openNotification(n: KyntusNotification): Promise<void> {
    this.markAsRead(n.id);
    if (!n.action) return;
    if (n.action.source) {
      await this.openNotificationsCenter(n.action.source);
      return;
    }
    await this.router.navigateByUrl(n.action.route);
  }

  async openNotificationsCenter(source?: KyntusNotificationSource): Promise<void> {
    const query = source ? { source } : {};
    await this.router.navigate(['/notifications'], { queryParams: query });
  }

  filterBySource(source: KyntusNotificationSource | 'all'): KyntusNotification[] {
    const all = this.notifications();
    if (source === 'all') return all;
    return all.filter((n) => n.source === source);
  }
}
