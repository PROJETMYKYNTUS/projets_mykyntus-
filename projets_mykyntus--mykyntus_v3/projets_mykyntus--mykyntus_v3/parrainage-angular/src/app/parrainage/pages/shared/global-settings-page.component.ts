import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { User, Bell, Layers, Sun, Moon, CheckCircle2, Cpu } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { ThemeService } from '../../state/theme.service';
import { UiPreferencesService } from '../../services/ui-preferences.service';
import { ReferralService } from '../../services/referral.service';
import { AdminService } from '../../services/admin.service';
import { ParrainageRoleService } from '../../state/parrainage-role.service';
import { MOCK_DEPARTMENTS, MOCK_PROJECTS, MOCK_USERS_BY_ROLE } from '../../lib/parrainage-directory';
import { formatOrgCompactLine, getParrainagePersonalOrgLabels } from '../../lib/personal-org-labels';
import type { NotificationPreferences, ParrainageRole } from '../../models/referral.model';
import type { SystemConfig } from '../../models/system-config.model';

type PrefKey = keyof NotificationPreferences;

const NOTIF_ROWS: ReadonlyArray<{ key: PrefKey; label: string }> = [
  { key: 'inApp', label: "Notifications dans l'application" },
  { key: 'email', label: 'E-mails' },
  { key: 'referrals', label: 'Nouveaux parrainages' },
  { key: 'approvals', label: 'Approbations / refus' },
  { key: 'payments', label: 'Récompenses & versements' },
  { key: 'systemAlerts', label: 'Alertes système' },
];

const ROLE_LABELS: Record<ParrainageRole, string> = {
  PILOTE: 'Pilote',
  COACH: 'Coach',
  MANAGER: 'Manager',
  RP: 'Responsable projet',
  RH: 'RH',
  COMPTA: 'Comptabilité',
  ADMIN: 'Administrateur',
  AUDIT: 'Audit',
};

@Component({
  selector: 'app-global-settings-page',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="max-w-4xl mx-auto space-y-8">
      <div>
        <h1 class="text-2xl font-semibold text-slate-50">Paramètres</h1>
        <p class="text-sm text-slate-500 mt-1">Profil, notifications et préférences — selon votre rôle.</p>
      </div>

      @if (saved()) {
        <div class="rounded-lg border border-emerald-500/40 bg-emerald-500/10 px-4 py-2 text-sm text-emerald-200">Enregistré.</div>
      }

      <section class="card-navy p-4 space-y-4">
        <div class="flex items-center gap-3 pb-3 border-b border-navy-800">
          <app-lucide-icon [icon]="userIcon" className="w-5 h-5 text-blue-500" />
          <h2 class="text-lg font-bold text-white">Profil</h2>
        </div>

        <div class="space-y-4">
          <div>
            <h3 class="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-2">Informations personnelles</h3>
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-x-4 gap-y-2 sm:gap-y-0 text-sm min-w-0">
              <div class="min-w-0">
                <span class="text-slate-500 block mb-0.5">Nom</span>
                <p class="text-slate-100 font-medium mb-2 sm:mb-0 break-words">{{ splitName().nom }}</p>
              </div>
              <div class="min-w-0">
                <span class="text-slate-500 block mb-0.5">Prénom</span>
                <p class="text-slate-100 font-medium mb-2 sm:mb-0 break-words">{{ splitName().prenom }}</p>
              </div>
              <div class="min-w-0">
                <span class="text-slate-500 block mb-0.5">Rôle</span>
                <p class="text-slate-100 font-medium mb-0 break-words">{{ roleLabel }}</p>
              </div>
            </div>
          </div>

          <div class="border-t border-navy-800 pt-2">
            <h3 class="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-1">Organisation</h3>
            <p class="text-sm text-slate-400 leading-snug">{{ orgCompact() || '—' }}</p>
          </div>

          <div class="border-t border-navy-800 pt-2">
            <h3 class="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-2">Contact</h3>
            <div class="space-y-2 text-sm">
              <div>
                <span class="text-slate-500 block mb-0.5">E-mail</span>
                <p class="text-slate-100 font-medium mb-0">{{ email }}</p>
              </div>
              <div class="rounded-lg border border-navy-800 bg-navy-900/40 p-2">
                <span class="text-slate-500 block mb-0.5">Annuaire</span>
                <p class="text-slate-400 text-xs mt-1 leading-relaxed mb-0">{{ rosterPreview }}…</p>
              </div>
            </div>
          </div>
        </div>

        <p class="text-xs text-slate-600 pt-3 border-t border-navy-800">
          Départements : {{ departmentsLine }} — Projets : {{ projectsLine }}
        </p>
      </section>

      <section class="card-navy p-6 space-y-6">
        <div class="flex items-center gap-3 pb-4 border-b border-navy-800">
          <app-lucide-icon [icon]="bellIcon" className="w-5 h-5 text-blue-500" />
          <h2 class="text-lg font-bold text-white">Notifications</h2>
        </div>
        <div class="space-y-4">
          @for (row of notifRows; track row.key) {
            <label class="flex items-center justify-between p-3 rounded-lg bg-navy-900/50 border border-navy-800">
              <span class="text-sm text-slate-300">{{ row.label }}</span>
              <input type="checkbox" class="rounded border-navy-700" [checked]="isChecked(row.key)" (change)="togglePref(row.key)" />
            </label>
          }
        </div>
      </section>

      <section class="card-navy p-6 space-y-6">
        <div class="flex items-center gap-3 pb-4 border-b border-navy-800">
          <app-lucide-icon [icon]="layersIcon" className="w-5 h-5 text-blue-500" />
          <h2 class="text-lg font-bold text-white">Interface</h2>
        </div>
        <div class="flex items-center justify-between p-3 rounded-lg bg-navy-900/50 border border-navy-800">
          <span class="text-sm text-slate-300">Mode compact (espacements réduits)</span>
          <input type="checkbox" class="rounded border-navy-700" [checked]="compact()" (change)="toggleCompact()" />
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <button type="button" (click)="setTheme('dark')"
            [class]="'flex items-center justify-between p-4 rounded-lg border transition-all ' + (theme.theme() === 'dark' ? 'bg-blue-600/10 border-blue-500/50 text-white' : 'bg-navy-900 border-navy-800 text-slate-400 hover:bg-navy-800')">
            <span class="flex items-center gap-2"><app-lucide-icon [icon]="moonIcon" className="w-5 h-5" /> Sombre</span>
            @if (theme.theme() === 'dark') { <app-lucide-icon [icon]="checkIcon" className="w-5 h-5 text-blue-500" /> }
          </button>
          <button type="button" (click)="setTheme('light')"
            [class]="'flex items-center justify-between p-4 rounded-lg border transition-all ' + (theme.theme() === 'light' ? 'bg-blue-600/10 border-blue-500/50 text-white' : 'bg-navy-900 border-navy-800 text-slate-400 hover:bg-navy-800')">
            <span class="flex items-center gap-2"><app-lucide-icon [icon]="sunIcon" className="w-5 h-5" /> Clair</span>
            @if (theme.theme() === 'light') { <app-lucide-icon [icon]="checkIcon" className="w-5 h-5 text-blue-500" /> }
          </button>
        </div>
      </section>

      @if (showSystem) {
        <section class="card-navy p-6 space-y-6 border-amber-500/20">
          <div class="flex items-center gap-3 pb-4 border-b border-navy-800">
            <app-lucide-icon [icon]="cpuIcon" className="w-5 h-5 text-amber-400" />
            <h2 class="text-lg font-bold text-white">Paramètres système</h2>
          </div>
          <div class="rounded-lg border border-navy-800 bg-navy-900/40 p-4 space-y-2 mb-4">
            <p class="text-xs font-bold text-slate-400 uppercase tracking-wider">Règles de primes (DH)</p>
            <p class="text-sm text-slate-300">
              Mode actif :
              <span class="font-semibold text-white">{{ config().referralProgramRules?.activeMode === 'CRITICAL_PERIOD' ? 'Période critique' : 'Standard' }}</span>
              —
              enveloppe totale :
              <span class="text-emerald-400/90">{{ config().defaultBonusAmount }} DH</span>
              (dérivé des tranches configurées).
            </p>
            <p class="text-xs text-slate-500">Modifiez les modes, montants et délais dans la page « Configuration système » (menu RH).</p>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="text-xs font-bold text-slate-400 uppercase tracking-wider">Limite parrainages / employé</label>
              <input type="number" class="w-full mt-1 bg-navy-900 border border-navy-800 rounded-lg px-4 py-2.5 text-white focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
                [value]="config().referralLimitPerEmployee" (input)="setLimit($any($event.target).value)" />
            </div>
            <div>
              <label class="text-xs font-bold text-slate-400 uppercase tracking-wider">Seuil alerte « en attente »</label>
              <input type="number" class="w-full mt-1 bg-navy-900 border border-navy-800 rounded-lg px-4 py-2.5 text-white focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50"
                [value]="config().pendingReferralAlertThreshold ?? 5" (input)="setThreshold($any($event.target).value)" />
            </div>
          </div>
          <p class="text-xs text-slate-500">
            Règles détaillées : menu RH « Règles de parrainage ». Configuration avancée :
            <span class="font-mono text-slate-400">/parrainage/admin/config</span>.
          </p>
          <button type="button" (click)="saveSystem()" class="bg-blue-600 hover:bg-blue-500 text-white px-6 py-2.5 rounded-lg font-medium transition-colors">
            Enregistrer la configuration
          </button>
        </section>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GlobalSettingsPageComponent {
  readonly theme = inject(ThemeService);
  private readonly ui = inject(UiPreferencesService);
  private readonly referrals = inject(ReferralService);
  private readonly admin = inject(AdminService);
  private readonly roleSvc = inject(ParrainageRoleService);

  readonly userIcon = User;
  readonly bellIcon = Bell;
  readonly layersIcon = Layers;
  readonly sunIcon = Sun;
  readonly moonIcon = Moon;
  readonly checkIcon = CheckCircle2;
  readonly cpuIcon = Cpu;

  readonly notifRows = NOTIF_ROWS;

  readonly saved = signal(false);
  readonly compact = signal(this.ui.get().compactMode);
  readonly prefs = computed(() => this.referrals.getNotificationPreferences());
  readonly config = signal<SystemConfig>(this.admin.getSystemConfig());

  readonly departmentsLine = MOCK_DEPARTMENTS.map((d) => d.name).join(' · ');
  readonly projectsLine = MOCK_PROJECTS.map((p) => p.name).join(' · ');

  get role(): ParrainageRole {
    return this.roleSvc.user().role;
  }

  get roleLabel(): string {
    return ROLE_LABELS[this.role] ?? 'Audit';
  }

  get showSystem(): boolean {
    return this.role === 'RH';
  }

  get email(): string {
    const u = this.roleSvc.user();
    return u.email ?? (u.id ? `${u.id}@mykyntus.com` : '—');
  }

  get rosterPreview(): string {
    const roster = MOCK_USERS_BY_ROLE[this.role === 'AUDIT' ? 'ADMIN' : this.role] ?? MOCK_USERS_BY_ROLE['PILOTE'];
    return roster.slice(0, 3).map((u) => u.name).join(', ');
  }

  splitName(): { prenom: string; nom: string } {
    const t = this.roleSvc.user().name?.trim() ?? '';
    if (!t) return { prenom: '—', nom: '—' };
    const parts = t.split(/\s+/);
    if (parts.length === 1) return { prenom: parts[0], nom: '—' };
    return { prenom: parts[0], nom: parts.slice(1).join(' ') };
  }

  readonly orgCompact = computed(() => {
    const org = getParrainagePersonalOrgLabels(this.roleSvc.user());
    return formatOrgCompactLine({ departement: org.departement, pole: org.pole, cellule: org.cellule });
  });

  isChecked(key: PrefKey): boolean {
    return this.prefs()[key] !== false;
  }

  togglePref(key: PrefKey): void {
    const next: NotificationPreferences = { ...this.prefs(), [key]: !(this.prefs()[key] !== false) };
    void this.referrals.updateNotificationPreferences(next).then(() => this.flashSaved());
  }

  toggleCompact(): void {
    const v = !this.compact();
    this.compact.set(v);
    this.ui.set({ compactMode: v });
  }

  setTheme(target: 'dark' | 'light'): void {
    if (this.theme.theme() !== target) this.theme.toggleTheme();
  }

  setLimit(value: string): void {
    this.config.set({ ...this.config(), referralLimitPerEmployee: Number(value) || 0 });
  }

  setThreshold(value: string): void {
    this.config.set({ ...this.config(), pendingReferralAlertThreshold: Number(value) || 0 });
  }

  saveSystem(): void {
    if (this.role !== 'RH') return;
    const u = this.roleSvc.user();
    void this.admin
      .updateSystemConfig(this.config(), { id: u.id, label: u.name, role: u.role })
      .then((next) => {
        this.config.set(structuredClone(next));
        this.flashSaved();
      });
  }

  private flashSaved(): void {
    this.saved.set(true);
    setTimeout(() => this.saved.set(false), 2000);
  }
}
