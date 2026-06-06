import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';

import { DOCUMENTATION_ROUTE_BASE } from '../../lib/documentation-route-base';
import type { NotificationItemUi } from '../../models/notification-item.model';
import { NotificationDataService } from '../../services/notification-data.service';
import { DocumentationHeaderUiService } from '../../services/documentation-header-ui.service';
import { DocIconComponent } from '../doc-icon/doc-icon.component';

function pickRecentNotifications(items: NotificationItemUi[], limit: number): NotificationItemUi[] {
  return [...items]
    .sort((a, b) => {
      if (a.read !== b.read) return a.read ? 1 : -1;
      return 0;
    })
    .slice(0, limit);
}

@Component({
  selector: 'app-documentation-notification-flyout',
  standalone: true,
  imports: [CommonModule, DocIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (ui.notifOpen()) {
      <div class="fixed inset-0 z-50" (click)="ui.closeNotif()">
        <div
          class="absolute right-8 top-20 z-50 w-[26rem] max-w-[calc(100vw-2rem)] card-navy shadow-xl overflow-hidden"
          (click)="$event.stopPropagation()"
        >
          <div class="px-4 py-3 border-b border-navy-800 flex items-center justify-between gap-2">
            <div class="flex items-center gap-2 min-w-0">
              <span class="text-sm font-semibold text-white">Notifications</span>
              @if (unreadCount() > 0) {
                <span
                  class="px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-400 text-[10px] font-bold border border-blue-500/30 shrink-0"
                >
                  {{ unreadCount() }} Non lues
                </span>
              }
            </div>
            @if (unreadCount() > 0) {
              <button
                type="button"
                class="text-xs text-blue-400 hover:text-blue-300 font-medium whitespace-nowrap flex items-center gap-1.5"
                (click)="markAll()"
              >
                <app-doc-icon name="check-circle-2" klass="w-3.5 h-3.5"></app-doc-icon>
                Tout marquer comme lu
              </button>
            }
          </div>

          <div class="max-h-[28rem] overflow-y-auto p-3 space-y-3">
            @if (recent().length === 0) {
              <div class="text-center py-8">
                <div class="w-12 h-12 card-navy rounded-full flex items-center justify-center mx-auto mb-3">
                  <app-doc-icon name="bell" klass="w-6 h-6 text-muted"></app-doc-icon>
                </div>
                <p class="text-sm text-muted">Aucune notification</p>
              </div>
            } @else {
              @for (n of recent(); track n.id) {
                <div
                  class="card-navy p-3 flex items-start gap-3 group"
                  [class.border-l-2]="!n.read"
                  [class.border-l-blue-500]="!n.read"
                >
                  <div
                    class="w-9 h-9 rounded-full flex items-center justify-center shrink-0"
                    [ngClass]="n.bgColor"
                  >
                    <app-doc-icon [name]="n.icon" [klass]="'w-4 h-4 ' + n.iconColor"></app-doc-icon>
                  </div>
                  <div class="flex-1 min-w-0">
                    <div class="flex items-start justify-between gap-2 mb-0.5">
                      <h4
                        class="text-sm font-bold truncate"
                        [class.text-white]="!n.read"
                        [class.text-primary]="n.read"
                      >
                        {{ n.title }}
                      </h4>
                      <span class="text-[10px] text-muted whitespace-nowrap shrink-0">{{ n.timestamp }}</span>
                    </div>
                    <p class="text-xs text-muted line-clamp-2">{{ n.description }}</p>
                    @if (!n.read) {
                      <button
                        type="button"
                        class="mt-2 text-[11px] font-medium text-muted hover:text-white transition-colors opacity-0 group-hover:opacity-100"
                        (click)="markOne(n.id)"
                      >
                        Marquer comme lu
                      </button>
                    }
                  </div>
                  @if (!n.read) {
                    <div
                      class="w-2 h-2 rounded-full bg-blue-500 mt-1.5 shrink-0 shadow-[0_0_8px_rgba(59,130,246,0.8)]"
                    ></div>
                  }
                </div>
              }
            }
          </div>

          <div class="px-4 py-3 border-t border-navy-800">
            <button
              type="button"
              class="w-full text-sm font-medium text-blue-400 hover:text-blue-300 transition-colors"
              (click)="openFull()"
            >
              Voir plus
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class DocumentationNotificationFlyoutComponent implements OnInit, OnDestroy {
  readonly ui = inject(DocumentationHeaderUiService);
  private readonly data = inject(NotificationDataService);
  private readonly router = inject(Router);

  private readonly tick = signal(0);
  private sub?: Subscription;

  readonly recent = computed(() => {
    this.tick();
    return pickRecentNotifications(this.data.list(), 3);
  });

  readonly unreadCount = computed(() => {
    this.tick();
    return this.data.unreadCount();
  });

  ngOnInit(): void {
    this.sub = this.data.updated$.subscribe(() => this.tick.update((v) => v + 1));
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  markAll(): void {
    this.data.markAllRead();
  }

  markOne(id: string): void {
    this.data.markRead(id);
  }

  async openFull(): Promise<void> {
    this.ui.closeNotif();
    await this.router.navigate([DOCUMENTATION_ROUTE_BASE, 'notifications']);
  }
}
