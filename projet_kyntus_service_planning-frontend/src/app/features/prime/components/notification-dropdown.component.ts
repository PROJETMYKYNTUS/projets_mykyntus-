import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationUiService } from '../state/notification-ui.service';
import { I18nService } from '../state/i18n.service';

@Component({
  selector: 'app-notification-dropdown',
  standalone: true,
  template: `
    @if (notifications.dropdownOpen()) {
      <div class="fixed inset-0 z-40" (click)="notifications.closeDropdown()">
        <div
          class="absolute right-4 top-16 z-50 w-80 card-navy shadow-xl"
          (click)="$event.stopPropagation()"
        >
        <div class="px-4 py-3 border-b border-navy-800 flex items-center justify-between">
          <span class="text-sm font-semibold text-white">
            {{ i18n.t('topbar.notifications') }}
          </span>
          <button
            type="button"
            (click)="notifications.markAllAsRead()"
            class="text-xs text-blue-400 hover:text-blue-300 font-medium"
          >
            Tout marquer comme lu
          </button>
        </div>
        <div class="max-h-80 overflow-y-auto">
          @if (notifications.notifications().length === 0) {
            <div class="px-4 py-6 text-sm text-slate-400 text-center">
              Aucune notification
            </div>
          } @else {
            <ul class="divide-y divide-navy-800">
              @for (n of notifications.notifications(); track n.id) {
                <li class="px-4 py-3 flex items-start gap-2 hover:bg-navy-800">
                  <span
                    class="mt-1 w-2 h-2 rounded-full"
                    [class.bg-slate-300]="n.read"
                    [class.bg-emerald-500]="!n.read"
                  ></span>
                  <div>
                    <p class="text-sm text-slate-200">
                      {{ i18n.t('notifications.' + n.type) }}
                    </p>
                    <p class="text-xs text-slate-500 mt-1">
                      {{ n.createdAt.toLocaleString() }}
                    </p>
                    @if (!n.read) {
                      <button
                        type="button"
                        (click)="notifications.markAsRead(n.id)"
                        class="mt-1 text-[11px] text-blue-400 hover:text-blue-300"
                      >
                        Marquer comme lu
                      </button>
                    }
                  </div>
                </li>
              }
            </ul>
          }
        </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationDropdownComponent {
  readonly notifications = inject(NotificationUiService);
  readonly i18n = inject(I18nService);
}
