import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { KyntusShellUiService } from '../../core/notifications/kyntus-shell-ui.service';
import { KyntusThemeService } from '../../core/theme/kyntus-theme.service';
import { NavigationActionsService } from '../../core/navigation/navigation-actions.service';
import { Router } from '@angular/router';

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
            <button type="button" (click)="shellUi.closeSettings()" aria-label="Fermer">×</button>
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
              <h3>Module actif</h3>
              <button type="button" class="ks-settings-link" (click)="openModuleSettings()">
                Paramètres du module courant
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
  private readonly nav = inject(NavigationActionsService);
  private readonly router = inject(Router);

  async openModuleSettings(): Promise<void> {
    const path = this.router.url.split('?')[0];
    if (path.startsWith('/prime')) {
      await this.nav.openPrimeSettings();
    } else if (path.startsWith('/parrainage')) {
      await this.nav.openParrainageSettings();
    } else if (path.startsWith('/documentation')) {
      await this.nav.openDocumentationSettings();
    } else {
      await this.router.navigateByUrl('/home');
    }
    this.shellUi.closeSettings();
  }
}
