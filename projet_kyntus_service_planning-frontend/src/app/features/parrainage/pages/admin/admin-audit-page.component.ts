import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { AlertTriangle, CheckCircle2, Clock3, Database } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { AccessDeniedComponent } from '../../components/access-denied.component';
import { AuditTableComponent } from '../../components/audit/audit-table.component';
import { AccessHistoryTableComponent } from '../../components/audit/access-history-table.component';
import { AnomaliesPanelComponent } from '../../components/audit/anomalies-panel.component';
import { ReportingDashboardComponent } from '../../components/audit/reporting-dashboard.component';
import { AuditDetailsDrawerComponent } from '../../components/audit/audit-details-drawer.component';
import { UserTimelineModalComponent } from '../../components/audit/user-timeline-modal.component';
import { ParrainageStoreService } from '../../services/parrainage-store.service';
import { ParrainageRoleService } from '../../state/parrainage-role.service';
import { AuditSectionService } from '../../state/audit-section.service';
import { enrichAuditRowFromId, getAuditOrgTree } from '../../lib/audit-org-ui';
import type { JournalRow, SortKey, SeverityLevel } from '../../audit/audit-types';
import type { AnomalyRow } from '../../audit/audit-demo-data';

const ORG = getAuditOrgTree();
const ROLE_FILTER_OPTIONS = ['Tous', 'RP', 'Manager', 'Coach', 'Pilote'] as const;
const SEVERITY_OPTIONS = ['Tous', 'INFO', 'WARNING', 'CRITICAL'] as const;
const ACTION_CHIPS = ['CREATE', 'UPDATE', 'DELETE', 'APPROVE', 'CONFIG'] as const;

const SECTION_INTRO: Record<string, { title: string; desc: string }> = {
  dashboard: { title: 'Audit Parrainage', desc: 'Vue synthétique — volumes et alertes.' },
  journal: { title: "Journal d'audit", desc: 'Log technique complet : investigation, conformité, export. IP, device, gravité, actions.' },
  'access-history': { title: "Historique d'accès", desc: 'Sécurité : connexions, déconnexions, échecs — sans actions métier.' },
  anomalies: { title: 'Anomalies', desc: 'Comportements suspects : volumes, horaires, accès inhabituels.' },
  reporting: { title: 'Reporting', desc: 'Indicateurs et tendances — pas un journal brut.' },
};

const toCsv = (headers: string[], data: Array<Array<string | number>>) =>
  [headers.join(';'), ...data.map((d) => d.join(';'))].join('\n');

function download(name: string, content: string, mime: string): void {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}

function deriveSeverity(action: string, status: string): SeverityLevel {
  if (action.includes('Suppression') || status === 'Rejeté') return 'CRITICAL';
  if (action.includes('Modification') || action === 'Validation') return 'WARNING';
  return 'INFO';
}

function deriveActionCode(action: string, rawAudit: boolean): string {
  if (rawAudit) return 'CONFIG';
  if (action === 'Création') return 'CREATE';
  if (action === 'Suppression') return 'DELETE';
  if (action === 'Validation') return 'APPROVE';
  return 'UPDATE';
}

type JournalBase = Pick<JournalRow, 'id' | 'datetime' | 'employee' | 'action' | 'item' | 'status' | 'departement' | 'pole' | 'cellule' | 'roleMetier'>;

function enrichJournal(base: JournalBase, id: string, rawAudit: boolean): JournalRow {
  const hash = id.split('').reduce((a, c) => a + c.charCodeAt(0), 0);
  const ips = [`105.66.${hash % 200}.${10 + (hash % 40)}`, `10.0.${hash % 5}.${hash % 200}`];
  const devices = ['Chrome 124 / Win 11', 'Safari 17 / macOS', 'Edge / Win 10', 'Firefox / Linux'];
  const beforeState = rawAudit
    ? { configVersion: 'précédente', scope: 'referralProgramRules' }
    : { dossier: base.item, statut: 'avant_action' };
  const afterState = { statut: base.status, actionLibelle: base.action, reference: id };
  return {
    ...base,
    ip: ips[hash % 2],
    device: devices[hash % devices.length],
    severity: deriveSeverity(base.action, base.status),
    actionCode: deriveActionCode(base.action, rawAudit),
    beforeState,
    afterState,
    metadata: { id, source: 'parrainage-microservice', channel: 'web', recordedAt: base.datetime, rawAudit },
  };
}

const PAGE_SIZE = 8;

@Component({
  selector: 'app-admin-audit-page',
  standalone: true,
  imports: [
    LucideIconComponent,
    AccessDeniedComponent,
    AuditTableComponent,
    AccessHistoryTableComponent,
    AnomaliesPanelComponent,
    ReportingDashboardComponent,
    AuditDetailsDrawerComponent,
    UserTimelineModalComponent,
  ],
  template: `
    @if (role !== 'ADMIN' && role !== 'AUDIT') {
      <app-access-denied message="Cette section est réservée aux administrateurs globaux et au rôle Audit." backLabel="Retour" />
    } @else {
      <section class="flex-1 space-y-6">
        <div>
          <h1 class="prime-page-title">{{ intro.title }}</h1>
          <p class="text-sm text-muted mt-1">{{ intro.desc }}</p>
        </div>

        @switch (auditSection.section()) {
          @case ('dashboard') {
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              <div class="card-navy p-4 bg-gradient-to-br from-blue-950/40 to-navy-900 border border-blue-900/40 shadow-sm hover:shadow-[0_10px_25px_rgba(37,99,235,0.18)] hover:scale-[1.02] transition-all duration-300">
                <div class="flex items-center gap-2 text-primary"><app-lucide-icon [icon]="dbIcon" className="w-4 h-4 text-blue-300" /><p class="text-xs">Nombre total</p></div>
                <p class="text-2xl text-white font-bold mt-2">{{ rows().length }}</p>
                <p class="text-xs text-muted">événements journal</p>
              </div>
              <div class="card-navy p-4 bg-gradient-to-br from-emerald-950/35 to-navy-900 border border-emerald-900/40 shadow-sm hover:shadow-[0_10px_25px_rgba(16,185,129,0.16)] hover:scale-[1.02] transition-all duration-300">
                <div class="flex items-center gap-2 text-primary"><app-lucide-icon [icon]="checkIcon" className="w-4 h-4 text-emerald-300" /><p class="text-xs">Infos (gravité)</p></div>
                <p class="text-2xl text-white font-bold mt-2">{{ countSeverity('INFO') }}</p>
                <p class="text-xs text-muted">niveau INFO</p>
              </div>
              <div class="card-navy p-4 bg-gradient-to-br from-amber-950/35 to-navy-900 border border-amber-900/40 shadow-sm hover:shadow-[0_10px_25px_rgba(245,158,11,0.16)] hover:scale-[1.02] transition-all duration-300">
                <div class="flex items-center gap-2 text-primary"><app-lucide-icon [icon]="clockIcon" className="w-4 h-4 text-amber-300" /><p class="text-xs">Avertissements</p></div>
                <p class="text-2xl text-white font-bold mt-2">{{ countSeverity('WARNING') }}</p>
                <p class="text-xs text-muted">WARNING</p>
              </div>
              <div class="card-navy p-4 bg-gradient-to-br from-rose-950/35 to-navy-900 border border-rose-900/40 shadow-sm hover:shadow-[0_10px_25px_rgba(244,63,94,0.16)] hover:scale-[1.02] transition-all duration-300">
                <div class="flex items-center gap-2 text-primary"><app-lucide-icon [icon]="alertIcon" className="w-4 h-4 text-rose-300" /><p class="text-xs">Critiques</p></div>
                <p class="text-2xl text-white font-bold mt-2">{{ countSeverity('CRITICAL') }}</p>
                <p class="text-xs text-muted">CRITICAL</p>
              </div>
            </div>
          }
          @case ('access-history') {
            <app-access-history-table />
          }
          @case ('reporting') {
            <app-reporting-dashboard />
          }
          @case ('anomalies') {
            <app-anomalies-panel (investigate)="investigateAnomaly($event)" (openTimeline)="openAnomalyTimeline($event)" />
          }
          @case ('journal') {
            <div class="card-navy p-4 space-y-3 border border-default/80">
              <div class="flex flex-wrap items-end gap-3">
                <select [value]="deptFilter()" (change)="onDeptChange($any($event.target).value)" [class]="selClass" aria-label="Département">
                  @for (d of deptOptions; track d) { <option [value]="d">{{ d === 'Tous' ? 'Département' : d }}</option> }
                </select>
                <select [value]="poleFilter()" (change)="onPoleChange($any($event.target).value)" [class]="selClass" [disabled]="deptFilter() === 'Tous'" aria-label="Pôle">
                  @for (p of poleOptions(); track p) { <option [value]="p">{{ p === 'Tous' ? 'Pôle' : p }}</option> }
                </select>
                <select [value]="celluleFilter()" (change)="setFilter(celluleFilter, $any($event.target).value)" [class]="selClass" [disabled]="deptFilter() === 'Tous' || poleFilter() === 'Tous'" aria-label="Cellule">
                  @for (c of celluleOptions(); track c) { <option [value]="c">{{ c === 'Tous' ? 'Cellule' : c }}</option> }
                </select>
                <select [value]="roleMetierFilter()" (change)="setFilter(roleMetierFilter, $any($event.target).value)" [class]="selClass" aria-label="Rôle métier">
                  @for (r of roleOptions; track r) { <option [value]="r">{{ r === 'Tous' ? 'Rôle' : r }}</option> }
                </select>
                <select [value]="severityFilter()" (change)="setFilter(severityFilter, $any($event.target).value)" [class]="selClass" aria-label="Gravité">
                  @for (s of severityOptions; track s) { <option [value]="s">{{ s === 'Tous' ? 'Gravité' : s }}</option> }
                </select>
                <button type="button" (click)="resetHierarchyFilters()" class="px-3 py-2 rounded-lg border border-default text-sm text-muted hover:bg-input hover:text-primary whitespace-nowrap transition-colors">
                  Réinitialiser filtres
                </button>
              </div>
              <div class="flex flex-wrap gap-2 items-center">
                <span class="text-[11px] text-muted uppercase">Action rapide :</span>
                @for (c of actionChips; track c) {
                  <button type="button" (click)="setFilter(actionChip, c)" [class]="'px-2.5 py-1 rounded-full text-xs border transition-colors duration-150 ' + (actionChip() === c ? 'bg-blue-600/25 border-blue-500/50 text-blue-200' : 'border-default text-muted hover:border-default')">
                    {{ c }}
                  </button>
                }
              </div>
              <div class="flex flex-wrap items-end gap-3">
                <input [value]="search()" (input)="setFilter(search, $any($event.target).value)" placeholder="Recherche globale (utilisateur, IP, action, org…)"
                  class="bg-input border border-default rounded-lg px-3 py-2 text-sm text-primary min-w-[200px] focus:border-blue-500/40 focus:ring-1 focus:ring-blue-500/20" />
                <input type="date" [value]="dateFilter()" (change)="setFilter(dateFilter, $any($event.target).value)" class="bg-input border border-default rounded-lg px-3 py-2 text-sm text-primary" />
                <select [value]="userFilter()" (change)="setFilter(userFilter, $any($event.target).value)" [class]="selClass">
                  @for (u of users(); track u) { <option [value]="u">{{ u }}</option> }
                </select>
                <select [value]="actionFilter()" (change)="setFilter(actionFilter, $any($event.target).value)" [class]="selClass">
                  @for (a of actionsList(); track a) { <option [value]="a">{{ a }}</option> }
                </select>
                <div class="flex gap-2 ml-auto">
                  <button type="button" (click)="exportAuditExcel()" class="px-3 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-sm whitespace-nowrap transition-colors">Excel</button>
                  <button type="button" (click)="exportAuditCsv()" class="px-3 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm whitespace-nowrap transition-colors">CSV</button>
                </div>
              </div>
            </div>

            <app-audit-table
              [visibleRows]="visibleRows()"
              [hasNoData]="hasNoData()"
              [isMockDisplay]="isMockDisplay()"
              [sortKey]="sortKey()"
              [sortDir]="sortDir()"
              (toggleSort)="toggleSort($event)"
              (view)="selected.set($event)"
            />

            <div class="flex items-center justify-between text-sm text-muted">
              <span>Page {{ safePage() }} / {{ totalPages() }}</span>
              <div class="flex gap-2">
                <button type="button" [disabled]="safePage() <= 1" (click)="page.set(safePage() - 1)" class="px-3 py-1.5 rounded-md border border-default disabled:opacity-40 hover:bg-input transition-colors">Précédent</button>
                <button type="button" [disabled]="safePage() >= totalPages()" (click)="page.set(safePage() + 1)" class="px-3 py-1.5 rounded-md border border-default disabled:opacity-40 hover:bg-input transition-colors">Suivant</button>
              </div>
            </div>
          }
        }

        <app-audit-details-drawer
          [selected]="selected()"
          (close)="selected.set(null)"
          (investigateUser)="investigateUser()"
          (openUserTimeline)="openTimeline()"
        />

        <app-user-timeline-modal
          [userLabel]="timelineUser() ?? ''"
          [rows]="rows()"
          [open]="!!timelineUser()"
          (close)="timelineUser.set(null)"
        />
      </section>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminAuditPageComponent {
  private readonly store = inject(ParrainageStoreService);
  private readonly roleSvc = inject(ParrainageRoleService);
  readonly auditSection = inject(AuditSectionService);

  readonly dbIcon = Database;
  readonly checkIcon = CheckCircle2;
  readonly clockIcon = Clock3;
  readonly alertIcon = AlertTriangle;

  readonly roleOptions = ROLE_FILTER_OPTIONS;
  readonly severityOptions = SEVERITY_OPTIONS;
  readonly actionChips = ['Tous', ...ACTION_CHIPS];
  readonly selClass = 'bg-input border border-default rounded-lg px-3 py-2 text-sm text-primary min-w-[140px]';
  readonly deptOptions = ['Tous', ...ORG.map((d) => d.dept)];

  readonly rows = computed(() => this.buildRows());

  readonly search = signal('');
  readonly dateFilter = signal('');
  readonly userFilter = signal('Tous');
  readonly actionFilter = signal('Tous');
  readonly severityFilter = signal<string>('Tous');
  readonly actionChip = signal<string>('Tous');
  readonly deptFilter = signal('Tous');
  readonly poleFilter = signal('Tous');
  readonly celluleFilter = signal('Tous');
  readonly roleMetierFilter = signal('Tous');
  readonly page = signal(1);
  readonly sortKey = signal<SortKey>('datetime');
  readonly sortDir = signal<'asc' | 'desc'>('desc');
  readonly selected = signal<JournalRow | null>(null);
  readonly timelineUser = signal<string | null>(null);

  get role() {
    return this.roleSvc.user().role;
  }

  get intro() {
    return SECTION_INTRO[this.auditSection.section()] ?? SECTION_INTRO['journal'];
  }

  private buildRows(): JournalRow[] {
    const history = this.store.history();
    const audit = this.store.auditLog();

    const fromHistory: JournalRow[] = history.map((h) => {
      const org = enrichAuditRowFromId(h.id);
      const base: JournalBase = {
        id: h.id,
        datetime: h.createdAt.toLocaleString('fr-FR'),
        employee: h.performedByLabel,
        action:
          h.action === 'SUBMITTED' ? 'Création' : h.action === 'APPROVED' ? 'Validation' : h.action === 'REJECTED' ? 'Suppression' : 'Modification',
        item: h.candidateName,
        status: h.action === 'APPROVED' || h.action === 'REWARDED' ? 'Validé' : 'En attente',
        ...org,
      };
      return enrichJournal(base, h.id, false);
    });

    const fromAudit: JournalRow[] = audit.map((a) => {
      const org = enrichAuditRowFromId(a.id);
      const base: JournalBase = {
        id: a.id,
        datetime: a.timestamp.toLocaleString('fr-FR'),
        employee: a.userLabel,
        action: a.action === 'CONFIG_UPDATE' ? 'Modification' : 'Validation',
        item: a.details ?? 'Configuration système',
        status: 'Validé',
        ...org,
      };
      return enrichJournal(base, a.id, true);
    });

    return [...fromHistory, ...fromAudit];
  }

  countSeverity(level: SeverityLevel): number {
    return this.rows().filter((r) => r.severity === level).length;
  }

  readonly poleOptions = computed(() => {
    if (this.deptFilter() === 'Tous') return ['Tous'];
    const d = ORG.find((x) => x.dept === this.deptFilter());
    return ['Tous', ...(d?.poles.map((p) => p.name) ?? [])];
  });

  readonly celluleOptions = computed(() => {
    if (this.deptFilter() === 'Tous' || this.poleFilter() === 'Tous') return ['Tous'];
    const d = ORG.find((x) => x.dept === this.deptFilter());
    const p = d?.poles.find((x) => x.name === this.poleFilter());
    return ['Tous', ...(p?.cellules ?? [])];
  });

  readonly users = computed(() => ['Tous', ...Array.from(new Set(this.rows().map((r) => r.employee)))]);
  readonly actionsList = computed(() => ['Tous', ...Array.from(new Set(this.rows().map((r) => r.action)))]);

  readonly filteredRows = computed(() => {
    const q = this.search().trim().toLowerCase();
    const data = this.rows().filter((r) => {
      const qOk =
        !q ||
        `${r.datetime} ${r.employee} ${r.action} ${r.item} ${r.departement} ${r.pole} ${r.cellule} ${r.roleMetier} ${r.ip} ${r.device}`
          .toLowerCase()
          .includes(q);
      const dOk = !this.dateFilter() || r.datetime.startsWith(this.dateFilter());
      const uOk = this.userFilter() === 'Tous' || r.employee === this.userFilter();
      const aOk = this.actionFilter() === 'Tous' || r.action === this.actionFilter();
      const sevOk = this.severityFilter() === 'Tous' || r.severity === this.severityFilter();
      const chipOk = this.actionChip() === 'Tous' || r.actionCode === this.actionChip();
      const deptOk = this.deptFilter() === 'Tous' || r.departement === this.deptFilter();
      const poleOk = this.poleFilter() === 'Tous' || r.pole === this.poleFilter();
      const cellOk = this.celluleFilter() === 'Tous' || r.cellule === this.celluleFilter();
      const roleOk = this.roleMetierFilter() === 'Tous' || r.roleMetier === this.roleMetierFilter();
      return qOk && dOk && uOk && aOk && sevOk && chipOk && deptOk && poleOk && cellOk && roleOk;
    });
    const key = this.sortKey();
    const dir = this.sortDir();
    return [...data].sort((a, b) => {
      const va = String(a[key as keyof JournalRow]).toLowerCase();
      const vb = String(b[key as keyof JournalRow]).toLowerCase();
      if (va === vb) return 0;
      return dir === 'asc' ? (va > vb ? 1 : -1) : va < vb ? 1 : -1;
    });
  });

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filteredRows().length / PAGE_SIZE)));
  readonly safePage = computed(() => Math.min(this.page(), this.totalPages()));
  readonly pagedRows = computed(() => this.filteredRows().slice((this.safePage() - 1) * PAGE_SIZE, this.safePage() * PAGE_SIZE));

  private readonly fallbackRows: JournalRow[] = [
    enrichJournal({ id: 'mock-1', datetime: '2026-03-27 08:10', employee: 'Audit Bot', action: 'Création', item: 'Parrainage Martin / Leila', status: 'En attente', ...enrichAuditRowFromId('mock-1') }, 'mock-1', false),
    enrichJournal({ id: 'mock-2', datetime: '2026-03-27 09:55', employee: 'RH Parrainage', action: 'Validation', item: 'Prime parrainage T2', status: 'Validé', ...enrichAuditRowFromId('mock-2') }, 'mock-2', false),
    enrichJournal({ id: 'mock-3', datetime: '2026-03-27 10:33', employee: 'Comptable', action: 'Suppression', item: 'Dossier doublon #P-44', status: 'Rejeté', ...enrichAuditRowFromId('mock-3') }, 'mock-3', false),
  ];

  readonly visibleRows = computed(() => (this.pagedRows().length > 0 ? this.pagedRows() : this.fallbackRows));
  readonly hasNoData = computed(() => this.rows().length === 0);
  readonly isMockDisplay = computed(() => this.pagedRows().length === 0);

  setFilter<T>(sig: { set: (v: T) => void }, value: T): void {
    sig.set(value);
    this.page.set(1);
  }

  onDeptChange(value: string): void {
    this.deptFilter.set(value);
    this.poleFilter.set('Tous');
    this.celluleFilter.set('Tous');
    this.page.set(1);
  }

  onPoleChange(value: string): void {
    this.poleFilter.set(value);
    this.celluleFilter.set('Tous');
    this.page.set(1);
  }

  resetHierarchyFilters(): void {
    this.deptFilter.set('Tous');
    this.poleFilter.set('Tous');
    this.celluleFilter.set('Tous');
    this.roleMetierFilter.set('Tous');
    this.page.set(1);
  }

  toggleSort(k: SortKey): void {
    if (this.sortKey() === k) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortKey.set(k);
      this.sortDir.set('asc');
    }
  }

  private exportRows(name: string, mime: string): void {
    const headers = ['Date/heure', 'Utilisateur', 'IP', 'Device', 'Gravité', 'Code', 'Département', 'Pôle', 'Cellule', 'Rôle', 'Action', 'Élément', 'Statut'];
    const content = toCsv(
      headers,
      this.filteredRows().map((r) => [r.datetime, r.employee, r.ip, r.device, r.severity, r.actionCode, r.departement, r.pole, r.cellule, r.roleMetier, r.action, r.item, r.status]),
    );
    download(name, content, mime);
  }

  exportAuditCsv(): void {
    this.exportRows('audit_parrainage.csv', 'text/csv;charset=utf-8');
  }

  exportAuditExcel(): void {
    this.exportRows('audit_parrainage.xls', 'application/vnd.ms-excel');
  }

  investigateUser(): void {
    const sel = this.selected();
    if (!sel) return;
    this.auditSection.setSection('journal');
    this.search.set(sel.employee);
    this.page.set(1);
    this.selected.set(null);
  }

  openTimeline(): void {
    const sel = this.selected();
    if (!sel) return;
    this.timelineUser.set(sel.employee);
  }

  investigateAnomaly(a: AnomalyRow): void {
    const q = a.relatedUserLabel ?? a.searchHints?.[0] ?? '';
    this.auditSection.setSection('journal');
    this.search.set(q);
    this.page.set(1);
  }

  openAnomalyTimeline(a: AnomalyRow): void {
    if (a.relatedUserLabel) this.timelineUser.set(a.relatedUserLabel);
  }
}
