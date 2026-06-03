import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { isProjectLeadRole } from '../lib/projectLeadRole';
import { RoleService } from '../state/role.service';
import { PrimeSectionService } from '../state/prime-section.service';
import { RpDashboardComponent } from './rp/rp-dashboard.component';
import { AdminDashboardComponent } from './admin/admin-dashboard.component';
import { AuditRootComponent } from './audit/audit-root.component';
import { NotificationsPageComponent } from './notifications-page.component';
import { PrimeDashboardStandardComponent } from './prime-dashboard-standard.component';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [
    RpDashboardComponent,
    AdminDashboardComponent,
    AuditRootComponent,
    NotificationsPageComponent,
    PrimeDashboardStandardComponent,
  ],
  template: `
    @if (isProjectLeadRole(role.currentRole())) {
      @if (primeSection.activeRpSection() === 'notifications') {
        <app-notifications-page />
      } @else {
        <app-rp-dashboard [rpUserId]="role.currentUser().id" />
      }
    } @else {
    @switch (role.currentRole()) {
      @case ('Admin') {
        @if (primeSection.activeAdminSection() === 'notifications') {
          <app-notifications-page />
        } @else {
          <app-admin-dashboard />
        }
      }
      @case ('Audit') {
        <app-audit-root />
      }
      @default {
        <app-prime-dashboard-standard />
      }
    }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPageComponent {
  readonly role = inject(RoleService);
  readonly primeSection = inject(PrimeSectionService);
  /** Pour le template (shell legacy RP uniquement). */
  protected readonly isProjectLeadRole = isProjectLeadRole;
}
