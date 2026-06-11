import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ComptaPaymentsPageComponent } from '../compta/compta-payments-page.component';

/** Vue admin : même inbox compta avec accès étendu (ADMIN). */
@Component({
  selector: 'app-admin-payments-page',
  standalone: true,
  imports: [ComptaPaymentsPageComponent],
  template: `
    <app-compta-payments-page />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPaymentsPageComponent {}
