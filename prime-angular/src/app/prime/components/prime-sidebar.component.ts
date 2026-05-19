import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import { cn } from '@/lib/utils';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import {
  AlertCircle,
  AlertTriangle,
  Award,
  BarChart,
  BarChart3,
  Bell,
  BriefcaseBusiness,
  ChevronDown,
  ChevronRight,
  ClipboardList,
  FileSpreadsheet,
  GitBranch,
  CheckCircle,
  History,
  LayoutDashboard,
  LayoutTemplate,
  List,
  ScrollText,
  Settings,
  Shield,
  SlidersHorizontal,
  UserCircle2,
  Users,
  Workflow,
} from 'lucide';
import { RoleService } from '../state/role.service';
import { I18nService } from '../state/i18n.service';
import {
  PrimeSectionService,
  type AdminSection,
  type AuditSection,
  type RpSection,
} from '../state/prime-section.service';
import type { Role } from '../models';
import { isProjectLeadRole } from '../lib/projectLeadRole';
import type { IconNode } from 'lucide';

type NavLink = { type: 'link'; name: string; path: string; icon: IconNode; roles: Role[] };
type NavGroup = {
  type: 'group';
  name: string;
  icon: IconNode;
  roles: Role[];
  children: { name: string; path: string }[];
};
type NavPathEntry = NavLink | NavGroup;

/** Sous-routes regroupées sous « Fiche PRIME » (menu superviseur). */
const PRIME_FICHE_GROUP_PATHS = ['/prime-saisie', '/prime-fiches-pilotes', '/prime-saisie-cellule'] as const;
type RpNavItem = { id: RpSection; name: string; icon: IconNode; roles: Role[] };
type AdminNavItem = { id: AdminSection; name: string; icon: IconNode; roles: Role[] };
type AuditNavItem = { id: AuditSection; name: string; icon: IconNode };

const pathNavEntries: NavPathEntry[] = [
  {
    type: 'link',
    name: 'Tableau de bord',
    path: '/',
    icon: LayoutDashboard,
    roles: ['Admin', 'RH', 'RP', 'Chef de projet', 'Manager', 'Superviseur', 'Coach', 'Référent technique'],
  },
  { type: 'link', name: 'Mon tableau de bord', path: '/employee/dashboard', icon: UserCircle2, roles: ['Pilote'] },
  { type: 'link', name: 'Types de prime', path: '/types', icon: List, roles: ['Admin'] },
  { type: 'link', name: 'Règles', path: '/rules', icon: Settings, roles: ['Admin'] },
  {
    type: 'link',
    name: 'Résultats',
    path: '/results',
    icon: Users,
    roles: ['Admin', 'RH', 'RP', 'Chef de projet', 'Manager', 'Superviseur', 'Coach', 'Référent technique'],
  },
  {
    type: 'link',
    name: 'Affectations organisationnelles',
    path: '/rh/organisation',
    icon: GitBranch,
    roles: ['RH', 'Admin'],
  },
  {
    type: 'link',
    name: 'Validation',
    path: '/validation',
    icon: CheckCircle,
    roles: ['Admin', 'RP', 'Chef de projet', 'Superviseur', 'Coach', 'Référent technique'],
  },
  {
    type: 'link',
    name: 'Synthèse globale PRIME',
    path: '/global-pool',
    icon: FileSpreadsheet,
    roles: ['Admin', 'RH', 'Manager', 'Comptabilité'],
  },
  { type: 'link', name: 'Historique', path: '/history', icon: History, roles: ['Admin', 'RH', 'RP'] },
  {
    type: 'link',
    name: 'Performance équipe',
    path: '/team-performance',
    icon: BarChart3,
    roles: ['Manager', 'Chef de projet', 'Superviseur', 'Coach', 'Référent technique'],
  },
  { type: 'link', name: 'Périmètre superviseur', path: '/superviseur/scope', icon: BriefcaseBusiness, roles: ['Superviseur'] },
  {
    type: 'link',
    name: 'Périmètre chef de projet',
    path: '/chef-projet/scope',
    icon: BriefcaseBusiness,
    roles: ['Chef de projet', 'RP'],
  },
  {
    type: 'link',
    name: 'Indicateurs PRIME par cellule',
    path: '/prime-cellule-indicateurs',
    icon: List,
    roles: ['Superviseur'],
  },
  {
    type: 'group',
    name: 'Fiche PRIME',
    icon: FileSpreadsheet,
    roles: ['Superviseur'],
    children: [
      { name: 'Partie commune (RACC / SAV)', path: '/prime-saisie' },
      { name: 'Partie personnalisée', path: '/prime-fiches-pilotes' },
    ],
  },
  {
    type: 'link',
    name: 'Templates fiche PRIME',
    path: '/template-manager',
    icon: LayoutTemplate,
    roles: ['Superviseur', 'Admin'],
  },
  { type: 'link', name: 'Configuration', path: '/configuration', icon: SlidersHorizontal, roles: ['Admin'] },
  { type: 'link', name: 'Mes primes', path: '/employee/primes', icon: Award, roles: ['Pilote'] },
  { type: 'link', name: 'Ma performance', path: '/employee/performance', icon: BarChart3, roles: ['Pilote'] },
  {
    type: 'link',
    name: 'Notifications',
    path: '/notifications',
    icon: Bell,
    roles: ['Admin', 'RH', 'RP', 'Chef de projet', 'Manager', 'Superviseur', 'Coach', 'Référent technique', 'Pilote', 'Audit'],
  },
  {
    type: 'link',
    name: 'Paramètres',
    path: '/settings',
    icon: SlidersHorizontal,
    roles: ['Admin', 'RH', 'RP', 'Chef de projet', 'Manager', 'Superviseur', 'Coach', 'Référent technique', 'Pilote', 'Audit'],
  },
];

const rpNavItems: RpNavItem[] = [
  { id: 'dashboard', name: 'Tableau de bord', icon: LayoutDashboard, roles: ['RP'] },
  { id: 'performance', name: 'Performance équipe', icon: BarChart3, roles: ['RP'] },
  { id: 'validation', name: 'Validation finale', icon: CheckCircle, roles: ['RP'] },
  { id: 'suivi-projet', name: 'Avancement fiches PRIME', icon: ClipboardList, roles: ['RP'] },
  { id: 'notifications', name: 'Notifications', icon: Bell, roles: ['RP'] },
  { id: 'settings', name: 'Paramètres', icon: SlidersHorizontal, roles: ['RP'] },
];

const adminNavItems: AdminNavItem[] = [
  { id: 'dashboard', name: 'Dashboard système', icon: LayoutDashboard, roles: ['Admin'] },
  { id: 'access', name: 'Gestion des accès', icon: Shield, roles: ['Admin'] },
  { id: 'workflows', name: 'Configuration du flux', icon: Workflow, roles: ['Admin'] },
  { id: 'logs', name: 'Supervision & logs', icon: History, roles: ['Admin'] },
  { id: 'anomalies', name: 'Anomalies', icon: AlertCircle, roles: ['Admin'] },
  { id: 'notifications', name: 'Notifications', icon: Bell, roles: ['Admin'] },
  { id: 'settings', name: 'Paramètres', icon: SlidersHorizontal, roles: ['Admin'] },
];

const auditNavItems: AuditNavItem[] = [
  { id: 'journal', name: 'Journal d’audit', icon: ScrollText },
  { id: 'access-history', name: 'Historique d’accès', icon: Shield },
  { id: 'anomalies', name: 'Anomalies', icon: AlertTriangle },
  { id: 'reporting', name: 'Reporting', icon: BarChart },
  { id: 'notifications', name: 'Notifications', icon: Bell },
  { id: 'settings', name: 'Paramètres', icon: SlidersHorizontal },
];

@Component({
  selector: 'app-prime-sidebar',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <aside [class]="asideClass">
      <div class="h-16 flex items-center justify-between px-4 border-b border-default">
        <div class="flex items-center gap-2">
          <div class="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center shadow-sm">
            <span class="text-white font-bold text-lg">P</span>
          </div>
          @if (!collapsed) {
            <span class="text-primary font-bold text-lg tracking-tight"> PRIME </span>
          }
        </div>
        <button
          type="button"
          (click)="toggleCollapsed.emit()"
          class="text-muted hover:text-primary text-xs px-1 py-1 rounded-md hover:bg-card"
          aria-label="Réduire ou agrandir le menu"
        >
          {{ collapsed ? '»' : '«' }}
        </button>
      </div>

      <div class="flex-1 py-6 px-2 space-y-1 overflow-y-auto">
        @if (!collapsed) {
          <div class="px-2 mb-3 text-xs font-semibold text-muted uppercase tracking-wider">
            {{ i18n.t('layout.menu') }}
          </div>
        }
        @if (isProjectLeadRole(currentRole)) {
          @for (item of rpVisibleItems; track item.id) {
            <button
              type="button"
              (click)="primeSection.setActiveRpSection(item.id)"
              [class]="rpAdminBtnClass(activeRpSection === item.id)"
            >
              <app-lucide-icon [icon]="item.icon" className="w-5 h-5 transition-colors" />
              @if (!collapsed) {
                <span>{{ item.name }}</span>
              }
            </button>
          }
        }
        @if (currentRole === 'Admin') {
          @for (item of adminVisibleItems; track item.id) {
            <button
              type="button"
              (click)="primeSection.setActiveAdminSection(item.id)"
              [class]="rpAdminBtnClass(activeAdminSection === item.id)"
            >
              <app-lucide-icon [icon]="item.icon" className="w-5 h-5 transition-colors" />
              @if (!collapsed) {
                <span>{{ item.name }}</span>
              }
            </button>
          }
        }
        @if (currentRole === 'Audit') {
          @for (item of auditNavItems; track item.id) {
            <button
              type="button"
              (click)="primeSection.setActiveAuditSection(item.id)"
              [title]="collapsed ? item.name : undefined"
              [class]="rpAdminBtnClass(activeAuditSection === item.id)"
            >
              <app-lucide-icon [icon]="item.icon" className="w-5 h-5 shrink-0 transition-colors" />
              @if (!collapsed) {
                <span>{{ item.name }}</span>
              }
            </button>
          }
        }
        @if (visiblePathEntries) {
          @for (entry of visiblePathEntries; track entryTrackKey(entry)) {
            @if (entry.type === 'link') {
              <button
                type="button"
                (click)="changeView.emit(entry.path)"
                [title]="collapsed ? entry.name : undefined"
                [class]="pathBtnClass(currentView === entry.path)"
              >
                <app-lucide-icon [icon]="entry.icon" className="w-5 h-5 shrink-0 transition-colors" />
                @if (!collapsed) {
                  <span>{{ entry.name }}</span>
                }
              </button>
            } @else {
              @if (collapsed) {
                <button
                  type="button"
                  (click)="changeView.emit(entry.children[0].path)"
                  [title]="entry.name + ' — ' + entry.children[0].name"
                  [class]="pathBtnClass(isGroupChildActive(entry))"
                >
                  <app-lucide-icon [icon]="entry.icon" className="w-5 h-5 shrink-0 transition-colors" />
                </button>
              } @else {
                <div class="space-y-0.5">
                  <button
                    type="button"
                    (click)="toggleFichePrimePeek()"
                    [class]="pathBtnClass(isGroupChildActive(entry))"
                  >
                    <app-lucide-icon [icon]="entry.icon" className="w-5 h-5 shrink-0 transition-colors" />
                    <span class="min-w-0 flex-1 truncate text-left">{{ entry.name }}</span>
                    <app-lucide-icon
                      [icon]="fichePrimePeek() ? icons.chevronDown : icons.chevronRight"
                      className="w-4 h-4 shrink-0 opacity-70"
                    />
                  </button>
                  @if (fichePrimePeek() || isGroupChildActive(entry)) {
                    @for (c of entry.children; track c.path) {
                      <button
                        type="button"
                        (click)="changeView.emit(c.path)"
                        [class]="pathBtnClass(currentView === c.path) + ' py-1.5 pl-9 text-[13px]'"
                      >
                        <span class="truncate">{{ c.name }}</span>
                      </button>
                    }
                  }
                </div>
              }
            }
          }
        }
      </div>

      <div class="p-4 border-t border-default bg-sidebar/80">
        <div class="flex items-center gap-3">
          <div
            class="w-10 h-10 rounded-full bg-card border border-default shadow-sm flex items-center justify-center text-blue-500 font-bold"
          >
            {{ currentRole.substring(0, 2).toUpperCase() }}
          </div>
          @if (!collapsed) {
            <div>
              <div class="text-sm font-semibold text-primary">Utilisateur actuel</div>
              <div class="text-xs text-muted font-medium">{{ currentRole }}</div>
            </div>
          }
        </div>
      </div>
    </aside>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeSidebarComponent implements OnChanges {
  readonly role = inject(RoleService);
  readonly i18n = inject(I18nService);
  readonly primeSection = inject(PrimeSectionService);

  /** Exposé au template (même logique que pour le layout / dashboard RP). */
  protected readonly isProjectLeadRole = isProjectLeadRole;

  @Input({ required: true }) collapsed = false;
  @Input({ required: true }) currentView = '/';
  @Output() toggleCollapsed = new EventEmitter<void>();
  @Output() changeView = new EventEmitter<string>();

  /** Sous-menu « Fiche PRIME » ouvert manuellement (icône chevron). */
  readonly fichePrimePeek = signal(false);

  readonly icons = {
    chevronDown: ChevronDown,
    chevronRight: ChevronRight,
  };

  readonly auditNavItems = auditNavItems;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['currentView'] && !this.isPrimeFicheGroupPath(this.currentView)) {
      this.fichePrimePeek.set(false);
    }
  }

  get currentRole(): Role {
    return this.role.currentRole();
  }

  get activeRpSection(): RpSection {
    return this.primeSection.activeRpSection();
  }

  get activeAdminSection(): AdminSection {
    return this.primeSection.activeAdminSection();
  }

  get activeAuditSection(): AuditSection {
    return this.primeSection.activeAuditSection();
  }

  get asideClass(): string {
    return cn(
      'bg-sidebar border-r border-default h-screen flex flex-col transition-all duration-300 ease-in-out',
      this.collapsed ? 'w-20' : 'w-64',
    );
  }

  get rpVisibleItems(): RpNavItem[] {
    return rpNavItems.filter((item) => item.roles.includes(this.currentRole));
  }

  get adminVisibleItems(): AdminNavItem[] {
    return adminNavItems.filter((item) => item.roles.includes(this.currentRole));
  }

  get visiblePathEntries(): NavPathEntry[] | null {
    if (this.currentRole === 'RP' || this.currentRole === 'Admin' || this.currentRole === 'Audit') {
      return null;
    }
    return pathNavEntries.filter((item) => item.roles.includes(this.currentRole));
  }

  entryTrackKey(entry: NavPathEntry): string {
    return entry.type === 'link' ? entry.path : `group:${entry.name}`;
  }

  isGroupChildActive(g: NavGroup): boolean {
    return g.children.some((c) => c.path === this.currentView);
  }

  isPrimeFicheGroupPath(path: string): boolean {
    return (PRIME_FICHE_GROUP_PATHS as readonly string[]).includes(path);
  }

  toggleFichePrimePeek(): void {
    this.fichePrimePeek.update((v) => !v);
  }

  rpAdminBtnClass(active: boolean): string {
    return cn(
      'flex w-full items-center gap-3 rounded-lg text-sm font-medium transition-all duration-200 px-3 py-2 text-left',
      active
        ? 'bg-blue-600/15 text-blue-400 ring-1 ring-blue-500/30 border border-blue-500/35 shadow-[0_0_14px_rgba(37,99,235,0.18)]'
        : 'text-muted hover:bg-card hover:text-primary',
    );
  }

  pathBtnClass(active: boolean): string {
    return this.rpAdminBtnClass(active);
  }
}
