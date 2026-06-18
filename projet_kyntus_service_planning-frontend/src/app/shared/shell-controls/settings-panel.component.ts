import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { KyntusShellUiService } from '../../core/notifications/kyntus-shell-ui.service';
import { KyntusThemeService } from '../../core/theme/kyntus-theme.service';

@Component({
  selector: 'app-shell-settings-panel',
  standalone: true,
  template: `
    @if (shellUi.settingsOpen()) {
      <div class="ks-settings-overlay">
        <div class="ks-settings-backdrop" (click)="shellUi.closeSettings()" aria-hidden="true"></div>
        <aside class="ks-settings-panel ky-slide-down">
          <header class="ks-settings-head">
            <h2>Paramètres</h2>
            <button type="button" (click)="shellUi.closeSettings()" aria-label="Fermer">&times;</button>
          </header>
          <div class="ks-settings-body">
            <section>
              <h3>Thème</h3>
              <div class="ks-theme-btns">
                <button
                  type="button"
                  [class.active]="theme.theme() === 'light'"
                  (click)="theme.setTheme('light')"
                >
                  Clair
                </button>
                <button
                  type="button"
                  [class.active]="theme.theme() === 'dark'"
                  (click)="theme.setTheme('dark')"
                >
                  Sombre
                </button>
              </div>
            </section>
            <section>
              <h3>Accès rapide</h3>
              <button type="button" class="ks-settings-link" (click)="openAllSettings()">
                Tous les paramètres
              </button>
              <button type="button" class="ks-settings-link" (click)="openNotifications()">
                Centre de notifications
              </button>
            </section>
          </div>
        </aside>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellSettingsPanelComponent {
  readonly shellUi = inject(KyntusShellUiService);
  readonly theme = inject(KyntusThemeService);
  private readonly router = inject(Router);

  async openAllSettings(): Promise<void> {
    this.shellUi.closeSettings();
    await this.router.navigateByUrl('/settings');
  }

  async openNotifications(): Promise<void> {
    this.shellUi.closeSettings();
    await this.router.navigateByUrl('/notifications');
  }
}
