import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';

import { DocumentationIdentityService } from '../../../../core/services/documentation-identity.service';
import type { DirectoryUserDto } from '../../../../core/models/documentation.models';
import type { DocumentationRole } from '../../interfaces/documentation-role';
import { DOCUMENTATION_ROUTE_BASE } from '../../lib/documentation-route-base';
import { formatOrgCompactLine, getPersonalOrgLabelsForViewer } from '../../lib/personal-org-labels';
import { mapApiRoleToDocumentationRole } from '../../lib/map-api-documentation-role';
import { DocumentationHeaderUiService } from '../../services/documentation-header-ui.service';
import { DocumentationNavigationService } from '../../services/documentation-navigation.service';
import { DocIconComponent } from '../doc-icon/doc-icon.component';

const ROLE_LABEL: Record<DocumentationRole, string> = {
  Pilote: 'Pilote',
  Coach: 'Coach',
  Manager: 'Manager',
  RP: 'RP',
  RH: 'RH',
  Admin: 'Administrateur',
  Audit: 'Audit',
};

@Component({
  selector: 'app-documentation-settings-flyout',
  standalone: true,
  imports: [CommonModule, DocIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (ui.settingsOpen()) {
      <div class="fixed inset-0 z-50 flex justify-end">
        <div
          class="absolute inset-0 bg-black/40 backdrop-blur-[1px]"
          (click)="ui.closeSettings()"
          aria-hidden="true"
        ></div>
        <aside
          class="relative w-full max-w-md h-full bg-app border-l border-default shadow-2xl overflow-y-auto flex flex-col"
          (click)="$event.stopPropagation()"
        >
          <header
            class="px-6 py-4 border-b border-default flex items-center justify-between sticky top-0 bg-app/95 backdrop-blur-md z-10"
          >
            <div>
              <h2 class="text-lg font-semibold text-primary">Paramètres</h2>
              <p class="text-xs text-muted mt-0.5">Profil, notifications et préférences.</p>
            </div>
            <button
              type="button"
              class="text-2xl leading-none text-muted hover:text-primary"
              (click)="ui.closeSettings()"
              aria-label="Fermer"
            >
              &times;
            </button>
          </header>

          <div class="p-6 flex-1">
            <section class="bg-card border border-default rounded-xl p-4 space-y-4">
              <div class="flex items-center gap-3 pb-3 border-b border-default">
                <app-doc-icon name="user" klass="w-5 h-5 text-blue-500"></app-doc-icon>
                <h3 class="text-lg font-bold text-primary">Profil</h3>
              </div>

              @if (identity.profile$ | async; as profile) {
                <div class="space-y-4">
                  <div>
                    <h4 class="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
                      Informations personnelles
                    </h4>
                    <div class="grid grid-cols-1 sm:grid-cols-3 gap-x-4 gap-y-2 sm:gap-y-0 text-sm min-w-0">
                      <div class="min-w-0">
                        <span class="text-muted block mb-0.5">Nom</span>
                        <p class="text-primary font-medium mb-2 sm:mb-0 break-words">{{ profile.nom }}</p>
                      </div>
                      <div class="min-w-0">
                        <span class="text-muted block mb-0.5">Prénom</span>
                        <p class="text-primary font-medium mb-2 sm:mb-0 break-words">{{ profile.prenom }}</p>
                      </div>
                      <div class="min-w-0">
                        <span class="text-muted block mb-0.5">Rôle</span>
                        <p class="text-primary font-medium mb-0 break-words">{{ roleLabel(profile) }}</p>
                      </div>
                    </div>
                  </div>

                  <div class="border-t border-default pt-2">
                    <h4 class="text-xs font-semibold uppercase tracking-wider text-muted mb-1">
                      Organisation
                    </h4>
                    <p class="text-sm text-muted leading-snug">{{ orgLine(profile) || '—' }}</p>
                  </div>

                  <div class="border-t border-default pt-2">
                    <h4 class="text-xs font-semibold uppercase tracking-wider text-muted mb-2">Contact</h4>
                    <div class="space-y-2 text-sm">
                      <div>
                        <span class="text-muted block mb-0.5">E-mail</span>
                        <p class="text-primary font-medium mb-0">{{ profile.email }}</p>
                      </div>
                      <div class="rounded-lg border border-default bg-app/50 p-2">
                        <span class="text-muted block mb-0.5">Annuaire</span>
                        <p class="text-muted text-xs mt-1 leading-relaxed mb-0">
                          {{ directoryPreview() }}
                        </p>
                      </div>
                    </div>
                  </div>
                </div>

                <p class="text-xs text-muted pt-3 border-t border-default">
                  Pôles : RH · Finance · Opérations — Projets : Primes · Performance · Qualité
                </p>
              } @else {
                <p class="text-sm text-muted">Chargement du profil…</p>
              }
            </section>
          </div>

          <footer class="px-6 py-4 border-t border-default bg-app/95 sticky bottom-0">
            <button
              type="button"
              class="w-full py-2.5 rounded-lg text-sm font-medium bg-blue-600 text-white hover:bg-blue-500 transition-colors"
              (click)="openFull()"
            >
              Voir tous les paramètres
            </button>
          </footer>
        </aside>
      </div>
    }
  `,
})
export class DocumentationSettingsFlyoutComponent {
  readonly ui = inject(DocumentationHeaderUiService);
  readonly identity = inject(DocumentationIdentityService);
  private readonly nav = inject(DocumentationNavigationService);
  private readonly router = inject(Router);

  roleLabel(profile: DirectoryUserDto): string {
    try {
      return ROLE_LABEL[mapApiRoleToDocumentationRole(profile.role)];
    } catch {
      return profile.role;
    }
  }

  orgLine(profile: DirectoryUserDto): string {
    const users = this.identity.directoryUsers$.value;
    const role = this.nav.role;
    const org = getPersonalOrgLabelsForViewer(users, profile.id, role);
    return formatOrgCompactLine(org);
  }

  directoryPreview(): string {
    const users = this.identity.directoryUsers$.value;
    if (!users.length) {
      return 'Utilisateur A, Utilisateur B, Utilisateur C…';
    }
    const labels = users.slice(0, 4).map((u) => {
      const name = [u.prenom, u.nom].filter(Boolean).join(' ').trim();
      return name || u.email || u.id;
    });
    return labels.join(', ') + (users.length > 4 ? '…' : '');
  }

  async openFull(): Promise<void> {
    this.ui.closeSettings();
    await this.router.navigate([DOCUMENTATION_ROUTE_BASE, 'settings']);
  }
}
