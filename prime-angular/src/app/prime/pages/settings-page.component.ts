import { ChangeDetectionStrategy, Component } from '@angular/core';
import { SettingsModuleComponent } from '../components/settings/settings-module.component';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [SettingsModuleComponent],
  template: `<app-settings-module />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsPageComponent {}
