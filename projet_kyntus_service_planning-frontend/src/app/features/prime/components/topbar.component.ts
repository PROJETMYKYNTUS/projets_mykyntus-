import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RoleService } from '../state/role.service';
import { ThemeService } from '../state/theme.service';
import { I18nService } from '../state/i18n.service';
import { NotificationUiService } from '../state/notification-ui.service';
import { PRIME_AUTHORIZED_ROLES, Role } from '../models';
import { NotificationDropdownComponent } from './notification-dropdown.component';
import { NotificationBadgeComponent } from './notification-badge.component';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { Bell, Moon, Search, Settings, Shield, Sun } from 'lucide';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [
    LucideIconComponent,
    NotificationDropdownComponent,
    NotificationBadgeComponent,
  ],
  template: `
    <header class="h-16 glass flex items-center justify-between px-6 z-10 sticky top-0">
      <div class="flex items-center gap-4 flex-1">
        <div class="relative w-64 hidden md:block">
          <span class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
            <app-lucide-icon [icon]="icons.search" className="w-4 h-4" />
          </span>
          <input
            type="text"
            [placeholder]="i18n.t('topbar.search.placeholder')"
            class="w-full pl-9 pr-4 py-2 bg-navy-900/50 border border-navy-800 rounded-full text-sm text-slate-300 focus:outline-none focus:ring-1 focus:ring-blue-500/50 focus:border-blue-500/50 transition-all placeholder:text-slate-600"
          />
        </div>
      </div>

      <div class="flex items-center gap-4">
        <button
          type="button"
          class="p-2 rounded-full text-slate-300 hover:text-white hover:bg-navy-800 transition-colors"
          (click)="theme.toggleTheme()"
          aria-label="Basculer le thème clair ou sombre"
        >
          @if (theme.theme() === 'light') {
            <app-lucide-icon [icon]="icons.moon" className="w-4 h-4" />
          } @else {
            <app-lucide-icon [icon]="icons.sun" className="w-4 h-4" />
          }
        </button>

        <div class="relative">
          <button
            type="button"
            class="relative p-2 text-slate-300 hover:text-white transition-colors"
            (click)="notifications.toggleDropdown()"
          >
            <app-lucide-icon [icon]="icons.bell" className="w-5 h-5" />
            <app-notification-badge [count]="notifications.unreadCount()" />
          </button>
          <app-notification-dropdown />
        </div>

        <button
          type="button"
          class="p-2 rounded-full text-slate-300 hover:text-white hover:bg-navy-800 transition-colors"
          (click)="notifications.openSettings()"
          aria-label="Ouvrir les paramètres"
        >
          <app-lucide-icon [icon]="icons.settings" className="w-4 h-4" />
        </button>
        <div class="h-6 w-px bg-navy-800 mx-1"></div>
        <div class="flex items-center gap-2 bg-navy-900/70 border border-navy-800 rounded-lg px-3 py-1.5">
          <app-lucide-icon [icon]="icons.shield" className="w-4 h-4 text-blue-400" />
          <span class="text-sm font-medium text-slate-300 hidden sm:inline">
            {{ i18n.t('topbar.role.label') }}:
          </span>
          <select
            class="bg-transparent text-sm font-semibold text-blue-400 focus:outline-none cursor-pointer"
            [value]="role.currentRole()"
            (change)="onRoleChange($event)"
          >
            @for (r of roles; track r) {
              <option [value]="r">{{ roleLabel[r] }}</option>
            }
          </select>
          @if (role.employeesForCurrentRole().length > 0) {
            <select
              class="bg-transparent text-xs font-medium text-slate-300 focus:outline-none cursor-pointer max-w-[11rem] truncate"
              [value]="role.currentUser().id"
              (change)="onUserChange($event)"
              title="Utilisateur démo (mode développeur)"
            >
              @for (u of role.employeesForCurrentRole(); track u.id) {
                <option [value]="u.id">{{ u.firstName }} {{ u.lastName }}</option>
              }
            </select>
          } @else {
            <span class="text-xs text-slate-500 hidden lg:inline truncate max-w-[10rem]" [title]="role.currentUser().email">
              {{ role.currentUser().firstName }} {{ role.currentUser().lastName }}
            </span>
          }
        </div>
      </div>
    </header>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TopbarComponent {
  readonly role = inject(RoleService);
  readonly theme = inject(ThemeService);
  readonly i18n = inject(I18nService);
  readonly notifications = inject(NotificationUiService);

  readonly roles = PRIME_AUTHORIZED_ROLES;
  readonly roleLabel: Record<Role, string> = {
    Admin: 'Administrateur',
    RH: 'RH',
    Manager: 'Manager',
    Comptabilité: 'Comptabilité',
    'Chef de projet': 'Chef de projet',
    Superviseur: 'Superviseur',
    'Référent technique': 'Référent technique',
    Pilote: 'Pilote',
    Audit: 'Audit',
    // ---- LEGACY COMPAT (Phase 0 — à supprimer en Phase 1.6) ----
    Coach: 'Coach (legacy)',
    RP: 'RP (legacy)',
    Comptable: 'Comptable (legacy)',
  };

  readonly icons = { search: Search, moon: Moon, sun: Sun, bell: Bell, settings: Settings, shield: Shield };

  onRoleChange(ev: Event): void {
    const v = (ev.target as HTMLSelectElement).value as Role;
    this.role.setRole(v);
  }

  onUserChange(ev: Event): void {
    const id = (ev.target as HTMLSelectElement).value;
    this.role.setUserId(id);
  }
}
