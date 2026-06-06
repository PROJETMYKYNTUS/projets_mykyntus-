import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import type { MenuItem } from './microservices.config';
import { PrimeNavRequestService } from '../../features/prime/services/prime-nav-request.service';
import { PrimeSectionService } from '../../features/prime/state/prime-section.service';
import { ParrainageNavService } from '../../features/parrainage/state/parrainage-nav.service';
import { AuditSectionService } from '../../features/parrainage/state/audit-section.service';
import { DocumentationNavigationService } from '../../features/documentation/services/documentation-navigation.service';
import { AuditInterfaceNavService } from '../../features/documentation/services/audit-interface-nav.service';
import { mapApiRoleToDocumentationRole } from '../../features/documentation/lib/map-api-documentation-role';
import { DocumentationIdentityService } from '../../core/services/documentation-identity.service';
import { KyntusSessionService } from '../session/kyntus-session.service';
import { mapJwtRoleToDocumentationRole } from './documentation-menu.config';
import type { DocumentationTabId } from '../../features/documentation/services/documentation-navigation.service';
import type { ParrainageView } from '../../features/parrainage/state/parrainage-nav.service';
@Injectable({ providedIn: 'root' })
export class NavigationActionsService {
  private readonly router = inject(Router);
  private readonly primeNav = inject(PrimeNavRequestService);
  private readonly primeSection = inject(PrimeSectionService);
  private readonly parrainageNav = inject(ParrainageNavService);
  private readonly parrainageAudit = inject(AuditSectionService);
  private readonly docNav = inject(DocumentationNavigationService);
  private readonly docAuditNav = inject(AuditInterfaceNavService);
  private readonly docIdentity = inject(DocumentationIdentityService);
  private readonly session = inject(KyntusSessionService);

  async applyMenuItem(item: MenuItem): Promise<void> {
    if (item.externalUrl) {
      window.open(item.externalUrl, '_blank');
      return;
    }

    if (
      item.primePath !== undefined ||
      item.primeAdminSection !== undefined ||
      item.primeRpSection !== undefined ||
      item.primeAuditSection !== undefined
    ) {
      await this.applyPrimeItem(item);
      return;
    }

    if (item.parrainageView !== undefined || item.parrainageAuditSection !== undefined) {
      await this.applyParrainageItem(item);
      return;
    }

    if (item.documentationTab !== undefined || item.documentationAuditSection !== undefined) {
      await this.applyDocumentationItem(item);
      return;
    }

    if (item.route) {
      await this.router.navigateByUrl(item.route);
    }
  }

  private async applyPrimeItem(item: MenuItem): Promise<void> {
    if (!this.router.url.split('?')[0].startsWith('/prime')) {
      await this.router.navigateByUrl('/prime');
    }
    if (item.primeAdminSection !== undefined) {
      this.primeSection.setActiveAdminSection(item.primeAdminSection);
      return;
    }
    if (item.primeRpSection !== undefined) {
      this.primeSection.setActiveRpSection(item.primeRpSection);
      return;
    }
    if (item.primeAuditSection !== undefined) {
      this.primeSection.setActiveAuditSection(item.primeAuditSection);
      return;
    }
    if (item.primePath !== undefined) {
      this.primeNav.requestView(item.primePath);
    }
  }

  private async applyParrainageItem(item: MenuItem): Promise<void> {
    if (!this.router.url.split('?')[0].startsWith('/parrainage')) {
      await this.router.navigateByUrl('/parrainage');
    }
    if (item.parrainageAuditSection !== undefined) {
      this.parrainageAudit.setSection(item.parrainageAuditSection);
      this.parrainageNav.setView('admin-audit');
      return;
    }
    if (item.parrainageView !== undefined) {
      this.parrainageNav.setView(item.parrainageView);
    }
  }

  private async applyDocumentationItem(item: MenuItem): Promise<void> {
    this.syncDocumentationNavRole();

    const onDoc = this.router.url.split('?')[0].startsWith('/documentation');
    if (!onDoc) {
      const target = item.route ?? '/documentation';
      await this.router.navigateByUrl(target);
      return;
    }

    if (item.documentationAuditSection !== undefined) {
      this.docAuditNav.setSection(item.documentationAuditSection);
      this.docNav.navigateToTab('audit-logs');
      return;
    }
    if (item.documentationTab !== undefined) {
      this.docNav.navigateToTab(item.documentationTab);
      return;
    }
    if (item.route) {
      await this.router.navigateByUrl(item.route);
    }
  }

  async openPrimeNotifications(): Promise<void> {
    await this.router.navigateByUrl('/notifications');
  }

  async openPrimeSettings(): Promise<void> {
    await this.router.navigateByUrl('/settings');
  }

  async openParrainageNotifications(): Promise<void> {
    await this.router.navigateByUrl('/notifications');
  }

  async openParrainageSettings(): Promise<void> {
    await this.router.navigateByUrl('/settings');
  }

  async openDocumentationTab(tab: DocumentationTabId): Promise<void> {
    await this.applyDocumentationItem({
      label: tab,
      route: '/documentation',
      documentationTab: tab,
    });
  }

  async openDocumentationSettings(): Promise<void> {
    await this.router.navigateByUrl('/settings');
  }

  async openPrimeConfiguration(): Promise<void> {
    await this.applyPrimeItem({
      label: 'Configuration',
      route: '/prime',
      primePath: '/configuration',
    });
  }

  async openParrainageAdminConfig(): Promise<void> {
    await this.applyParrainageItem({
      label: 'Configuration système',
      route: '/parrainage',
      parrainageView: 'admin-config',
    });
  }

  async openPrimePath(primePath: string): Promise<void> {
    await this.applyPrimeItem({
      label: 'PRIME',
      route: '/prime',
      primePath,
    });
  }

  async openParrainageView(view: ParrainageView): Promise<void> {
    await this.applyParrainageItem({
      label: 'Parrainage',
      route: '/parrainage',
      parrainageView: view,
    });
  }

  /** Rôle documentation immédiat (profil annuaire ou JWT) — évite Pilote par défaut avant le chargement de l’annuaire. */
  private syncDocumentationNavRole(): void {
    const prof = this.docIdentity.profile$.value;
    if (prof?.role) {
      try {
        this.docNav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(prof.role));
      } catch {
        /* ignore */
      }
      return;
    }
    const jwtRole = this.session.getRole();
    if (jwtRole) {
      this.docNav.syncRoleFromIdentity(mapJwtRoleToDocumentationRole(jwtRole));
    }
  }
}
