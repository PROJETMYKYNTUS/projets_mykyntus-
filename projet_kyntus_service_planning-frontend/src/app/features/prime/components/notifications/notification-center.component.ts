import type { IconNode } from 'lucide';
import { Bell, CheckCircle2, Settings, XCircle } from 'lucide';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { cn } from '@/lib/utils';

export type NotificationFilter = 'all' | 'unread' | 'documents' | 'system';

export interface NotificationItemVm {
  id: number;
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

@Component({
  selector: 'app-notification-center',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="max-w-4xl mx-auto space-y-6 p-8 bg-app min-h-full">
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <h2 class="text-xl font-bold text-primary">Notifications</h2>
          @if (unreadCount() > 0) {
            <span class="ky-badge ky-badge--info">
              {{ unreadCount() }} Non lues
            </span>
          }
        </div>
        <button
          type="button"
          (click)="markAllRead.emit()"
          class="text-sm text-muted hover:text-primary transition-colors flex items-center gap-2"
        >
          <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
          Tout marquer comme lu
        </button>
      </div>

      <div class="flex gap-2 overflow-x-auto pb-2 scrollbar-hide">
        @for (f of filters; track f.key) {
          <button type="button" (click)="filter.set(f.key)" [class]="filterChipClass(f.key)">
            {{ f.label }}
          </button>
        }
      </div>

      <div class="space-y-8">
        @for (group of groupedEntries(); track group[0]) {
          <div class="space-y-4">
            <h3 class="text-xs font-bold text-muted uppercase tracking-widest">{{ group[0] }}</h3>
            <div class="space-y-3">
              @for (notification of group[1]; track notification.id) {
                <div
                  [class]="
                    'bg-card border border-default rounded-xl p-4 flex items-start gap-4 group cursor-pointer' +
                    (!notification.read ? ' border-l-2 border-l-[var(--blue-500)]' : '')
                  "
                >
                  <div
                    class="w-10 h-10 rounded-full flex items-center justify-center shrink-0"
                    [class]="notification.bgColor"
                  >
                    <app-lucide-icon
                      [icon]="notification.icon"
                      [className]="'w-5 h-5 ' + notification.iconColor"
                    />
                  </div>
                  <div class="flex-1 min-w-0">
                    <div class="flex items-start justify-between gap-2 mb-1">
                      <h4
                        class="text-sm font-bold truncate"
                        [class.text-primary]="!notification.read"
                        [class.text-muted]="notification.read"
                      >
                        {{ notification.title }}
                      </h4>
                      <span class="text-xs text-muted whitespace-nowrap shrink-0">
                        {{ notification.timestamp }}
                      </span>
                    </div>
                    <p class="text-sm text-muted line-clamp-2">{{ notification.description }}</p>
                    @if (!notification.read) {
                      <div class="mt-3">
                        <button
                          type="button"
                          class="text-xs font-medium text-muted hover:text-primary transition-colors"
                          (click)="markRead.emit(notification.id)"
                        >
                          Marquer comme lu
                        </button>
                      </div>
                    }
                  </div>
                  @if (!notification.read) {
                    <div
                      class="w-2 h-2 rounded-full bg-[var(--blue-500)] mt-2 shrink-0 shadow-[var(--shadow-2)]"
                    ></div>
                  }
                </div>
              }
            </div>
          </div>
        }

        @if (filtered().length === 0) {
          <div class="text-center py-12">
            <div
              class="w-16 h-16 bg-card border border-default rounded-full flex items-center justify-center mx-auto mb-4"
            >
              <app-lucide-icon [icon]="icons.bell" className="w-8 h-8 text-muted" />
            </div>
            <h3 class="text-lg font-medium text-primary mb-2">Aucune notification</h3>
            <p class="text-muted">Vous êtes à jour !</p>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationCenterComponent {
  readonly items = input.required<NotificationItemVm[]>();
  readonly markAllRead = output<void>();
  readonly markRead = output<number>();

  readonly filter = signal<NotificationFilter>('all');

  readonly filters: { key: NotificationFilter; label: string }[] = [
    { key: 'all', label: 'Toutes' },
    { key: 'unread', label: 'Non lues' },
    { key: 'system', label: 'Système' },
    { key: 'documents', label: 'Métier' },
  ];

  readonly icons = { check: CheckCircle2, bell: Bell };

  readonly unreadCount = computed(() => this.items().filter((n) => !n.read).length);

  readonly filtered = computed(() => {
    const f = this.filter();
    return this.items().filter((n) => {
      if (f === 'all') return true;
      if (f === 'unread') return !n.read;
      return n.type === f;
    });
  });

  readonly groupedEntries = computed(() => {
    const grouped = this.filtered().reduce(
      (acc, n) => {
        if (!acc[n.dateGroup]) acc[n.dateGroup] = [];
        acc[n.dateGroup].push(n);
        return acc;
      },
      {} as Record<string, NotificationItemVm[]>,
    );
    return Object.entries(grouped) as [string, NotificationItemVm[]][];
  });

  filterChipClass(f: NotificationFilter): string {
    return cn(
      'px-4 py-2 rounded-lg text-sm font-medium transition-all whitespace-nowrap',
      this.filter() === f
        ? 'ky-btn-primary shadow-[var(--shadow-2)]'
        : 'bg-card text-muted hover:bg-app hover:text-primary border border-default',
    );
  }
}

export function mapPrimeNotification(item: {
  id: number;
  type: string;
  createdAt: Date;
  read: boolean;
  label: string;
}): NotificationItemVm {
  const statusByType: Record<
    string,
    { kind: 'documents' | 'system'; color: string; bg: string; icon: IconNode }
  > = {
    primeValidated: {
      kind: 'documents',
      color: 'text-[var(--success-text)]',
      bg: 'bg-[var(--success-bg)]',
      icon: CheckCircle2,
    },
    primeRejected: {
      kind: 'documents',
      color: 'text-[var(--danger-text)]',
      bg: 'bg-[var(--danger-bg)]',
      icon: XCircle,
    },
    newPrimeRule: {
      kind: 'system',
      color: 'text-[var(--info-text)]',
      bg: 'bg-[var(--info-bg)]',
      icon: Settings,
    },
    teamPerformanceUpdated: {
      kind: 'system',
      color: 'text-[var(--electric-blue)]',
      bg: 'bg-[var(--info-bg)]',
      icon: Bell,
    },
  };
  const style = statusByType[item.type] ?? statusByType['teamPerformanceUpdated'];
  return {
    id: item.id,
    type: style.kind,
    title: item.label,
    description: item.label,
    timestamp: item.createdAt.toLocaleString(),
    dateGroup: toDateGroup(item.createdAt),
    read: item.read,
    icon: style.icon,
    iconColor: style.color,
    bgColor: style.bg,
  };
}

function toDateGroup(date: Date): string {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const current = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const diff = Math.floor((today.getTime() - current.getTime()) / 86400000);
  if (diff === 0) return "Aujourd'hui";
  if (diff === 1) return 'Hier';
  return 'Plus tôt';
}
