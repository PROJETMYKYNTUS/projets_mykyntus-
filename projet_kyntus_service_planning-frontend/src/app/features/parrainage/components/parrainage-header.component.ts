import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Search } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { ParrainageNavService, type ParrainageView } from '../state/parrainage-nav.service';
import { AuditSectionService } from '../state/audit-section.service';

const VIEW_TITLES: Record<ParrainageView, string> = {
  'pilote-dashboard': 'Tableau de bord',
  'pilote-submit': 'Soumettre un parrainage',
  'pilote-referrals': 'Suivi des parrainages',
  'pilote-bonus': 'Suivi des primes',
  'rh-dashboard': 'Pilotage parrainage (RH)',
  'rh-management': 'Gestion des parrainages',
  'rh-details': 'Détail du parrainage',
  'rh-rules': 'Règles de parrainage',
  'rh-history': 'Historique',
  settings: 'Paramètres',
  notifications: 'Notifications',
  'admin-dashboard': 'Centre opérationnel',
  'admin-tools': 'Outils administrateur',
  'admin-workflow': 'Configuration du flux',
  'admin-config': 'Configuration système',
  'admin-payments': 'Paiements',
  'admin-audit': "Journal d'audit",
  'compta-payments': 'Primes à verser',
  'pm-dashboard': 'Tableau de bord équipe',
  'pm-team': "Membres de l'équipe",
  'pm-referrals': 'Suivi des parrainages',
  'pm-performance': "Performance de l'équipe",
};

const AUDIT_TITLES: Record<string, string> = {
  dashboard: 'Dashboard audit',
  journal: "Journal d'audit",
  'access-history': "Historique d'accès",
  anomalies: 'Anomalies',
  reporting: 'Reporting',
};

@Component({
  selector: 'app-parrainage-header',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <header class="h-20 px-8 flex items-center justify-between bg-app/80 backdrop-blur-md border-b border-default sticky top-0 z-40 transition-colors duration-300">
      <div>
        <h2 class="text-2xl font-bold text-primary tracking-tight">{{ title }}</h2>
      </div>
      <div class="flex items-center gap-6">
        <div class="relative group hidden md:block">
          <app-lucide-icon [icon]="searchIcon" className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted group-focus-within:text-blue-500 transition-colors" />
          <input
            type="text"
            placeholder="Rechercher…"
            class="bg-card/50 border border-default rounded-full py-2 pl-10 pr-4 text-sm text-primary focus:outline-none focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/50 w-64 transition-all placeholder:text-muted shadow-inner"
          />
        </div>
        <div class="flex items-center gap-3 pl-2 group">
          <div class="text-right hidden md:block">
            <p class="text-sm font-bold text-primary leading-none group-hover:text-blue-400 transition-colors">Parrainage</p>
            <p class="text-[10px] text-muted font-medium mt-1">Kyntus</p>
          </div>
          <div class="w-9 h-9 rounded-full bg-gradient-to-tr from-blue-600 to-blue-500 flex items-center justify-center text-white font-bold shadow-[0_0_10px_rgba(37,99,235,0.3)] border border-blue-500/30">P</div>
        </div>
      </div>
    </header>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ParrainageHeaderComponent {
  readonly nav = inject(ParrainageNavService);
  private readonly audit = inject(AuditSectionService);

  readonly searchIcon = Search;

  get title(): string {
    const v = this.nav.currentView();
    if (v === 'admin-audit') return AUDIT_TITLES[this.audit.section()] ?? "Journal d'audit";
    return VIEW_TITLES[v] ?? 'Parrainage';
  }
}
