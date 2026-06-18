import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { Bell, CheckCircle2, FileText, Settings, XCircle } from 'lucide';
import type { IconNode } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

export type NotificationFilter = 'all' | 'unread' | 'documents' | 'system';

export interface NotificationCenterItem {
  id: string;
  type: 'documents' | 'system';
  title: string;
  description: string;
  timestamp: string;
  dateGroup: string;
  read: boolean;
  icon: IconNode;
  iconColor: string;
  bgColor: string;
}

const FILTER_LABELS: Record<NotificationFilter, string> = {
  all: 'Toutes',
  unread: 'Non lues',
  documents: 'Parrainages',
  system: 'Système',
};

@Component({
  selector: 'app-notification-center',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="max-w-4xl mx-auto space-y-6">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <h2 class="text-xl font-bold text-white">{{ title }}</h2>
          @if (unreadCount() > 0) {
            <span class="px-2.5 py-0.5 rounded-full bg-blue-500/20 text-blue-400 text-xs font-bold border border-blue-500/30">
              {{ unreadCount() }} {{ unreadLabel }}
            </span>
          }
        </div>
        <button type="button" (click)="markAllRead.emit()" class="text-sm text-muted hover:text-white transition-colors flex items-center gap-2">
          <app-lucide-icon [icon]="checkIcon" className="w-4 h-4" />
          {{ markAllLabel }}
        </button>
      </div>

      <div class="flex gap-2 overflow-x-auto pb-2 scrollbar-hide">
        @for (f of filters; track f) {
          <button (click)="filter.set(f)"
            [class]="'px-4 py-2 rounded-lg text-sm font-medium transition-all whitespace-nowrap ' + (filter() === f ? 'bg-blue-600 text-white shadow-[0_0_10px_rgba(37,99,235,0.3)]' : 'bg-card text-muted hover:bg-input hover:text-primary border border-default')">
            {{ filterLabel(f) }}
          </button>
        }
      </div>

      <div class="space-y-8">
        @for (group of groupedNotifications(); track group.group) {
          <div class="space-y-4">
            <h3 class="text-xs font-bold text-muted uppercase tracking-widest">{{ group.group }}</h3>
            <div class="space-y-3">
              @for (n of group.items; track n.id) {
                <div [class]="'card-navy p-4 flex items-start gap-4 group cursor-pointer ' + (!n.read ? 'border-l-2 border-l-blue-500' : '')">
                  <div [class]="'w-10 h-10 rounded-full flex items-center justify-center shrink-0 ' + n.bgColor">
                    <app-lucide-icon [icon]="n.icon" [className]="'w-5 h-5 ' + n.iconColor" />
                  </div>
                  <div class="flex-1 min-w-0">
                    <div class="flex items-start justify-between gap-2 mb-1">
                      <h4 [class]="'text-sm font-bold truncate ' + (!n.read ? 'text-white' : 'text-primary')">{{ n.title }}</h4>
                      <span class="text-xs text-muted whitespace-nowrap shrink-0">{{ n.timestamp }}</span>
                    </div>
                    <p class="text-sm text-muted line-clamp-2">{{ n.description }}</p>
                    <div class="mt-3 flex items-center gap-4 opacity-0 group-hover:opacity-100 transition-opacity">
                      @if (!n.read) {
                        <button type="button" (click)="markRead.emit(n.id)" class="text-xs font-medium text-muted hover:text-white transition-colors">Marquer comme lu</button>
                      }
                    </div>
                  </div>
                  @if (!n.read) {
                    <div class="w-2 h-2 rounded-full bg-blue-500 mt-2 shrink-0 shadow-[0_0_8px_rgba(59,130,246,0.8)]"></div>
                  }
                </div>
              }
            </div>
          </div>
        }

        @if (filteredNotifications().length === 0) {
          <div class="text-center py-12">
            <div class="w-16 h-16 bg-card rounded-full flex items-center justify-center mx-auto mb-4">
              <app-lucide-icon [icon]="bellIcon" className="w-8 h-8 text-muted" />
            </div>
            <h3 class="text-lg font-medium text-white mb-2">{{ emptyTitle }}</h3>
            <p class="text-muted">{{ emptyDescription }}</p>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationCenterComponent {
  @Input() title = 'Notifications';
  @Input() unreadLabel = 'Non lues';
  @Input() markAllLabel = 'Tout marquer comme lu';
  @Input() emptyTitle = 'Aucune notification';
  @Input() emptyDescription = 'Vous êtes à jour !';
  @Input({ required: true }) items: NotificationCenterItem[] = [];
  @Input() filters: NotificationFilter[] = ['all', 'unread', 'documents', 'system'];
  @Output() markAllRead = new EventEmitter<void>();
  @Output() markRead = new EventEmitter<string>();

  readonly bellIcon = Bell;
  readonly checkIcon = CheckCircle2;
  readonly filter = signal<NotificationFilter>('all');

  filterLabel(f: NotificationFilter): string {
    return FILTER_LABELS[f];
  }

  unreadCount = () => this.items.filter((n) => !n.read).length;

  readonly filteredNotifications = computed(() => {
    const f = this.filter();
    return this.items.filter((n) => {
      if (f === 'all') return true;
      if (f === 'unread') return !n.read;
      return n.type === f;
    });
  });

  readonly groupedNotifications = computed(() => {
    const groups: Record<string, NotificationCenterItem[]> = {};
    for (const n of this.filteredNotifications()) {
      (groups[n.dateGroup] ??= []).push(n);
    }
    return Object.entries(groups).map(([group, items]) => ({ group, items }));
  });
}

function toDateGroup(d: Date): string {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const notifDate = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  const diffDays = Math.floor((today.getTime() - notifDate.getTime()) / 86400000);
  if (diffDays === 0) return "Aujourd'hui";
  if (diffDays === 1) return 'Hier';
  return 'Plus tôt';
}

function toRelativeTimestamp(d: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);
  if (diffMins < 1) return "À l'instant";
  if (diffMins < 60) return `Il y a ${diffMins} min`;
  if (diffHours < 24) return `Il y a ${diffHours}h`;
  if (diffDays === 1) return `Hier à ${d.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}`;
  if (diffDays < 7) return d.toLocaleDateString('fr-FR', { weekday: 'long', day: 'numeric', month: 'short' });
  return d.toLocaleDateString('fr-FR');
}

export function mapReferralNotificationToCenter(item: {
  id: string;
  type: string;
  message: string;
  createdAt: Date;
  read: boolean;
}): NotificationCenterItem {
  const msg = item.message.toLowerCase();
  const isReject = msg.includes('reject') || msg.includes('refus') || msg.includes('rejet');
  const isReward = item.type === 'REFERRAL_REWARDED';
  const isNew = item.type === 'NEW_REFERRAL';

  let icon: IconNode = Bell;
  let iconColor = 'text-purple-500';
  let bgColor = 'bg-purple-500/10';
  let type: NotificationCenterItem['type'] = 'documents';

  if (isReject) {
    icon = XCircle;
    iconColor = 'text-red-500';
    bgColor = 'bg-red-500/10';
  } else if (isReward || isNew) {
    icon = isNew ? FileText : CheckCircle2;
    iconColor = isReward ? 'text-emerald-500' : 'text-blue-500';
    bgColor = isReward ? 'bg-emerald-500/10' : 'bg-blue-500/10';
  } else if (item.type === 'STATUS_CHANGED') {
    icon = Settings;
    iconColor = 'text-purple-500';
    bgColor = 'bg-purple-500/10';
    type = 'system';
  }

  return {
    id: item.id,
    type,
    icon,
    title:
      item.type === 'NEW_REFERRAL'
        ? 'Nouveau parrainage soumis'
        : item.type === 'REFERRAL_REWARDED'
          ? 'Prime de parrainage versée'
          : item.type === 'STATUS_CHANGED'
            ? 'Statut du parrainage mis à jour'
            : 'Notification',
    description: item.message,
    timestamp: toRelativeTimestamp(item.createdAt),
    dateGroup: toDateGroup(item.createdAt),
    read: item.read,
    iconColor,
    bgColor,
  };
}
