import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import {
  KyntusThemeService,
  type KyntusTheme,
} from '../../../../core/theme/kyntus-theme.service';

export type AppTheme = KyntusTheme;

@Injectable({ providedIn: 'root' })
export class AppContextService {
  private readonly kyntusTheme = inject(KyntusThemeService);
  private readonly themeSubject = new BehaviorSubject<AppTheme>(
    this.kyntusTheme.theme(),
  );
  readonly theme$ = this.themeSubject.asObservable();

  private readonly messages: Record<string, string> = {
    'nav.dashboard': 'Tableau de bord',
    'nav.myDocs': 'Mes documents',
    'nav.requestDoc': 'Demande de document',
    'nav.requestTracking': 'Suivi des demandes',
    'nav.notifications': 'Notifications',
    'nav.settings': 'Paramètres',
    'nav.teamDocs': 'Documents de l’équipe',
    'nav.teamRequests': 'Demandes de l’équipe',
    'nav.allRequests': 'Demandes RH',
    'nav.hrDocHistory': 'Historique',
    'nav.docGen': 'Génération de documents',
    'nav.templates': 'Modèles',
    'nav.adminConfig': 'Configuration',
    'nav.docTypes': 'Types de documents',
    'nav.permissions': 'Permissions',
    'nav.workflow': 'Flux documentaire',
    'nav.storage': 'Stockage',
    'nav.auditLogs': 'Journaux des documents',
    'nav.accessHistory': 'Historique d’accès',
    'nav.personal': 'Personnel',
    'nav.interface': 'Interface',
    'nav.switchRole': 'Changer de rôle (démo)',
    'nav.logout': 'Déconnexion',
    'header.search': 'Rechercher des documents…',
    'header.role': 'Développeur senior',
    'title.dashboard': 'Tableau de bord',
    'title.myDocs': 'Mes documents',
    'title.request': 'Demander un document',
    'title.tracking': 'Suivi des demandes',
    'title.teamDocs': 'Documents de l’équipe',
    'title.teamRequests': 'Demandes de l’équipe',
    'title.hrMgmt': 'Toutes les demandes',
    'title.hrDocHistory': 'Historique des documents générés',
    'title.docGen': 'Génération de documents',
    'title.templates': 'Gestion des modèles',
    'title.adminConfig': 'Configuration générale',
    'title.docTypes': 'Types de documents',
    'title.permissions': 'Gestion des permissions',
    'title.workflow': 'Configuration du flux',
    'title.storage': 'Configuration du stockage',
    'title.auditLogs': 'Journaux des documents',
    'title.accessHistory': 'Historique d’accès',
    'title.notifications': 'Notifications',
    'title.settings': 'Paramètres',
  };

  constructor() {
    this.syncFromGlobal();
  }

  private syncFromGlobal(): void {
    const t = this.kyntusTheme.theme();
    this.themeSubject.next(t);
  }

  t(key: string): string {
    return this.messages[key] ?? key;
  }

  get theme(): AppTheme {
    return this.themeSubject.value;
  }

  toggleTheme(): void {
    this.kyntusTheme.toggleTheme();
    this.themeSubject.next(this.kyntusTheme.theme());
  }

  setTheme(theme: AppTheme): void {
    this.kyntusTheme.setTheme(theme);
    this.themeSubject.next(theme);
  }

  /** Interface en français uniquement. */
  setLanguage(_: 'fr'): void {}
}
