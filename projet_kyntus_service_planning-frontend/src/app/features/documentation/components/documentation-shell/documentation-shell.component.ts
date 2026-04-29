import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Subscription, catchError, map, of } from 'rxjs';

import { DocumentationDataApiService } from '../../../../core/services/documentation-data-api.service';
import { DocumentationIdentityService } from '../../../../core/services/documentation-identity.service';
import {
  DocumentationNotificationService,
  DocumentationToast,
} from '../../../../core/services/documentation-notification.service';
import { DocumentationRealtimeService } from '../../../../core/services/documentation-realtime.service';
import { environment } from '../../../../../environments/environment';
import { mapApiRoleToDocumentationRole } from '../../lib/map-api-documentation-role';
import { AppContextService } from '../../services/app-context.service';
import { DocumentationNavigationService } from '../../services/documentation-navigation.service';
import { DevSelectorComponent } from '../dev-selector/dev-selector.component';
import { DocumentationHeaderComponent } from '../documentation-header/documentation-header.component';
import { DocumentationSidebarComponent } from '../documentation-sidebar/documentation-sidebar.component';

@Component({
  selector: 'app-documentation-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    DevSelectorComponent,
    DocumentationSidebarComponent,
    DocumentationHeaderComponent,
  ],
  templateUrl: './documentation-shell.component.html',
  styleUrl: './documentation-shell.component.css',
})
export class DocumentationShellComponent implements OnInit, OnDestroy {
  /** Bandeau dev : fixed plein Ã©cran en tÃªte â€” la sidebar se cale dessous. */
  readonly devBannerEnabled = environment.documentationDevToolsEnabled && !environment.production;
  readonly title$ = this.nav.activeTab$.pipe(
    map((tab) => this.nav.titleForActiveTab(tab, (k) => this.app.t(k))),
  );
  toast: DocumentationToast | null = null;
  private readonly sub = new Subscription();
  private forcedRoleFromQuery: 'RH' | 'Pilote' | null = null;

  constructor(
    readonly nav: DocumentationNavigationService,
    private readonly app: AppContextService,
    private readonly data: DocumentationDataApiService,
    private readonly identity: DocumentationIdentityService,
    private readonly notifications: DocumentationNotificationService,
    private readonly realtime: DocumentationRealtimeService,
  ) {}

  ngOnInit(): void {
    this.realtime.start();
    this.sub.add(this.realtime.updates$.subscribe(() => this.identity.bumpContextRevision()));

    const launch = this.readLaunchContextFromQuery();
    this.resetCachedDocumentationIdentityIfTokenUserChanged(launch.emailFromToken, launch.roleFromTokenApi);
    this.forcedRoleFromQuery = launch.forcedRole;
    if (this.forcedRoleFromQuery) {
      // Le shell Planning fournit ?doc=rh|pilot : ce paramÃ¨tre doit primer sur tout contexte local prÃ©cÃ©dent.
      this.nav.setRole(this.forcedRoleFromQuery);
    }

    this.sub.add(
      this.notifications.toast$.subscribe((toast) => {
        this.toast = toast;
      }),
    );
    this.data.getDirectoryUsers().subscribe({
      next: (list) => {
        this.identity.setDirectoryUsers(list);
        if (launch.emailFromToken) {
          const match = list.find((u) => u.email?.trim().toLowerCase() === launch.emailFromToken);
          if (match) {
            this.identity.applyProfile(match);
            this.identity.bumpContextRevision();
            if (this.forcedRoleFromQuery) {
              this.nav.syncRoleFromIdentity(this.forcedRoleFromQuery);
            } else {
              this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(match.role));
            }
          }
        }
      },
      error: () => this.identity.setDirectoryUsers([]),
    });

    const devTools = environment.documentationDevToolsEnabled && !environment.production;
    if (devTools) {
      // En mode dev local, on Ã©vite d'Ã©craser un mode explicite transmis par Planning (?doc=rh|pilot).
      if (!this.forcedRoleFromQuery) {
        const p = this.identity.profile$.value;
        if (p?.role) {
          try {
            this.nav.syncRoleFromIdentity(mapApiRoleToDocumentationRole(p.role));
          } catch {
            /* rÃ´le inconnu */
          }
        }
      }
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
    this.sub.unsubscribe();
    this.realtime.stop();
  }

  dismissToast(): void {
    this.notifications.clear();
  }

  private readLaunchContextFromQuery(): {
    forcedRole: 'RH' | 'Pilote' | null;
    emailFromToken: string | null;
    roleFromTokenApi: string | null;
  } {
    if (typeof window === 'undefined') {
      return { forcedRole: null, emailFromToken: null, roleFromTokenApi: null };
    }
    const params = new URLSearchParams(window.location.search);
    const doc = (params.get('doc') ?? '').trim().toLowerCase();
    const forcedRole = doc === 'rh' ? 'RH' : doc === 'pilot' ? 'Pilote' : null;

    const token = params.get('t') ?? '';
    if (!token) {
      return { forcedRole, emailFromToken: null, roleFromTokenApi: null };
    }
    const payload = this.decodeJwtPayload(token);
    const email =
      (payload?.['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] as string | undefined)
        ?.trim()
        ?.toLowerCase() ?? null;
    const roleFromTokenApi =
      (payload?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] as string | undefined)
        ?.trim()
        ?.toLowerCase() ?? null;
    return { forcedRole, emailFromToken: email, roleFromTokenApi };
  }

  private decodeJwtPayload(token: string): Record<string, unknown> | null {
    try {
      const [, payload] = token.split('.');
      if (!payload) return null;
      const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
      const json = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => `%${(`00${c.charCodeAt(0).toString(16)}`).slice(-2)}`)
          .join(''),
      );
      return JSON.parse(json) as Record<string, unknown>;
    } catch {
      return null;
    }
  }

  /**
   * Evite la fuite de contexte entre sessions (ex: login Fatima aprÃ¨s Yasmine).
   * Si le token courant appartient Ã  un autre email que le cache local Documentation, on purge les clÃ©s.
   */
  private resetCachedDocumentationIdentityIfTokenUserChanged(
    emailFromToken: string | null,
    roleFromTokenApi: string | null,
  ): void {
    if (typeof window === 'undefined' || !emailFromToken) {
      return;
    }
    const PROFILE_KEY = 'documentation-dev-profile-json';
    const USER_ID_KEY = 'documentation-dev-user-id';
    const USER_ROLE_KEY = 'documentation-dev-user-role';
    const SCOPE_MANAGER_KEY = 'documentation-scope-manager-id';
    const SCOPE_COACH_KEY = 'documentation-scope-coach-id';

    let cachedEmail = '';
    try {
      const raw = localStorage.getItem(PROFILE_KEY);
      if (raw) {
        const dto = JSON.parse(raw) as { email?: string };
        cachedEmail = dto.email?.trim().toLowerCase() ?? '';
      }
    } catch {
      cachedEmail = '';
    }

    if (cachedEmail && cachedEmail !== emailFromToken) {
      localStorage.removeItem(PROFILE_KEY);
      localStorage.removeItem(USER_ID_KEY);
      localStorage.removeItem(SCOPE_MANAGER_KEY);
      localStorage.removeItem(SCOPE_COACH_KEY);
      if (roleFromTokenApi) {
        localStorage.setItem(USER_ROLE_KEY, roleFromTokenApi);
      } else {
        localStorage.removeItem(USER_ROLE_KEY);
      }
      this.identity.hydrateFromStorage();
    }
  }
}
