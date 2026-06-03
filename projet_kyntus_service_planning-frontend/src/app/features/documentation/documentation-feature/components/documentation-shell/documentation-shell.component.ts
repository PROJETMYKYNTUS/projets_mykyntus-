import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { Subscription, catchError, map, of } from 'rxjs';

import { DocumentationDataApiService } from '../../../core/services/documentation-data-api.service';
import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import {
  DocumentationNotificationService,
  DocumentationToast,
} from '../../../core/services/documentation-notification.service';
import { environment } from '../../../../../../environments/environment';
import { DOCUMENTATION_ROUTE_BASE } from '../../lib/documentation-route-base';
import { mapApiRoleToDocumentationRole } from '../../lib/map-api-documentation-role';
import type { DirectoryUserDto } from '../../../shared/models/api.models';
import { AppContextService } from '../../services/app-context.service';
import { KyntusSessionService } from '../../../../../core/session/kyntus-session.service';
import { KYNTUS_DEFAULT_TENANT } from '../../../../../core/session/kyntus-session.constants';
import { mapJwtRoleToDocumentationRole } from '../../../../../core/navigation/documentation-menu.config';
import { DocumentationNavigationService } from '../../services/documentation-navigation.service';
import { DevSelectorComponent } from '../dev-selector/dev-selector.component';
import { DocumentationHeaderComponent } from '../documentation-header/documentation-header.component';

@Component({
  selector: 'app-documentation-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    DevSelectorComponent,
    DocumentationHeaderComponent,
  ],
  templateUrl: './documentation-shell.component.html',
  styleUrls: ['./documentation-shell.component.css', '../../../../../../styles-documentation.css'],
})
export class DocumentationShellComponent implements OnInit, OnDestroy {
  /** Bandeau dev : fixed plein écran en tête — la sidebar se cale dessous. */
  readonly devBannerEnabled = environment.documentationDevToolsEnabled && !environment.production;
  readonly title$ = this.nav.activeTab$.pipe(
    map((tab) => this.nav.titleForActiveTab(tab, (k) => this.app.t(k))),
  );
  toast: DocumentationToast | null = null;
  private readonly sub = new Subscription();

  constructor(
    readonly nav: DocumentationNavigationService,
    private readonly app: AppContextService,
    private readonly data: DocumentationDataApiService,
    private readonly identity: DocumentationIdentityService,
    private readonly notifications: DocumentationNotificationService,
    private readonly router: Router,
    private readonly session: KyntusSessionService,
  ) {}

  ngOnInit(): void {
    document.documentElement.classList.add('dark');
    this.sub.add(
      this.notifications.toast$.subscribe((toast) => {
        this.toast = toast;
      }),
    );

    const devTools = environment.documentationDevToolsEnabled && !environment.production;

    if (!this.identity.getTenantId()) {
      this.identity.setTenantId(KYNTUS_DEFAULT_TENANT);
    }

    const jwtRole = this.session.getRole();
    if (jwtRole) {
      this.nav.syncRoleFromIdentity(mapJwtRoleToDocumentationRole(jwtRole));
    }

    const needsFullDirectory =
      devTools ||
      !!this.identity.parsePlanningHandoffQuery() ||
      !!this.readPlanningLoginEmail();

    if (needsFullDirectory) {
      this.data.getDirectoryUsers().subscribe({
        next: (list) => {
          this.identity.setDirectoryUsers(list);
          const linked = this.tryApplyPlanningHandoff(list) || this.tryApplyPlanningLoginEmail(list);
          this.finishShellInitAfterDirectory(devTools, linked);
        },
        error: () => {
          this.identity.setDirectoryUsers([]);
          this.finishShellInitAfterDirectory(devTools, false);
        },
      });
      return;
    }

    this.data
      .getDirectoryUserMe()
      .pipe(catchError(() => of(null)))
      .subscribe((me) => {
        if (me) {
          this.identity.applyProfile(me);
          this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(me.role));
          this.identity.bumpContextRevision();
        }
      });
  }

  /** true = liaison planning appliquée (profil + route) ; false = pas de handoff ou échec (annuaire). */
  private tryApplyPlanningHandoff(users: DirectoryUserDto[]): boolean {
    const handoff = this.identity.parsePlanningHandoffQuery();
    if (!handoff) {
      return false;
    }
    const needle = handoff.email.trim().toLowerCase();
    const match = users.find((u) => (u.email ?? '').trim().toLowerCase() === needle);
    if (!match) {
      this.notifications.showError(
        `Aucun utilisateur documentation pour l’e-mail « ${handoff.email } ». Vérifiez l’annuaire (tenant / seed).`,
      );
      void this.router.navigateByUrl(this.router.url.split('?')[0] || DOCUMENTATION_ROUTE_BASE, {
        replaceUrl: true,
      });
      return false;
    }

    this.identity.applyProfile(match);

    if (handoff.handoff === 'rh') {
      try {
        this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(match.role));
      } catch {
        this.nav.syncRoleFromIdentity('RH');
      }
      void this.router.navigate([DOCUMENTATION_ROUTE_BASE, 'hr-mgmt'], { replaceUrl: true });
    } else {
      try {
        this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(match.role));
      } catch {
        this.nav.syncRoleFromIdentity('Pilote');
      }
      void this.router.navigate([DOCUMENTATION_ROUTE_BASE], { replaceUrl: true });
    }

    this.identity.bumpContextRevision();
    return true;
  }

  /** Liaison e-mail utilisateur planning (localStorage) → annuaire documentation. */
  private tryApplyPlanningLoginEmail(users: DirectoryUserDto[]): boolean {
    const email = this.readPlanningLoginEmail();
    if (!email) {
      return false;
    }
    const needle = email.trim().toLowerCase();
    const match = users.find((u) => (u.email ?? '').trim().toLowerCase() === needle);
    if (!match) {
      this.notifications.showError(
        `Aucun utilisateur documentation pour « ${email} ». Vérifiez l’annuaire (tenant atlas-tech-demo / seed Docker).`,
      );
      return false;
    }
    this.identity.applyProfile(match);
    try {
      this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(match.role));
    } catch {
      this.nav.syncRoleFromIdentity('Pilote');
    }
    this.identity.bumpContextRevision();
    return true;
  }

  private readPlanningLoginEmail(): string | null {
    const email = this.session.getEmail();
    return email.includes('@') ? email : null;
  }

  private finishShellInitAfterDirectory(devTools: boolean, handoffApplied: boolean): void {
    if (devTools) {
      const p = this.identity.profile$.value;
      if (p?.role) {
        try {
          this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(p.role));
        } catch {
          /* rôle inconnu */
        }
      }
      return;
    }

    if (handoffApplied) {
      return;
    }

    this.data
      .getDirectoryUserMe()
      .pipe(catchError(() => of(null)))
      .subscribe((me) => {
        if (me) {
          this.identity.applyProfile(me);
          this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(me.role));
          this.identity.bumpContextRevision();
        }
      });
  }

  ngOnDestroy(): void {
    document.documentElement.classList.remove('dark');
    this.sub.unsubscribe();
  }

  dismissToast(): void {
    this.notifications.clear();
  }
}
