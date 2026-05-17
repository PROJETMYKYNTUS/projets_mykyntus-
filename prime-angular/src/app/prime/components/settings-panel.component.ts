import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ThemeService } from '../state/theme.service';
import { I18nService } from '../state/i18n.service';
import { NotificationUiService } from '../state/notification-ui.service';
import { cn } from '@/lib/utils';

@Component({
  selector: 'app-settings-panel',
  standalone: true,
  template: `
    @if (notificationUi.settingsOpen()) {
      <div class="fixed inset-0 z-40 flex">
        <div
          class="flex-1 bg-slate-900/30"
          (click)="notificationUi.closeSettings()"
          aria-hidden="true"
        ></div>
        <aside class="w-80 max-w-full bg-card h-full shadow-xl border-l border-default flex flex-col">
          <header class="px-6 py-4 border-b border-default flex items-center justify-between">
            <h2 class="text-lg font-semibold text-primary">
              {{ i18n.t('settings.title') }}
            </h2>
            <button
              type="button"
              (click)="notificationUi.closeSettings()"
              class="text-muted hover:text-primary text-sm"
            >
              ✕
            </button>
          </header>

          <div class="flex-1 overflow-y-auto px-6 py-4 space-y-6">
            <section>
              <h3 class="text-sm font-semibold text-muted mb-2">
                {{ i18n.t('settings.theme') }}
              </h3>
              <div class="flex gap-2">
                <button
                  type="button"
                  (click)="theme.setTheme('light')"
                  [class]="themeBtnClass(theme.theme() === 'light')"
                >
                  {{ i18n.t('settings.theme.light') }}
                </button>
                <button
                  type="button"
                  (click)="theme.setTheme('dark')"
                  [class]="themeBtnClass(theme.theme() === 'dark')"
                >
                  {{ i18n.t('settings.theme.dark') }}
                </button>
              </div>
            </section>

            <section>
              <h3 class="text-sm font-semibold text-muted mb-2">
                {{ i18n.t('settings.notifications') }}
              </h3>
              <p class="text-xs text-muted">
                {{ i18n.t('topbar.notifications') }}
              </p>
            </section>
          </div>
        </aside>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsPanelComponent {
  readonly theme = inject(ThemeService);
  readonly i18n = inject(I18nService);
  readonly notificationUi = inject(NotificationUiService);

  themeBtnClass(active: boolean): string {
    return cn(
      'flex-1 px-3 py-2 rounded-lg text-sm border',
      active
        ? 'border-blue-500 bg-blue-600/10 text-blue-500'
        : 'border-default text-primary hover:bg-app',
    );
  }
}
