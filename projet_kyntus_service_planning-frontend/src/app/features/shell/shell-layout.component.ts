import { Component, OnDestroy, OnInit, ViewEncapsulation, inject } from '@angular/core';

import { CommonModule } from '@angular/common';

import { RouterModule, Router, NavigationEnd } from '@angular/router';

import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

import { filter } from 'rxjs/operators';
import { Bell, Moon, Settings, Sun } from 'lucide';

import { Microservice, MenuItem } from '../../core/navigation/microservices.config';

import { NavigationMenuService } from '../../core/navigation/navigation-menu.service';

import { NavigationActionsService } from '../../core/navigation/navigation-actions.service';

import { AuthService } from '../../core/services/auth.service';
import { KyntusThemeService } from '../../core/theme/kyntus-theme.service';
import { KyntusNotificationHubService } from '../../core/notifications/kyntus-notification-hub.service';
import { KyntusShellUiService } from '../../core/notifications/kyntus-shell-ui.service';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { ShellNotificationBadgeComponent } from '../../shared/shell-controls/notification-badge.component';
import { ShellNotificationDropdownComponent } from '../../shared/shell-controls/notification-dropdown.component';
import { ShellSettingsPanelComponent } from '../../shared/shell-controls/settings-panel.component';

import { PrimeNavRequestService } from '../prime/services/prime-nav-request.service';

import { PrimeSectionService } from '../prime/state/prime-section.service';

import { KyntusSessionService } from '../../core/session/kyntus-session.service';
import { mapJwtRoleToPrimeRole } from '../../core/session/kyntus-role-ui.config';
import type { Role as PrimeRole } from '../prime/models';

import { ParrainageNavService } from '../parrainage/state/parrainage-nav.service';

import { AuditSectionService } from '../parrainage/state/audit-section.service';

import { isProjectLeadRole } from '../prime/lib/projectLeadRole';

import { DocumentationNavigationService } from '../documentation/documentation-feature/services/documentation-navigation.service';

import { AuditInterfaceNavService } from '../documentation/documentation-feature/services/audit-interface-nav.service';



@Component({

  selector: 'app-shell-layout',

  standalone: true,

  imports: [
    CommonModule,
    RouterModule,
    LucideIconComponent,
    ShellNotificationBadgeComponent,
    ShellNotificationDropdownComponent,
    ShellSettingsPanelComponent,
  ],

  templateUrl: './shell-layout.component.html',

  styleUrls: ['./shell-layout.component.css'],

  encapsulation: ViewEncapsulation.None,

})

export class ShellLayoutComponent implements OnInit, OnDestroy {

  private readonly router = inject(Router);

  private readonly auth = inject(AuthService);

  private readonly sanitizer = inject(DomSanitizer);

  private readonly menuService = inject(NavigationMenuService);

  private readonly navActions = inject(NavigationActionsService);

  private readonly primeNav = inject(PrimeNavRequestService);

  private readonly primeSection = inject(PrimeSectionService);

  private readonly session = inject(KyntusSessionService);

  private readonly parrainageNav = inject(ParrainageNavService);

  private readonly parrainageAudit = inject(AuditSectionService);

  private readonly docNav = inject(DocumentationNavigationService);

  private readonly docAuditNav = inject(AuditInterfaceNavService);

  readonly theme = inject(KyntusThemeService);
  readonly hub = inject(KyntusNotificationHubService);
  private readonly shellUi = inject(KyntusShellUiService);

  readonly icons = { bell: Bell, settings: Settings, moon: Moon, sun: Sun };

  currentUser: any = null;

  role = '';

  sidebarOpen = false;
  sidebarCollapsed = false;
  logoError = false;

  moduleContentClass = '';



  readonly homeIcon =

    '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>';



  groups: Microservice[] = [];

  openGroups = new Set<string>();

  private subDocRole?: { unsubscribe(): void };



  private iconCache = new Map<string, SafeHtml>();



  ngOnInit(): void {

    const userStr = localStorage.getItem('user');

    if (userStr) {

      try {

        this.currentUser = JSON.parse(userStr);

      } catch {

        this.currentUser = null;

      }

    }

    this.role = (this.auth.getRole() || this.currentUser?.role || '').trim();

    if (!localStorage.getItem('token')) {

      window.location.href = 'http://localhost:8201/login';

      return;

    }



    this.refreshGroups();

    this.updateModuleClass(this.router.url);



    this.router.events

      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))

      .subscribe((e) => {

        this.refreshGroups();

        this.updateModuleClass(e.urlAfterRedirects);

        this.openGroupForUrl(e.urlAfterRedirects);

      });

  }



  private refreshGroups(): void {

    this.groups = this.menuService.buildVisibleGroups(this.role);

    this.openGroupForUrl(this.router.url);

  }



  private updateModuleClass(url: string): void {

    const path = url.split('?')[0];

    if (/^\/prime(\/|$)/.test(path)) {

      this.moduleContentClass = 'module-prime';

    } else if (/^\/parrainage(\/|$)/.test(path)) {

      this.moduleContentClass = 'module-parrainage';

    } else if (/^\/documentation(\/|$)/.test(path)) {

      this.moduleContentClass = 'module-documentation';

    } else {

      this.moduleContentClass = '';

    }

  }



  private openGroupForUrl(url: string): void {

    const path = url.split('?')[0];

    for (const g of this.groups) {

      const matchChild = g.children.some((c) => {

        if (c.route && path.startsWith(c.route)) return true;

        return false;

      });

      if (
        matchChild ||
        (g.id === 'prime' && path.startsWith('/prime')) ||
        (g.id === 'parrainage' && path.startsWith('/parrainage')) ||
        (g.id === 'documentation' && path.startsWith('/documentation'))
      ) {

        this.openGroups.add(g.id);

      }

    }

  }



  toggleGroup(id: string): void {

    if (this.openGroups.has(id)) this.openGroups.delete(id);

    else this.openGroups.add(id);

  }



  isOpen(id: string): boolean {

    return this.openGroups.has(id);

  }



  isItemActive(item: MenuItem): boolean {

    const path = this.router.url.split('?')[0];



    if (item.parrainageView !== undefined && path.startsWith('/parrainage')) {

      if (item.parrainageAuditSection !== undefined) {

        return (

          this.parrainageNav.currentView() === 'admin-audit' &&

          this.parrainageAudit.section() === item.parrainageAuditSection

        );

      }

      return this.parrainageNav.currentView() === item.parrainageView;

    }

    if (
      (item.documentationTab !== undefined || item.documentationAuditSection !== undefined) &&
      path.startsWith('/documentation')
    ) {
      if (item.documentationAuditSection !== undefined) {
        const tab = this.docNav.activeTab$.value;
        return tab === 'audit-logs' && this.docAuditNav.section === item.documentationAuditSection;
      }
      if (item.documentationTab !== undefined) {
        return this.docNav.activeTab$.value === item.documentationTab;
      }
    }

    if (

      (item.primePath !== undefined ||

        item.primeAdminSection !== undefined ||

        item.primeRpSection !== undefined ||

        item.primeAuditSection !== undefined) &&

      path.startsWith('/prime')

    ) {

      const role: PrimeRole = mapJwtRoleToPrimeRole(this.session.getRole()) ?? 'Superviseur';

      if (item.primeAdminSection !== undefined) {

        return role === 'Admin' && this.primeSection.activeAdminSection() === item.primeAdminSection;

      }

      if (item.primeRpSection !== undefined) {

        return isProjectLeadRole(role) && this.primeSection.activeRpSection() === item.primeRpSection;

      }

      if (item.primeAuditSection !== undefined) {

        return role === 'Audit' && this.primeSection.activeAuditSection() === item.primeAuditSection;

      }

      if (item.primePath !== undefined) {

        return this.primeNav.activePath() === item.primePath;

      }

    }



    return !!item.route && path.startsWith(item.route);

  }



  async onItemClick(item: MenuItem): Promise<void> {

    this.sidebarOpen = false;

    await this.navActions.applyMenuItem(item);

    this.refreshGroups();

    this.openGroupForUrl(this.router.url);

  }



  icon(svg: string): SafeHtml {

    let cached = this.iconCache.get(svg);

    if (!cached) {

      cached = this.sanitizer.bypassSecurityTrustHtml(svg);

      this.iconCache.set(svg, cached);

    }

    return cached;

  }



  get userInitials(): string {

    const name: string = this.currentUser?.username || 'KY';

    return name.substring(0, 2).toUpperCase();

  }



  toggleSidebar(): void {

    this.sidebarOpen = !this.sidebarOpen;

  }

  toggleCollapse(): void {
    this.sidebarCollapsed = !this.sidebarCollapsed;
  }

  toggleNotifDropdown(): void {
    this.shellUi.toggleDropdown();
    if (this.shellUi.dropdownOpen()) {
      this.hub.refreshContracts();
    }
  }

  openNotifications(): void {
    if (!this.shellUi.dropdownOpen()) {
      this.shellUi.toggleDropdown();
    }
    this.hub.refreshContracts();
  }

  openSettings(): void {
    this.shellUi.openSettings();
  }



  logout(): void {

    localStorage.clear();

    window.location.href = 'http://localhost:8201/login';

  }

  ngOnDestroy(): void {
    this.subDocRole?.unsubscribe();
  }

}


