import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ParrainageLayoutComponent } from './parrainage/components/parrainage-layout.component';

@Component({
  selector: 'app-root',
  imports: [ParrainageLayoutComponent],
  template: `<app-parrainage-layout />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {}
