import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import type { DirectoryUserDto } from '../../../core/models/documentation.models';
import type { DocumentationRole } from '../interfaces/documentation-role';
import { formatOrgCompactLine, getPersonalOrgLabelsForViewer } from '../lib/personal-org-labels';
import { mapApiRoleToDocumentationRole } from '../lib/map-api-documentation-role';
import type { NotificationPreferences } from '../models/notification-preferences.model';
import { AppContextService } from '../services/app-context.service';
import { DocumentationNavigationService } from '../services/documentation-navigation.service';
import { SettingsStorageService } from '../services/settings-storage.service';
import { DocIconComponent } from '../components/doc-icon/doc-icon.component';

const ROLE_LABEL: Record<DocumentationRole, string> = {
  Pilote: 'Pilote',
  Coach: 'Coach',
  Manager: 'Manager',
  RP: 'RP',
  RH: 'RH',
  Admin: 'Administrateur',
  Audit: 'Audit',
};

const COMPACT_STORAGE_KEY = 'documentation.settings.compact.v1';

@Component({
  standalone: true,
  selector: 'app-settings-page',
  imports: [CommonModule, FormsModule, DocIconComponent],
  templateUrl: './settings-page.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsPageComponent implements OnInit {
  readonly profile$ = this.identity.profile$;
  readonly directoryUsers$ = this.identity.directoryUsers$;
  readonly role$ = this.nav.role$;

  saved = false;
  compactMode = false;
  prefs: NotificationPreferences;

  readonly notificationPrefRows: { key: keyof NotificationPreferences; label: string }[] = [
    { key: 'inApp', label: "Notifications dans l'application" },
    { key: 'email', label: 'E-mails' },
    { key: 'referrals', label: 'Nouveaux parrainages' },
    { key: 'approvals', label: 'Approbations / refus' },
    { key: 'payments', label: 'Récompenses & versements' },
    { key: 'systemAlerts', label: 'Alertes système' },
  ];

  constructor(
    readonly app: AppContextService,
    private readonly identity: DocumentationIdentityService,
    private readonly nav: DocumentationNavigationService,
    private readonly storage: SettingsStorageService,
  ) {
    this.prefs = this.storage.getNotificationPreferences();
  }

  ngOnInit(): void {
    try {
      this.compactMode = localStorage.getItem(COMPACT_STORAGE_KEY) === 'true';
    } catch {
      this.compactMode = false;
    }
  }

  roleLabelFromProfile(profile: DirectoryUserDto): string {
    try {
      return ROLE_LABEL[mapApiRoleToDocumentationRole(profile.role)];
    } catch {
      return profile.role;
    }
  }

  orgCompact(role: DocumentationRole): string {
    const profile = this.identity.profile$.value;
    if (!profile) return '';
    const users = this.identity.directoryUsers$.value;
    const org = getPersonalOrgLabelsForViewer(users, profile.id, role);
    return formatOrgCompactLine(org);
  }

  directoryUserLine(u: DirectoryUserDto): string {
    const name = [u.prenom, u.nom].filter(Boolean).join(' ').trim() || u.email || u.id;
    const role = u.role?.trim() ?? '';
    return role ? `${name} (${role})` : name;
  }

  prefEnabled(key: keyof NotificationPreferences): boolean {
    return this.prefs[key] !== false;
  }

  togglePref(key: keyof NotificationPreferences): void {
    this.prefs = { ...this.prefs, [key]: !this.prefEnabled(key) };
    this.storage.updateNotificationPreferences(this.prefs);
    this.flashSaved();
  }

  persistUi(compact: boolean): void {
    this.compactMode = compact;
    try {
      localStorage.setItem(COMPACT_STORAGE_KEY, compact ? 'true' : 'false');
    } catch {
      /* ignore */
    }
    this.flashSaved();
  }

  private flashSaved(): void {
    this.saved = true;
    setTimeout(() => {
      this.saved = false;
    }, 2000);
  }
}
