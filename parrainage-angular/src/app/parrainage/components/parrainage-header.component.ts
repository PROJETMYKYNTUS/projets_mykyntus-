import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { ParrainageRole, ReferralNotification } from '../models/referral.model';
import { Search, Sun, Moon, Bell, Settings, CheckCircle2, FileText, XCircle } from 'lucide';
import type { IconNode } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { ThemeService } from '../state/theme.service';
import { ParrainageRoleService } from '../state/parrainage-role.service';
import { ParrainageNavService, type ParrainageView } from '../state/parrainage-nav.service';
import { AuditSectionService } from '../state/audit-section.service';
import { ReferralService } from '../services/referral.service';

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

interface DropdownItem {
  id: string;
  title: string;
  description: string;
  timestamp: string;
  read: boolean;
  icon: IconNode;
  iconColor: string;
  bgColor: string;
}

@Component({
  selector: 'app-parrainage-header',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <header class="h-20 px-8 flex items-center justify-between bg-navy-950/80 backdrop-blur-md border-b border-navy-800 sticky top-0 z-40 transition-colors duration-300">
      <div>
        <h2 class="text-2xl font-bold text-white tracking-tight">{{ title }}</h2>
      </div>
      <div class="flex items-center gap-6">
        <div class="relative group hidden md:block">
          <app-lucide-icon [icon]="searchIcon" className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500 group-focus-within:text-blue-500 transition-colors" />
          <input
            type="text"
            placeholder="Rechercher…"
            class="bg-navy-900/50 border border-navy-800 rounded-full py-2 pl-10 pr-4 text-sm text-slate-300 focus:outline-none focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/50 w-64 transition-all placeholder:text-slate-600 shadow-inner"
          />
        </div>
        <div class="flex items-center gap-3">
          <button type="button" (click)="theme.toggleTheme()" class="p-2 text-slate-400 hover:text-white transition-colors rounded-full hover:bg-navy-800" title="Thème clair ou sombre">
            @if (theme.theme() === 'dark') {
              <app-lucide-icon [icon]="sunIcon" className="w-5 h-5" />
            } @else {
              <app-lucide-icon [icon]="moonIcon" className="w-5 h-5" />
            }
          </button>
          <div class="relative">
            <button type="button" (click)="dropdownOpen.set(!dropdownOpen())" class="relative p-2 text-slate-400 hover:text-white transition-colors rounded-full hover:bg-navy-800">
              <app-lucide-icon [icon]="bellIcon" className="w-5 h-5" />
              @if (unread() > 0) {
                <span class="absolute top-1.5 right-1.5 min-w-[18px] h-[18px] px-1 rounded-full bg-blue-500/20 text-blue-400 text-[10px] font-bold border border-blue-500/30 flex items-center justify-center">{{ unread() }}</span>
              }
            </button>
            @if (dropdownOpen()) {
              <div class="absolute right-0 top-12 z-50 w-96 card-navy">
                <div class="px-4 py-3 border-b border-navy-800 flex items-center justify-between">
                  <span class="text-sm font-semibold text-white">Notifications</span>
                  <button type="button" (click)="markAllRead()" class="text-xs text-blue-400 hover:text-blue-300 font-medium">Tout lire</button>
                </div>
                <div class="max-h-80 overflow-y-auto divide-y divide-navy-800">
                  @if (items().length === 0) {
                    <div class="px-4 py-6 text-sm text-slate-400 text-center">Aucune notification</div>
                  } @else {
                    @for (n of items(); track n.id) {
                      <div class="px-4 py-3 hover:bg-navy-800/50">
                        <div class="flex items-start gap-3">
                          <div [class]="'w-8 h-8 rounded-full flex items-center justify-center shrink-0 ' + n.bgColor">
                            <app-lucide-icon [icon]="n.icon" [className]="'w-4 h-4 ' + n.iconColor" />
                          </div>
                          <div class="flex-1 min-w-0">
                            <p [class]="'text-sm ' + (n.read ? 'text-slate-300' : 'text-white font-medium')">{{ n.title }}</p>
                            <p class="text-xs text-slate-500 mt-1 line-clamp-2">{{ n.description }}</p>
                            <p class="text-[11px] text-slate-600 mt-1">{{ n.timestamp }}</p>
                          </div>
                          @if (!n.read) {
                            <button type="button" (click)="markRead(n.id)" class="text-[11px] text-blue-400 hover:text-blue-300">Lu</button>
                          }
                        </div>
                      </div>
                    }
                  }
                </div>
                <div class="px-4 py-3 border-t border-navy-800">
                  <button type="button" (click)="goToNotifications()" class="text-sm text-blue-400 hover:text-blue-300 font-medium">Fermer</button>
                </div>
              </div>
            }
          </div>
          <button type="button" (click)="nav.setView('settings')" class="p-2 text-slate-400 hover:text-white transition-colors rounded-full hover:bg-navy-800">
            <app-lucide-icon [icon]="settingsIcon" className="w-5 h-5" />
          </button>
          <div class="w-px h-6 bg-navy-800 mx-1"></div>
          <div class="flex items-center gap-3 pl-2 group">
            <div class="text-right hidden md:block">
              <p class="text-sm font-bold text-white leading-none group-hover:text-blue-400 transition-colors">Parrainage</p>
              <p class="text-[10px] text-slate-500 font-medium mt-1">MyKyntus</p>
            </div>
            <div class="w-9 h-9 rounded-full bg-gradient-to-tr from-blue-600 to-blue-500 flex items-center justify-center text-white font-bold shadow-[0_0_10px_rgba(37,99,235,0.3)] border border-blue-500/30 group-hover:shadow-[0_0_15px_rgba(37,99,235,0.5)] transition-all">P</div>
          </div>
        </div>
      </div>
    </header>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ParrainageHeaderComponent {
  readonly theme = inject(ThemeService);
  readonly nav = inject(ParrainageNavService);
  readonly role = inject(ParrainageRoleService);
  private readonly audit = inject(AuditSectionService);
  private readonly referrals = inject(ReferralService);

  readonly searchIcon = Search;
  readonly sunIcon = Sun;
  readonly moonIcon = Moon;
  readonly bellIcon = Bell;
  readonly settingsIcon = Settings;

  readonly dropdownOpen = signal(false);
  private readonly refreshTick = signal(0);

  readonly items = computed<DropdownItem[]>(() => {
    this.refreshTick();
    const u = this.role.user();
    return this.referrals
      .getNotificationsForRole(u.role, { id: u.id, projectId: u.projectId })
      .slice(0, 8)
      .map((n) => this.toDropdownItem(n));
  });

  readonly unread = computed(() => {
    this.refreshTick();
    const u = this.role.user();
    return this.referrals.getNotificationsForRole(u.role, { id: u.id, projectId: u.projectId }).filter((n) => !n.read).length;
  });

  get title(): string {
    const v = this.nav.currentView();
    if (v === 'admin-audit') return AUDIT_TITLES[this.audit.section()] ?? "Journal d'audit";
    return VIEW_TITLES[v] ?? 'Parrainage';
  }

  markAllRead(): void {
    void this.referrals.markAllNotificationsAsRead();
    this.refreshTick.update((x) => x + 1);
  }

  markRead(id: string): void {
    void this.referrals.markNotificationAsRead(id);
    this.refreshTick.update((x) => x + 1);
  }

  goToNotifications(): void {
    this.dropdownOpen.set(false);
    this.nav.setView('notifications');
  }

  private toDropdownItem(n: ReferralNotification): DropdownItem {
    const msg = n.message.toLowerCase();
    const isReject = msg.includes('reject') || msg.includes('refus') || msg.includes('rejet');
    const isReward = n.type === 'REFERRAL_REWARDED';
    const isNew = n.type === 'NEW_REFERRAL';
    let icon: IconNode = Bell;
    let iconColor = 'text-purple-500';
    let bgColor = 'bg-purple-500/10';
    if (isReject) {
      icon = XCircle;
      iconColor = 'text-red-500';
      bgColor = 'bg-red-500/10';
    } else if (isReward || isNew) {
      icon = isNew ? FileText : CheckCircle2;
      iconColor = isReward ? 'text-emerald-500' : 'text-blue-500';
      bgColor = isReward ? 'bg-emerald-500/10' : 'bg-blue-500/10';
    } else if (n.type === 'REFERRAL_PAYMENT_READY') {
      icon = CheckCircle2;
      iconColor = 'text-amber-500';
      bgColor = 'bg-amber-500/10';
    } else if (n.type === 'STATUS_CHANGED') {
      icon = Settings;
    }
    return {
      id: n.id,
      title:
        n.type === 'NEW_REFERRAL'
          ? 'Nouveau parrainage soumis'
          : n.type === 'REFERRAL_REWARDED'
            ? 'Prime de parrainage versée'
            : n.type === 'REFERRAL_PAYMENT_READY'
              ? 'Prime éligible au versement'
              : n.type === 'STATUS_CHANGED'
              ? 'Statut du parrainage mis à jour'
              : 'Notification',
      description: n.message,
      timestamp: this.toRelative(n.createdAt),
      read: n.read,
      icon,
      iconColor,
      bgColor,
    };
  }

  private toRelative(d: Date): string {
    const diffMs = Date.now() - d.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);
    if (diffMins < 1) return "À l'instant";
    if (diffMins < 60) return `Il y a ${diffMins} min`;
    if (diffHours < 24) return `Il y a ${diffHours}h`;
    if (diffDays === 1) return `Hier à ${d.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })}`;
    if (diffDays < 7) return d.toLocaleDateString('fr-FR', { weekday: 'long', day: 'numeric', month: 'short' });
    return d.toLocaleDateString('fr-FR');
  }
}
