import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { ParrainageSidebarComponent } from './parrainage-sidebar.component';
import { ParrainageHeaderComponent } from './parrainage-header.component';
import { PmDrillBarComponent } from './pm-drill-bar.component';
import { UiPreferencesService } from '../services/ui-preferences.service';
import { ParrainageStoreService } from '../services/parrainage-store.service';
import { ParrainageNavService } from '../state/parrainage-nav.service';
import { ParrainageRoleService } from '../state/parrainage-role.service';
import { PiloteDashboardPageComponent } from '../pages/pilote/pilote-dashboard-page.component';
import { PiloteSubmitPageComponent } from '../pages/pilote/pilote-submit-page.component';
import { PiloteReferralsPageComponent } from '../pages/pilote/pilote-referrals-page.component';
import { PiloteBonusPageComponent } from '../pages/pilote/pilote-bonus-page.component';
import { RhDashboardPageComponent } from '../pages/rh/rh-dashboard-page.component';
import { RhManagementPageComponent } from '../pages/rh/rh-management-page.component';
import { RhDetailsPageComponent } from '../pages/rh/rh-details-page.component';
import { RhRulesPageComponent } from '../pages/rh/rh-rules-page.component';
import { RhHistoryPageComponent } from '../pages/rh/rh-history-page.component';
import { AdminDashboardPageComponent } from '../pages/admin/admin-dashboard-page.component';
import { AdminToolsPageComponent } from '../pages/admin/admin-tools-page.component';
import { AdminWorkflowPageComponent } from '../pages/admin/admin-workflow-page.component';
import { AdminConfigPageComponent } from '../pages/admin/admin-config-page.component';
import { AdminPaymentsPageComponent } from '../pages/admin/admin-payments-page.component';
import { AdminAuditPageComponent } from '../pages/admin/admin-audit-page.component';
import { PmDashboardPageComponent } from '../pages/pm/pm-dashboard-page.component';
import { PmTeamPageComponent } from '../pages/pm/pm-team-page.component';
import { PmReferralsPageComponent } from '../pages/pm/pm-referrals-page.component';
import { PmPerformancePageComponent } from '../pages/pm/pm-performance-page.component';
import { ComptaPaymentsPageComponent } from '../pages/compta/compta-payments-page.component';
import { GlobalNotificationsPageComponent } from '../pages/shared/global-notifications-page.component';
import { GlobalSettingsPageComponent } from '../pages/shared/global-settings-page.component';

@Component({
  selector: 'app-parrainage-layout',
  standalone: true,
  imports: [
    ParrainageSidebarComponent,
    ParrainageHeaderComponent,
    PmDrillBarComponent,
    PiloteDashboardPageComponent,
    PiloteSubmitPageComponent,
    PiloteReferralsPageComponent,
    PiloteBonusPageComponent,
    RhDashboardPageComponent,
    RhManagementPageComponent,
    RhDetailsPageComponent,
    RhRulesPageComponent,
    RhHistoryPageComponent,
    AdminDashboardPageComponent,
    AdminToolsPageComponent,
    AdminWorkflowPageComponent,
    AdminConfigPageComponent,
    AdminPaymentsPageComponent,
    ComptaPaymentsPageComponent,
    AdminAuditPageComponent,
    PmDashboardPageComponent,
    PmTeamPageComponent,
    PmReferralsPageComponent,
    PmPerformancePageComponent,
    GlobalNotificationsPageComponent,
    GlobalSettingsPageComponent,
  ],
  template: `
    <div class="min-h-screen flex bg-navy-950 text-slate-100">
      <app-parrainage-sidebar
        [isCollapsed]="collapsed()"
        [currentView]="nav.currentView()"
        (toggleCollapsed)="collapsed.set($event)"
      />
      <main [class]="'flex-1 flex flex-col transition-all duration-300 min-h-screen ' + (collapsed() ? 'ml-[70px]' : 'ml-64')">
        <app-parrainage-header />
        <div [class]="'flex-1 ' + (compact() ? 'p-4' : 'p-8')">
          @if (store.loading()) {
            <div class="card-navy p-6 text-center text-sm text-slate-400">Chargement des données…</div>
          }
          @if (store.error()) {
            <div class="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-2 text-sm text-rose-200 mb-4">
              {{ store.error() }}
            </div>
          }
          @if (showPmDrill()) {
            <app-pm-drill-bar />
          }
          @for (view of [nav.currentView()]; track view) {
            <div class="page-enter">
              @switch (view) {
                @case ('pilote-dashboard') { <app-pilote-dashboard-page /> }
                @case ('pilote-submit') { <app-pilote-submit-page /> }
                @case ('pilote-referrals') { <app-pilote-referrals-page /> }
                @case ('pilote-bonus') { <app-pilote-bonus-page /> }
                @case ('rh-dashboard') { <app-rh-dashboard-page /> }
                @case ('rh-management') { <app-rh-management-page /> }
                @case ('rh-details') { <app-rh-details-page /> }
                @case ('rh-rules') { <app-rh-rules-page /> }
                @case ('rh-history') { <app-rh-history-page /> }
                @case ('compta-payments') { <app-compta-payments-page /> }
                @case ('admin-dashboard') { <app-admin-dashboard-page /> }
                @case ('admin-tools') { <app-admin-tools-page /> }
                @case ('admin-workflow') { <app-admin-workflow-page /> }
                @case ('admin-config') { <app-admin-config-page /> }
                @case ('admin-payments') { <app-admin-payments-page /> }
                @case ('admin-audit') { <app-admin-audit-page /> }
                @case ('pm-dashboard') { <app-pm-dashboard-page /> }
                @case ('pm-team') { <app-pm-team-page /> }
                @case ('pm-referrals') { <app-pm-referrals-page /> }
                @case ('pm-performance') { <app-pm-performance-page /> }
                @case ('notifications') { <app-global-notifications-page /> }
                @case ('settings') { <app-global-settings-page /> }
                @default { <app-pilote-dashboard-page /> }
              }
            </div>
          }
        </div>
        <footer class="p-8 border-t border-navy-800 text-center">
          <p class="text-xs text-slate-600">© 2024 MyKyntus — Plateforme RH entreprise.</p>
        </footer>
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ParrainageLayoutComponent {
  readonly nav = inject(ParrainageNavService);
  readonly store = inject(ParrainageStoreService);
  private readonly role = inject(ParrainageRoleService);
  private readonly ui = inject(UiPreferencesService);

  readonly collapsed = signal(false);
  readonly compact = signal(this.ui.get().compactMode);

  constructor() {
    effect(() => {
      const u = this.role.user();
      this.nav.onRoleChanged();
      void this.store.bootstrap(u.role, u.id, u.projectId);
    });
    if (typeof window !== 'undefined') {
      window.addEventListener('parrainage:ui-prefs', () => this.compact.set(this.ui.get().compactMode));
    }
  }

  showPmDrill(): boolean {
    const u = this.role.user();
    const v = this.nav.currentView();
    return (u.role === 'MANAGER' || u.role === 'RP') && v.startsWith('pm-');
  }
}
