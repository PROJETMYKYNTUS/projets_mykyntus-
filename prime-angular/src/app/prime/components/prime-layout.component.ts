import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { PRIME_AUTHORIZED_ROLES } from '../models';
import { isProjectLeadRole } from '../lib/projectLeadRole';
import { isPrimePathAllowedForRole } from '../lib/prime-nav-access';
import { RoleService } from '../state/role.service';
import { PrimeSectionService } from '../state/prime-section.service';
import { PrimeSidebarComponent } from '../components/prime-sidebar.component';
import { TopbarComponent } from '../components/topbar.component';
import { SettingsPanelComponent } from '../components/settings-panel.component';
import { AccessDeniedComponent } from '../pages/access-denied.component';
import { DashboardPageComponent } from '../pages/dashboard-page.component';
import { NotificationsPageComponent } from '../pages/notifications-page.component';
import { SettingsPageComponent } from '../pages/settings-page.component';
import { PrimeTypesPageComponent } from '../pages/prime-types-page.component';
import { PrimeRulesPageComponent } from '../pages/prime-rules-page.component';
import { PrimeResultsPageComponent } from '../pages/prime-results-page.component';
import { PrimeValidationPageComponent } from '../pages/prime-validation-page.component';
import { PrimeHistoryPageComponent } from '../pages/prime-history-page.component';
import { TeamPerformancePageComponent } from '../pages/team-performance-page.component';
import { PrimeConfigurationPageComponent } from '../pages/prime-configuration-page.component';
import { SuperviseurScopePageComponent } from '../pages/superviseur-scope-page.component';
import { PrimeSaisieComponent } from '../pages/prime-saisie.component';
import { TemplateManagerComponent } from '../pages/template-manager.component';
import { EmployeeDashboardPageComponent } from '../pages/employee/employee-dashboard-page.component';
import { MyPrimesPageComponent } from '../pages/employee/my-primes-page.component';
import { MyPerformancePageComponent } from '../pages/employee/my-performance-page.component';
import { OrganisationManagementComponent } from '../pages/organisation-management.component';
import { PrimeFichesPilotesPageComponent } from '../pages/prime-fiches-pilotes-page.component';
import { PrimeCelluleIndicatorsPageComponent } from '../pages/prime-cellule-indicators-page.component';
import { PrimeSaisieCellulePageComponent } from '../pages/prime-saisie-cellule-page.component';
import { PrimeFichesCommunesListComponent } from '../pages/prime-fiches-communes-list.component';
import { PrimeGlobalPoolPageComponent } from '../pages/prime-global-pool-page.component';
import { ChefProjetScopePageComponent } from '../pages/chef-projet-scope-page.component';
import { PrimeFicheSessionService } from '../services/prime-fiche-session.service';
import { PrimeNavRequestService } from '../services/prime-nav-request.service';
import { PrimeAdminService } from '../services/prime-admin.service';

@Component({
  selector: 'app-prime-layout',
  standalone: true,
  imports: [
    PrimeSidebarComponent,
    TopbarComponent,
    SettingsPanelComponent,
    AccessDeniedComponent,
    DashboardPageComponent,
    NotificationsPageComponent,
    SettingsPageComponent,
    PrimeTypesPageComponent,
    PrimeRulesPageComponent,
    PrimeResultsPageComponent,
    PrimeValidationPageComponent,
    PrimeHistoryPageComponent,
    TeamPerformancePageComponent,
    PrimeConfigurationPageComponent,
    SuperviseurScopePageComponent,
    PrimeSaisieComponent,
    TemplateManagerComponent,
    EmployeeDashboardPageComponent,
    MyPrimesPageComponent,
    MyPerformancePageComponent,
    OrganisationManagementComponent,
    PrimeFichesPilotesPageComponent,
    PrimeCelluleIndicatorsPageComponent,
    PrimeSaisieCellulePageComponent,
    PrimeFichesCommunesListComponent,
    PrimeGlobalPoolPageComponent,
    ChefProjetScopePageComponent,
  ],
  template: `
    @if (!isAuthorized()) {
      <app-access-denied />
    } @else if (adminNotificationsShell()) {
      <div class="flex h-screen bg-navy-950 overflow-hidden font-sans">
        <app-prime-sidebar
          [collapsed]="collapsed()"
          [currentView]="currentView()"
          (toggleCollapsed)="toggleCollapsed()"
          (changeView)="setView($event)"
        />
        <div class="flex-1 flex flex-col overflow-hidden">
          <app-topbar />
          <main class="flex-1 min-w-0 overflow-y-auto"><app-notifications-page /></main>
          <app-settings-panel />
        </div>
      </div>
    } @else if (adminSettingsShell()) {
      <div class="flex h-screen bg-navy-950 overflow-hidden font-sans">
        <app-prime-sidebar
          [collapsed]="collapsed()"
          [currentView]="currentView()"
          (toggleCollapsed)="toggleCollapsed()"
          (changeView)="setView($event)"
        />
        <div class="flex-1 flex flex-col overflow-hidden">
          <app-topbar />
          <main class="flex-1 min-w-0 overflow-y-auto"><app-settings-page /></main>
          <app-settings-panel />
        </div>
      </div>
    } @else if (rpNotificationsShell()) {
      <div class="flex h-screen bg-navy-950 overflow-hidden font-sans">
        <app-prime-sidebar
          [collapsed]="collapsed()"
          [currentView]="currentView()"
          (toggleCollapsed)="toggleCollapsed()"
          (changeView)="setView($event)"
        />
        <div class="flex-1 flex flex-col overflow-hidden">
          <app-topbar />
          <main class="flex-1 min-w-0 overflow-y-auto"><app-notifications-page /></main>
          <app-settings-panel />
        </div>
      </div>
    } @else if (rpSettingsShell()) {
      <div class="flex h-screen bg-navy-950 overflow-hidden font-sans">
        <app-prime-sidebar
          [collapsed]="collapsed()"
          [currentView]="currentView()"
          (toggleCollapsed)="toggleCollapsed()"
          (changeView)="setView($event)"
        />
        <div class="flex-1 flex flex-col overflow-hidden">
          <app-topbar />
          <main class="flex-1 min-w-0 overflow-y-auto"><app-settings-page /></main>
          <app-settings-panel />
        </div>
      </div>
    } @else if (auditSettingsShell()) {
      <div class="flex h-screen bg-navy-950 overflow-hidden font-sans">
        <app-prime-sidebar
          [collapsed]="collapsed()"
          [currentView]="currentView()"
          (toggleCollapsed)="toggleCollapsed()"
          (changeView)="setView($event)"
        />
        <div class="flex-1 flex flex-col overflow-hidden">
          <app-topbar />
          <main class="flex-1 min-w-0 overflow-y-auto"><app-settings-page /></main>
          <app-settings-panel />
        </div>
      </div>
    } @else if (auditNotificationsShell()) {
      <div class="flex h-screen bg-navy-950 overflow-hidden font-sans">
        <app-prime-sidebar
          [collapsed]="collapsed()"
          [currentView]="currentView()"
          (toggleCollapsed)="toggleCollapsed()"
          (changeView)="setView($event)"
        />
        <div class="flex-1 flex flex-col overflow-hidden">
          <app-topbar />
          <main class="flex-1 min-w-0 overflow-y-auto"><app-notifications-page /></main>
          <app-settings-panel />
        </div>
      </div>
    } @else {
      <div class="flex h-screen bg-navy-950 overflow-hidden font-sans">
        <app-prime-sidebar
          [collapsed]="collapsed()"
          [currentView]="currentView()"
          (toggleCollapsed)="toggleCollapsed()"
          (changeView)="setView($event)"
        />
        <div class="flex-1 flex flex-col overflow-hidden">
          <app-topbar />
          <main class="flex-1 min-w-0 overflow-y-auto">
            @if (isAdminRpOrAudit()) {
              <app-dashboard-page />
            } @else {
              @switch (effectiveView()) {
                @case ('/types') {
                  <app-prime-types-page />
                }
                @case ('/rules') {
                  <app-prime-rules-page />
                }
                @case ('/results') {
                  <app-prime-results-page />
                }
                @case ('/rh/organisation') {
                  <app-organisation-management />
                }
                @case ('/validation') {
                  <app-prime-validation-page />
                }
                @case ('/chef-projet/scope') {
                  <app-chef-projet-scope-page />
                }
                @case ('/global-pool') {
                  <app-prime-global-pool-page />
                }
                @case ('/history') {
                  <app-prime-history-page />
                }
                @case ('/team-performance') {
                  <app-team-performance-page />
                }
                @case ('/configuration') {
                  <app-prime-configuration-page />
                }
                @case ('/superviseur/scope') {
                  <app-superviseur-scope-page />
                }
                @case ('/prime-saisie') {
                  @if (session.step() === 'idle') {
                    <app-prime-fiches-communes-list />
                  } @else {
                    <app-prime-saisie />
                  }
                }
                @case ('/template-manager') {
                  <app-template-manager />
                }
                @case ('/prime-fiches-pilotes') {
                  <app-prime-fiches-pilotes-page />
                }
                @case ('/prime-cellule-indicateurs') {
                  <app-prime-cellule-indicators-page />
                }
                @case ('/prime-saisie-cellule') {
                  <app-prime-saisie-cellule-page />
                }
                @case ('/notifications') {
                  <app-notifications-page />
                }
                @case ('/settings') {
                  <app-settings-page />
                }
                @case ('/employee/dashboard') {
                  <app-employee-dashboard-page />
                }
                @case ('/employee/primes') {
                  <app-my-primes-page />
                }
                @case ('/employee/performance') {
                  <app-my-performance-page />
                }
                @default {
                  <app-dashboard-page />
                }
              }
            }
          </main>
          <app-settings-panel />
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeLayoutComponent {
  readonly role = inject(RoleService);
  readonly primeSection = inject(PrimeSectionService);
  readonly session = inject(PrimeFicheSessionService);
  private readonly navRequest = inject(PrimeNavRequestService);
  private readonly primeAdmin = inject(PrimeAdminService);

  readonly collapsed = signal(false);
  readonly currentView = signal('/');

  readonly effectiveView = computed(() => {
    if (this.role.currentRole() === 'Pilote' && this.currentView() === '/') {
      return '/employee/dashboard';
    }
    return this.currentView();
  });

  /** Route loguée pour l’audit (inclut la section Admin / RP / Audit du shell). */
  readonly auditRouteLabel = computed(() => {
    const r = this.role.currentRole();
    if (r === 'Admin') return `/admin/${this.primeSection.activeAdminSection()}`;
    if (isProjectLeadRole(r)) return `/rp/${this.primeSection.activeRpSection()}`;
    if (r === 'Audit') return `/audit/${this.primeSection.activeAuditSection()}`;
    return this.effectiveView();
  });

  readonly isAuthorized = computed(() => PRIME_AUTHORIZED_ROLES.includes(this.role.currentRole()));

  /** Zone principale « dashboard embarqué » : Admin, Audit, RP legacy (shell sectionné). */
  readonly isAdminRpOrAudit = computed(() => {
    const r = this.role.currentRole();
    return r === 'Admin' || r === 'RP' || r === 'Audit';
  });

  readonly adminNotificationsShell = computed(
    () => this.role.currentRole() === 'Admin' && this.primeSection.activeAdminSection() === 'notifications',
  );
  readonly adminSettingsShell = computed(
    () => this.role.currentRole() === 'Admin' && this.primeSection.activeAdminSection() === 'settings',
  );
  readonly rpNotificationsShell = computed(
    () =>
      isProjectLeadRole(this.role.currentRole()) &&
      this.primeSection.activeRpSection() === 'notifications',
  );
  readonly rpSettingsShell = computed(
    () =>
      isProjectLeadRole(this.role.currentRole()) &&
      this.primeSection.activeRpSection() === 'settings',
  );
  readonly auditSettingsShell = computed(
    () => this.role.currentRole() === 'Audit' && this.primeSection.activeAuditSection() === 'settings',
  );
  readonly auditNotificationsShell = computed(
    () => this.role.currentRole() === 'Audit' && this.primeSection.activeAuditSection() === 'notifications',
  );

  constructor() {
    effect(() => {
      const path = this.navRequest.pendingPath();
      if (path) {
        if (isPrimePathAllowedForRole(path, this.role.currentRole())) {
          this.currentView.set(path);
        }
        this.navRequest.clearPending();
      }
    });

    effect((onCleanup) => {
      const route = this.auditRouteLabel()?.trim();
      const u = this.role.currentUser();
      const r = this.role.currentRole();
      if (!u?.id || !route) return;
      const sub = this.primeAdmin
        .recordAuditNavigation({
          userId: u.id,
          userDisplayName: `${u.firstName} ${u.lastName}`.trim(),
          role: r,
          route,
        })
        .subscribe({ error: () => {} });
      onCleanup(() => sub.unsubscribe());
    });
  }

  toggleCollapsed(): void {
    this.collapsed.update((c) => !c);
  }

  setView(v: string): void {
    if (!isPrimePathAllowedForRole(v, this.role.currentRole())) return;
    this.currentView.set(v);
  }
}
