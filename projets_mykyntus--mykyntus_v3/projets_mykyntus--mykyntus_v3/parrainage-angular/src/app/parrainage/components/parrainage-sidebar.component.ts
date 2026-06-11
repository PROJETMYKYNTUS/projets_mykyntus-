import { ChangeDetectionStrategy, Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { FileText, Menu, ChevronLeft, LayoutDashboard, ScrollText, Shield, AlertTriangle, BarChart3, Bell, SlidersHorizontal } from 'lucide';
import type { IconNode } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { ParrainageRoleService } from '../state/parrainage-role.service';
import { ParrainageNavService, type ParrainageView } from '../state/parrainage-nav.service';
import { AuditSectionService, type AuditSectionId } from '../state/audit-section.service';
import type { ParrainageRole } from '../models/referral.model';

type NavItem = { to: ParrainageView; label: string };
type AuditNavTone = 'emerald' | 'blue' | 'cyan' | 'amber' | 'violet';

const AUDIT_NAV_TONE_ACTIVE: Record<AuditNavTone, string> = {
  emerald: 'border-emerald-500/50 ring-emerald-500/30 bg-emerald-600/15 text-emerald-300 shadow-[0_0_14px_rgba(16,185,129,0.2)]',
  blue: 'border-blue-500/50 ring-blue-500/30 bg-blue-600/15 text-blue-300 shadow-[0_0_14px_rgba(37,99,235,0.18)]',
  cyan: 'border-cyan-500/50 ring-cyan-500/30 bg-cyan-600/15 text-cyan-300 shadow-[0_0_14px_rgba(6,182,212,0.18)]',
  amber: 'border-amber-500/50 ring-amber-500/30 bg-amber-600/15 text-amber-200 shadow-[0_0_14px_rgba(245,158,11,0.18)]',
  violet: 'border-violet-500/50 ring-violet-500/30 bg-violet-600/15 text-violet-200 shadow-[0_0_14px_rgba(139,92,246,0.2)]',
};

const RH_ITEMS: NavItem[] = [
  { to: 'rh-dashboard', label: 'Tableau de bord' },
  { to: 'rh-management', label: 'Gestion des parrainages' },
  { to: 'rh-rules', label: 'Règles de parrainage' },
  { to: 'rh-history', label: 'Historique' },
  { to: 'notifications', label: 'Notifications' },
  { to: 'settings', label: 'Paramètres' },
  { to: 'admin-config', label: 'Configuration système' },
];
const PILOTE_ITEMS: NavItem[] = [
  { to: 'pilote-dashboard', label: 'Tableau de bord' },
  { to: 'pilote-submit', label: 'Soumettre un parrainage' },
  { to: 'pilote-referrals', label: 'Suivi des parrainages' },
  { to: 'pilote-bonus', label: 'Suivi des primes' },
  { to: 'notifications', label: 'Notifications' },
  { to: 'settings', label: 'Paramètres' },
];
const ADMIN_ITEMS: NavItem[] = [
  { to: 'admin-dashboard', label: 'Centre opérationnel' },
  { to: 'admin-tools', label: 'Outils administrateur' },
  { to: 'admin-workflow', label: 'Configuration du flux' },
  { to: 'admin-config', label: 'Configuration système' },
  { to: 'admin-payments', label: 'Paiements' },
  { to: 'notifications', label: 'Notifications' },
  { to: 'settings', label: 'Paramètres' },
  { to: 'admin-audit', label: "Journal d'audit" },
];
const PM_ITEMS: NavItem[] = [
  { to: 'pm-dashboard', label: "Tableau de bord équipe" },
  { to: 'pm-team', label: "Membres de l'équipe" },
  { to: 'pm-referrals', label: 'Parrainages' },
  { to: 'pm-performance', label: 'Performance' },
  { to: 'notifications', label: 'Notifications' },
  { to: 'settings', label: 'Paramètres' },
];

const COMPTA_ITEMS: NavItem[] = [
  { to: 'compta-payments', label: 'Primes à verser' },
  { to: 'notifications', label: 'Notifications' },
  { to: 'settings', label: 'Paramètres' },
];
const ROLE_OPTIONS: Array<{ code: ParrainageRole; label: string }> = [
  { code: 'PILOTE', label: 'Pilote' },
  { code: 'COACH', label: 'Coach' },
  { code: 'MANAGER', label: 'Manager' },
  { code: 'RP', label: 'Responsable projet' },
  { code: 'RH', label: 'RH' },
  { code: 'COMPTA', label: 'Comptabilité' },
  { code: 'ADMIN', label: 'Administrateur' },
  { code: 'AUDIT', label: 'Audit' },
];

const ROLE_CHIPS: Array<{ code: ParrainageRole; short: string; title: string }> = [
  { code: 'PILOTE', short: 'Pl', title: 'Pilote' },
  { code: 'COACH', short: 'Co', title: 'Coach' },
  { code: 'MANAGER', short: 'Mg', title: 'Manager' },
  { code: 'RP', short: 'RP', title: 'RP' },
  { code: 'RH', short: 'RH', title: 'RH' },
  { code: 'COMPTA', short: 'Cp', title: 'Comptabilité' },
  { code: 'ADMIN', short: 'Ad', title: 'Administrateur' },
  { code: 'AUDIT', short: 'Au', title: 'Audit' },
];

const ROLE_LABELS: Record<ParrainageRole, string> = {
  RH: 'RH',
  PILOTE: 'Pilote',
  ADMIN: 'Administrateur',
  COACH: 'Coach',
  MANAGER: 'Manager',
  RP: 'Responsable projet',
  AUDIT: 'Audit',
  COMPTA: 'Comptabilité',
};

interface AuditNavEntry {
  key: string;
  label: string;
  icon: IconNode;
  tone: AuditNavTone;
  section: AuditSectionId;
}

@Component({
  selector: 'app-parrainage-sidebar',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div [class]="'h-screen bg-navy-900 border-r border-navy-800 flex flex-col fixed left-0 top-0 z-50 transition-all duration-300 ' + (isCollapsed ? 'w-[70px]' : 'w-64')">
      <div [class]="'p-4 flex items-center gap-3 ' + (isCollapsed ? 'justify-center' : 'justify-between')">
        @if (!isCollapsed) {
          <div class="flex items-center gap-3 overflow-hidden">
            <div class="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center shrink-0 shadow-[0_0_10px_rgba(37,99,235,0.5)]">
              <app-lucide-icon [icon]="fileIcon" className="text-white w-5 h-5" />
            </div>
            <h1 class="text-xl font-bold tracking-tight text-white whitespace-nowrap">MyKyntus</h1>
          </div>
        }
        <button type="button" (click)="toggleCollapsed.emit(!isCollapsed)" class="p-2 text-slate-400 hover:text-white hover:bg-navy-800 rounded-lg transition-colors shrink-0">
          <app-lucide-icon [icon]="isCollapsed ? menuIcon : chevronIcon" className="w-5 h-5" />
        </button>
      </div>

      <nav class="flex-1 px-3 space-y-1 overflow-y-auto pb-4 mt-4">
        @if (role.user().role === 'AUDIT') {
          <div class="space-y-1">
            @for (item of auditNavItems; track item.key) {
              <button
                type="button"
                (click)="selectAudit(item.section)"
                [title]="isCollapsed ? item.label : null"
                [class]="'w-full flex items-center ' + (isCollapsed ? 'justify-center px-0' : 'gap-3 px-3') + ' py-2.5 rounded-lg transition-all duration-200 border ' + (isAuditActive(item.section) ? ('ring-1 ' + auditTone[item.tone]) : 'border-transparent text-slate-400 hover:bg-navy-800 hover:text-slate-200')"
              >
                <app-lucide-icon [icon]="item.icon" [className]="'w-5 h-5 shrink-0 ' + (isAuditActive(item.section) ? '' : 'opacity-80')" />
                @if (!isCollapsed) { <span class="font-medium text-sm whitespace-nowrap">{{ item.label }}</span> }
              </button>
            }
            <div class="pt-3 mt-2 border-t border-navy-800 space-y-1">
              <button
                type="button"
                (click)="nav.setView('notifications')"
                [title]="isCollapsed ? 'Notifications' : null"
                [class]="'w-full flex items-center ' + (isCollapsed ? 'justify-center px-0' : 'gap-3 px-3') + ' py-2.5 rounded-lg transition-all duration-200 ' + (nav.currentView() === 'notifications' ? 'bg-blue-600/15 text-blue-400 border border-blue-500/40 ring-1 ring-blue-500/30' : 'text-slate-400 hover:bg-navy-800 hover:text-slate-200')"
              >
                <app-lucide-icon [icon]="bellIcon" className="w-5 h-5 shrink-0" />
                @if (!isCollapsed) { <span class="font-medium text-sm whitespace-nowrap">Notifications</span> }
              </button>
              <button
                type="button"
                (click)="nav.setView('settings')"
                [title]="isCollapsed ? 'Paramètres' : null"
                [class]="'w-full flex items-center ' + (isCollapsed ? 'justify-center px-0' : 'gap-3 px-3') + ' py-2.5 rounded-lg transition-all duration-200 ' + (nav.currentView() === 'settings' ? 'bg-blue-600/15 text-blue-400 border border-blue-500/40 ring-1 ring-blue-500/30' : 'text-slate-400 hover:bg-navy-800 hover:text-slate-200')"
              >
                <app-lucide-icon [icon]="slidersIcon" className="w-5 h-5 shrink-0" />
                @if (!isCollapsed) { <span class="font-medium text-sm whitespace-nowrap">Paramètres</span> }
              </button>
            </div>
          </div>
        } @else {
          @for (item of navItems; track item.to + item.label) {
            <button
              type="button"
              (click)="nav.setView(item.to)"
              [title]="isCollapsed ? item.label : null"
              [class]="'w-full flex items-center ' + (isCollapsed ? 'justify-center px-0' : 'gap-3 px-3') + ' py-2.5 rounded-lg transition-all duration-200 ' + (nav.currentView() === item.to ? 'bg-blue-600/15 text-blue-400 border border-blue-500/40 ring-1 ring-blue-500/30 shadow-[0_0_14px_rgba(37,99,235,0.18)]' : 'text-slate-400 hover:bg-navy-800 hover:text-slate-200')"
            >
              @if (!isCollapsed) { <span class="font-medium text-sm whitespace-nowrap">{{ item.label }}</span> }
              @if (isCollapsed) { <span class="text-xs font-medium">{{ item.label[0] }}</span> }
            </button>
          }
        }
      </nav>

      <div [class]="'p-4 border-t border-navy-800 ' + (isCollapsed ? 'flex flex-col items-stretch gap-2' : '')">
        @if (!isCollapsed) {
          <div class="card-navy p-4 md:p-5 space-y-3">
            <h3 class="text-sm font-semibold text-slate-50">Rôle (démo)</h3>
            <select
              [value]="role.user().role"
              (change)="onRoleSelect($event)"
              class="w-full rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50 cursor-pointer transition-colors"
            >
              @for (opt of roleOptions; track opt.code) {
                <option [value]="opt.code">{{ opt.label }}</option>
              }
            </select>
            <p class="text-xs text-slate-400">Rôle actuel : <span class="font-medium text-slate-200">{{ roleLabel }}</span></p>
          </div>
        }
        @if (isCollapsed) {
          <div class="card-navy p-2 space-y-2">
            <p class="text-[9px] uppercase tracking-wide text-slate-500 text-center leading-tight">Rôle</p>
            <div class="flex flex-wrap gap-1 justify-center">
              @for (chip of roleChips; track chip.code) {
                <button
                  type="button"
                  (click)="changeRole(chip.code)"
                  [title]="chip.title"
                  [class]="'min-w-[1.75rem] rounded-lg border px-1.5 py-1.5 text-[9px] font-medium transition-colors ' + (chip.code === role.user().role ? 'border-blue-500/40 bg-blue-600/15 text-blue-400 ring-1 ring-blue-500/30 shadow-[0_0_10px_rgba(37,99,235,0.12)]' : 'border-navy-800 bg-navy-900/60 text-slate-400 hover:border-navy-700 hover:bg-navy-800/40 hover:text-slate-200')"
                >
                  {{ chip.short }}
                </button>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ParrainageSidebarComponent {
  readonly role = inject(ParrainageRoleService);
  readonly nav = inject(ParrainageNavService);
  readonly audit = inject(AuditSectionService);

  @Input() isCollapsed = false;
  @Input() currentView: ParrainageView = 'pilote-dashboard';
  @Output() toggleCollapsed = new EventEmitter<boolean>();

  readonly fileIcon = FileText;
  readonly menuIcon = Menu;
  readonly chevronIcon = ChevronLeft;
  readonly bellIcon = Bell;
  readonly slidersIcon = SlidersHorizontal;
  readonly auditTone = AUDIT_NAV_TONE_ACTIVE;
  readonly roleOptions = ROLE_OPTIONS;
  readonly roleChips = ROLE_CHIPS;

  readonly auditNavItems: AuditNavEntry[] = [
    { key: 'dash', label: 'Dashboard audit', icon: LayoutDashboard, tone: 'emerald', section: 'dashboard' },
    { key: 'journal', label: "Journal d'audit", icon: ScrollText, tone: 'blue', section: 'journal' },
    { key: 'access', label: "Historique d'accès", icon: Shield, tone: 'cyan', section: 'access-history' },
    { key: 'anom', label: 'Anomalies', icon: AlertTriangle, tone: 'amber', section: 'anomalies' },
    { key: 'report', label: 'Reporting', icon: BarChart3, tone: 'violet', section: 'reporting' },
  ];

  get navItems(): NavItem[] {
    const r = this.role.user().role;
    if (r === 'RH') return RH_ITEMS;
    if (r === 'COMPTA') return COMPTA_ITEMS;
    if (r === 'ADMIN') return ADMIN_ITEMS;
    if (r === 'MANAGER' || r === 'COACH' || r === 'RP') return PM_ITEMS;
    return PILOTE_ITEMS;
  }

  get roleLabel(): string {
    return ROLE_LABELS[this.role.user().role];
  }

  isAuditActive(section: AuditSectionId): boolean {
    return this.nav.currentView() === 'admin-audit' && this.audit.section() === section;
  }

  selectAudit(section: AuditSectionId): void {
    this.audit.setSection(section);
    this.nav.setView('admin-audit');
  }

  onRoleSelect(event: Event): void {
    this.changeRole((event.target as HTMLSelectElement).value as ParrainageRole);
  }

  changeRole(roleCode: ParrainageRole): void {
    this.role.loginAsRole(roleCode);
    this.nav.onRoleChanged();
  }
}
