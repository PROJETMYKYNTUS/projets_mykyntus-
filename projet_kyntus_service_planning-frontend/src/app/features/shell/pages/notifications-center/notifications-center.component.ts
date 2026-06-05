import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  KyntusNotificationHubService,
  type KyntusNotification,
  type KyntusNotificationSource,
} from '../../../../core/notifications/kyntus-notification-hub.service';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { Bell, CheckCheck } from 'lucide';

const SOURCE_LABELS: Record<KyntusNotificationSource, string> = {
  planning: 'Planning',
  contract: 'Contrats',
  reclamation: 'Réclamations',
  prime: 'PRIME',
  parrainage: 'Parrainage',
  documentation: 'Documentation',
  formation: 'Formation',
  conge: 'Congés',
};

const FILTER_OPTIONS: { id: KyntusNotificationSource | 'all'; label: string }[] = [
  { id: 'all', label: 'Toutes' },
  { id: 'planning', label: 'Planning' },
  { id: 'contract', label: 'Contrats' },
  { id: 'reclamation', label: 'Réclamations' },
  { id: 'prime', label: 'PRIME' },
  { id: 'parrainage', label: 'Parrainage' },
  { id: 'documentation', label: 'Documentation' },
];

@Component({
  selector: 'app-notifications-center',
  standalone: true,
  imports: [RouterLink, LucideIconComponent],
  templateUrl: './notifications-center.component.html',
  styleUrl: './notifications-center.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsCenterComponent {
  private readonly hub = inject(KyntusNotificationHubService);
  private readonly route = inject(ActivatedRoute);

  readonly icons = { bell: Bell, markAll: CheckCheck };
  readonly filterOptions = FILTER_OPTIONS;
  readonly sourceLabels = SOURCE_LABELS;

  readonly activeFilter = signal<KyntusNotificationSource | 'all'>('all');

  readonly filteredNotifications = computed(() => {
    const filter = this.activeFilter();
    const all = this.hub.notifications();
    if (filter === 'all') return all;
    return all.filter((n) => n.source === filter);
  });

  readonly unreadCount = computed(() => this.filteredNotifications().filter((n) => !n.read).length);

  constructor() {
    const src = this.route.snapshot.queryParamMap.get('source') as KyntusNotificationSource | null;
    if (src && src in SOURCE_LABELS) {
      this.activeFilter.set(src);
    }
    this.hub.refreshContracts();
  }

  setFilter(id: KyntusNotificationSource | 'all'): void {
    this.activeFilter.set(id);
  }

  sourceLabel(source: KyntusNotificationSource): string {
    return SOURCE_LABELS[source] ?? source;
  }

  formatDate(d: Date): string {
    return d.toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
  }

  async openNotification(n: KyntusNotification): Promise<void> {
    await this.hub.openNotification(n);
  }

  markAllRead(): void {
    this.hub.markAllAsRead();
  }
}
