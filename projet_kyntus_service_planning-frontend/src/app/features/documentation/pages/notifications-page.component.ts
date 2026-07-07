import { CommonModule, KeyValuePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { Subscription } from 'rxjs';

import { DocIconComponent } from '../components/doc-icon/doc-icon.component';
import type { NotificationFilter } from '../models/notification-item.model';
import { NotificationDataService } from '../services/notification-data.service';

const FILTER_LABELS: Record<NotificationFilter, string> = {
  all: 'Toutes',
  unread: 'Non lues',
  system: 'Système',
  documents: 'Métier',
};

@Component({
  standalone: true,
  selector: 'app-notifications-page',
  imports: [CommonModule, KeyValuePipe, DocIconComponent],
  templateUrl: './notifications-page.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsPageComponent implements OnInit, OnDestroy {
  private readonly data = inject(NotificationDataService);
  private sub?: Subscription;

  readonly filter = signal<NotificationFilter>('all');
  readonly filterOptions: NotificationFilter[] = ['all', 'unread', 'system', 'documents'];

  private readonly tick = signal(0);

  ngOnInit(): void {
    this.data.ensureLoaded();
    this.sub = this.data.updated$.subscribe(() => this.tick.update((v) => v + 1));
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private readonly items = computed(() => {
    this.tick();
    return this.data.list();
  });

  readonly filteredItems = computed(() => {
    const f = this.filter();
    return this.items().filter((n) => {
      if (f === 'all') return true;
      if (f === 'unread') return !n.read;
      return n.type === f;
    });
  });

  readonly grouped = computed(() => {
    const map: Record<string, ReturnType<NotificationDataService['list']>> = {};
    for (const n of this.filteredItems()) {
      if (!map[n.dateGroup]) map[n.dateGroup] = [];
      map[n.dateGroup].push(n);
    }
    return map;
  });

  readonly unreadCount = computed(() => this.items().filter((n) => !n.read).length);

  setFilter(f: NotificationFilter): void {
    this.filter.set(f);
  }

  filterLabel(f: NotificationFilter): string {
    return FILTER_LABELS[f];
  }

  markAll(): void {
    this.data.markAllRead();
  }

  markOne(id: string): void {
    this.data.markRead(id);
  }

  deleteOne(id: string): void {
    this.data.remove(id);
  }
}
