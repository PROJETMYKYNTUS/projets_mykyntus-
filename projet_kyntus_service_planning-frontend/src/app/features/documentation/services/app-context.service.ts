import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type AppTheme = 'dark' | 'light';

@Injectable({ providedIn: 'root' })
export class AppContextService {
  private readonly themeSubject = new BehaviorSubject<AppTheme>('dark');
  readonly theme$ = this.themeSubject.asObservable();

  private readonly messages: Record<string, string> = {
    'nav.dashboard': 'Tableau de bord',
    'nav.myDocs': 'Mes documents',
    'nav.requestDoc': 'Demande de document',
    'nav.requestTracking': 'Suivi des demandes',
    'nav.notifications': 'Notifications',
    'nav.settings': 'ParamÃ¨tres',
    'nav.teamDocs': 'Documents de lâ€™Ã©quipe',
    'nav.teamRequests': 'Demandes de lâ€™Ã©quipe',
    'nav.allRequests': 'Demandes RH',
    'nav.hrDocHistory': 'Historique',
    'nav.docGen': 'GÃ©nÃ©ration de documents',
    'nav.templates': 'ModÃ¨les',
    'nav.adminConfig': 'Configuration',
    'nav.docTypes': 'Types de documents',
    'nav.permissions': 'Permissions',
    'nav.workflow': 'Flux documentaire',
    'nav.storage': 'Stockage',
    'nav.auditLogs': 'Journaux des documents',
    'nav.accessHistory': 'Historique dâ€™accÃ¨s',
    'nav.personal': 'Personnel',
    'nav.interface': 'Interface',
    'nav.switchRole': 'Changer de rÃ´le (dÃ©mo)',
    'nav.logout': 'DÃ©connexion',
    'header.search': 'Rechercher des documentsâ€¦',
    'header.role': 'DÃ©veloppeur senior',
    'title.dashboard': 'Tableau de bord',
    'title.myDocs': 'Mes documents',
    'title.request': 'Demander un document',
    'title.tracking': 'Suivi des demandes',
    'title.teamDocs': 'Documents de lâ€™Ã©quipe',
    'title.teamRequests': 'Demandes de lâ€™Ã©quipe',
    'title.hrMgmt': 'Toutes les demandes',
    'title.hrDocHistory': 'Historique des documents gÃ©nÃ©rÃ©s',
    'title.docGen': 'GÃ©nÃ©ration de documents',
    'title.templates': 'Gestion des modÃ¨les',
    'title.adminConfig': 'Configuration gÃ©nÃ©rale',
    'title.docTypes': 'Types de documents',
    'title.permissions': 'Gestion des permissions',
    'title.workflow': 'Configuration du flux',
    'title.storage': 'Configuration du stockage',
    'title.auditLogs': 'Journaux des documents',
    'title.accessHistory': 'Historique dâ€™accÃ¨s',
    'title.notifications': 'Notifications',
    'title.settings': 'ParamÃ¨tres',
  };

  constructor() {
    this.applyThemeToDocument(this.themeSubject.value);
  }

  t(key: string): string {
    return this.messages[key] ?? key;
  }

  get theme(): AppTheme {
    return this.themeSubject.value;
  }

  toggleTheme(): void {
    const next = this.themeSubject.value === 'dark' ? 'light' : 'dark';
    this.themeSubject.next(next);
    this.applyThemeToDocument(next);
  }

  /** Interface en franÃ§ais uniquement. */
  setLanguage(_: 'fr'): void {}

  private applyThemeToDocument(theme: AppTheme): void {
    if (typeof document === 'undefined') return;
    document.documentElement.lang = 'fr';
    document.documentElement.classList.toggle('dark', theme === 'dark');
  }
}
