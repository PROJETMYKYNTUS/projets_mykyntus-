import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Database,
  Eye,
  GitBranch,
  Inbox,
  Shield,
  X,
} from 'lucide';
import {
  PrimeSectionService,
  type AuditSection,
} from '../../state/prime-section.service';
import { PrimeAdminService, type AnomalyDto, type AuditLogDto } from '../../services/prime-admin.service';
import { AuditPrimeService } from '../../services/audit-prime.service';
import { getAuditOrgTree } from '../../lib/auditOrgUi';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

type SeverityLevel = 'INFO' | 'WARNING' | 'CRITICAL';
type SortKey =
  | 'date'
  | 'employee'
  | 'action'
  | 'item'
  | 'status'
  | 'departement'
  | 'pole'
  | 'cellule'
  | 'roleMetier'
  | 'severity';
type SortDir = 'asc' | 'desc';

interface JournalRow {
  id: string;
  date: string;
  employee: string;
  action: string;
  item: string;
  status: string;
  departement: string;
  pole: string;
  cellule: string;
  roleMetier: string;
  ip: string;
  device: string;
  severity: SeverityLevel;
  actionCode: 'CREATE' | 'UPDATE' | 'DELETE' | 'CONFIG';
  beforeState: Record<string, unknown>;
  afterState: Record<string, unknown>;
  metadata: Record<string, unknown>;
}

interface AccessRow {
  id: string;
  user: string;
  datetime: string;
  ip: string;
  location: string;
  success: boolean;
  type: string;
  role: string;
  departement: string;
}

interface AnomalyCard {
  id: string;
  text: string;
  user: string;
  severity: 'CRITICAL' | 'WARNING';
}

interface AuditDashboardState {
  totalPrimes: number;
  validations: number;
  anomalies: number;
  conformityRate: number;
}

const toCsv = (headers: string[], rows: Array<Array<string | number>>) =>
  [headers.join(';'), ...rows.map((r) => r.join(';'))].join('\n');

const downloadFile = (name: string, content: string, mime: string) => {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
};

const auditPageTitles: Partial<Record<AuditSection, string>> = {
  dashboard: 'Dashboard audit',
  journal: 'Journal d’audit',
  anomalies: 'Anomalies',
  reporting: 'Reporting',
  'access-history': 'Historique d’accès',
};

const ORG = getAuditOrgTree();
const ROLE_FILTER_OPTIONS = ['Tous', 'RP', 'Manager', 'Superviseur', 'Coach', 'Pilote'] as const;
const SEVERITY_OPTIONS = ['Tous', 'INFO', 'WARNING', 'CRITICAL'] as const;
const ACTION_CHIPS = ['Tous', 'CREATE', 'UPDATE', 'DELETE', 'CONFIG'] as const;

function mapAuditLogToJournalRow(d: AuditLogDto): JournalRow {
  const action = d.action ?? '';
  const al = action.toLowerCase();
  let actionCode: JournalRow['actionCode'] = 'UPDATE';
  if (al.includes('delete') || al.includes('reject') || al.includes('forbidden')) actionCode = 'DELETE';
  else if (al.includes('create') || al.includes('insert') || al.includes('nav')) actionCode = 'CREATE';
  else if (al.includes('config')) actionCode = 'CONFIG';

  let severity: SeverityLevel = 'INFO';
  if (actionCode === 'DELETE') severity = 'CRITICAL';
  else if (actionCode === 'CONFIG' || actionCode === 'UPDATE') severity = 'WARNING';

  let parsedDetail: Record<string, unknown> = {};
  if (d.detailJson) {
    try {
      parsedDetail = JSON.parse(d.detailJson) as Record<string, unknown>;
    } catch {
      parsedDetail = {};
    }
  }

  const status =
    typeof parsedDetail['status'] === 'string'
      ? (parsedDetail['status'] as string)
      : typeof parsedDetail['previousStatus'] === 'string'
        ? (parsedDetail['previousStatus'] as string)
        : '—';

  return {
    id: d.id,
    date: new Date(d.at).toLocaleString('fr-FR'),
    employee: (d.userDisplayName?.trim() || d.userId || '—').trim(),
    action,
    item: [d.entityType, d.entityId].filter(Boolean).join(' ') || '—',
    status,
    departement: '—',
    pole: '—',
    cellule: '—',
    roleMetier: d.role?.trim() || '—',
    ip: d.ipAddress ?? '—',
    device: '—',
    severity,
    actionCode,
    beforeState: {},
    afterState: parsedDetail,
    metadata: parsedDetail,
  };
}

@Component({
  selector: 'app-audit-root',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="prime-page-shell">
      <div>
        <h1 class="text-3xl font-bold text-white tracking-tight">{{ title() }}</h1>
        <p class="text-slate-400 mt-1">
          Tableau structuré avec filtres hiérarchiques, tri, pagination et fiche dans le panneau
          latéral.
        </p>
      </div>

      @if (primeSection.activeAuditSection() === 'dashboard') {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <div
            class="bg-card border border-default rounded-xl p-4 bg-gradient-to-br from-blue-950/35 to-app shadow-sm hover:shadow-[0_10px_25px_rgba(37,99,235,0.18)] hover:scale-[1.03] transition-all duration-300"
          >
            <div class="flex items-center gap-2 text-muted">
              <app-lucide-icon [icon]="icons.database" className="w-4 h-4 text-blue-300" />
              <p class="text-xs">Nombre total</p>
            </div>
            <p class="text-2xl text-primary font-bold mt-2">{{ dashboard().totalPrimes }}</p>
            <p class="text-xs text-muted">fiches auditées</p>
          </div>
          <div
            class="bg-card border border-default rounded-xl p-4 bg-gradient-to-br from-emerald-950/30 to-app shadow-sm hover:shadow-[0_10px_25px_rgba(16,185,129,0.16)] hover:scale-[1.03] transition-all duration-300"
          >
            <div class="flex items-center gap-2 text-muted">
              <app-lucide-icon [icon]="icons.checkCircle" className="w-4 h-4 text-emerald-300" />
              <p class="text-xs">Nombre valide</p>
            </div>
            <p class="text-2xl text-primary font-bold mt-2">{{ dashboard().validations }}</p>
            <p class="text-xs text-muted">validations enregistrées</p>
          </div>
          <div
            class="bg-card border border-default rounded-xl p-4 bg-gradient-to-br from-amber-950/30 to-app shadow-sm hover:shadow-[0_10px_25px_rgba(245,158,11,0.16)] hover:scale-[1.03] transition-all duration-300"
          >
            <div class="flex items-center gap-2 text-muted">
              <app-lucide-icon [icon]="icons.clock" className="w-4 h-4 text-amber-300" />
              <p class="text-xs">Nombre en attente</p>
            </div>
            <p class="text-2xl text-primary font-bold mt-2">{{ dashboard().conformityRate }}%</p>
            <p class="text-xs text-muted">taux de conformité</p>
          </div>
          <div
            class="bg-card border border-default rounded-xl p-4 bg-gradient-to-br from-rose-950/30 to-app shadow-sm hover:shadow-[0_10px_25px_rgba(244,63,94,0.16)] hover:scale-[1.03] transition-all duration-300"
          >
            <div class="flex items-center gap-2 text-muted">
              <app-lucide-icon [icon]="icons.alert" className="w-4 h-4 text-rose-300" />
              <p class="text-xs">Nombre d anomalies</p>
            </div>
            <p class="text-2xl text-primary font-bold mt-2">{{ dashboard().anomalies }}</p>
            <p class="text-xs text-muted">alertes ouvertes</p>
          </div>
        </div>
      }

      @if (primeSection.activeAuditSection() === 'reporting') {
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
          <div class="bg-card border border-default rounded-xl p-4">
            <p class="text-sm text-primary">Conformité des validations</p>
            <p class="text-xs text-muted mt-1">Taux conforme: {{ dashboard().conformityRate }}%</p>
            <p class="text-xs text-muted">Validations: {{ dashboard().validations }} / {{ dashboard().totalPrimes }}</p>
          </div>
          <div class="bg-card border border-default rounded-xl p-4">
            <p class="text-sm text-primary">Anomalies</p>
            <p class="text-xs text-muted mt-1">Ouvertes: {{ dashboard().anomalies }}</p>
            <p class="text-xs text-muted">Résolues: {{ resolvedAnomalyCount() }}</p>
          </div>
          <div class="bg-card border border-default rounded-xl p-4">
            <p class="text-sm text-primary">Journal exploitable</p>
            <p class="text-xs text-muted mt-1">Entrées audit: {{ rows().length }}</p>
            <p class="text-xs text-muted">Filtrées: {{ filtered().length }}</p>
          </div>
        </div>
        @if (reportingError()) {
          <div class="bg-card border border-rose-500/40 rounded-xl p-4 text-sm text-rose-300">
            {{ reportingError() }}
          </div>
        }
      }

      @if (showDataTable()) {
        <div class="space-y-3">
          <div class="bg-card border border-default rounded-xl p-4 space-y-3">
            <div class="flex flex-wrap items-end gap-3">
              <select
                [value]="deptFilter()"
                (change)="onDeptChange($event)"
                [class]="orgSelectClass"
                aria-label="Département"
              >
                @for (d of deptOptions(); track d) {
                  <option [value]="d">{{ d === 'Tous' ? 'Département' : d }}</option>
                }
              </select>
              <select
                [value]="poleFilter()"
                (change)="onPoleChange($event)"
                [class]="orgSelectClass"
                [disabled]="deptFilter() === 'Tous'"
                aria-label="Pôle"
              >
                @for (p of poleOptions(); track p) {
                  <option [value]="p">{{ p === 'Tous' ? 'Pôle' : p }}</option>
                }
              </select>
              <select
                [value]="celluleFilter()"
                (change)="onCelluleChange($event)"
                [class]="orgSelectClass"
                [disabled]="deptFilter() === 'Tous' || poleFilter() === 'Tous'"
                aria-label="Cellule"
              >
                @for (c of celluleOptions(); track c) {
                  <option [value]="c">{{ c === 'Tous' ? 'Cellule' : c }}</option>
                }
              </select>
              <select
                [value]="roleMetierFilter()"
                (change)="onRoleMetierChange($event)"
                [class]="orgSelectClass"
                aria-label="Rôle métier"
              >
                @for (r of roleFilterOptions; track r) {
                  <option [value]="r">{{ r === 'Tous' ? 'Rôle' : r }}</option>
                }
              </select>
              <select
                [value]="severityFilter()"
                (change)="onSeverityChange($event)"
                [class]="orgSelectClass"
                aria-label="Gravité"
              >
                @for (r of severityOptions; track r) {
                  <option [value]="r">{{ r === 'Tous' ? 'Gravité' : r }}</option>
                }
              </select>
              <button
                type="button"
                (click)="resetHierarchyFilters()"
                class="px-3 py-2 rounded-lg border border-default text-sm text-muted hover:bg-app hover:text-primary whitespace-nowrap"
              >
                Réinitialiser filtres
              </button>
              <button
                type="button"
                (click)="toggleInvestigationMode()"
                [class]="investigationButtonClass()"
              >
                Mode investigation
              </button>
            </div>
            <div class="flex flex-wrap gap-2 items-center">
              @for (c of actionChips; track c) {
                <button
                  type="button"
                  (click)="setActionChip(c)"
                  [class]="actionChipClass(c)"
                >
                  {{ c }}
                </button>
              }
            </div>
            <div class="flex flex-wrap items-end gap-3">
              <input
                [value]="search()"
                (input)="onSearchInput($event)"
                placeholder="Recherche"
                [class]="filterBarClass + ' min-w-[160px]'"
              />
              <input
                type="date"
                [value]="dateFilter()"
                (input)="onDateInput($event)"
                [class]="filterBarClass"
              />
              <select
                [value]="userFilter()"
                (change)="onUserChange($event)"
                [class]="orgSelectClass"
              >
                @for (u of users(); track u) {
                  <option [value]="u">{{ u }}</option>
                }
              </select>
              <select
                [value]="actionFilter()"
                (change)="onActionChange($event)"
                [class]="orgSelectClass"
              >
                @for (a of actions(); track a) {
                  <option [value]="a">{{ a }}</option>
                }
              </select>
              <div class="flex gap-2 ml-auto">
                <button
                  type="button"
                  (click)="exportExcel()"
                  class="px-3 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm whitespace-nowrap"
                >
                  Excel
                </button>
                <button
                  type="button"
                  (click)="exportCsv()"
                  class="px-3 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-sm whitespace-nowrap"
                >
                  CSV
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      @if (showDataTable() && hasNoData()) {
        <div class="bg-card border border-default rounded-xl p-4 flex items-center gap-3">
          <app-lucide-icon [icon]="icons.inbox" className="w-5 h-5 text-muted" />
          <div>
            <p class="text-primary text-sm">Aucune entrée dans le journal d’audit</p>
            <p class="text-xs text-muted">Les événements du journal apparaîtront ici dès qu’ils seront disponibles.</p>
          </div>
        </div>
      }

      @if (showDataTable()) {
        <div class="bg-card border border-default rounded-xl overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-app/60 font-semibold">
              <tr>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('date')">
                  Date / heure
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('employee')">
                  Utilisateur
                </th>
                <th class="px-4 py-3 text-left">IP</th>
                <th class="px-4 py-3 text-left">Device</th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('severity')">
                  Gravité
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('departement')">
                  Département
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('pole')">
                  Pôle
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('cellule')">
                  Cellule
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('roleMetier')">
                  Rôle
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('action')">
                  Action
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('item')">
                  Élément
                </th>
                <th class="px-4 py-3 text-left cursor-pointer" (click)="toggleSort('status')">
                  Statut
                </th>
                <th class="px-4 py-3 text-left">Voir</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              @for (r of paged(); track r.id) {
                <tr class="hover:bg-app/40 transition-colors">
                  <td class="px-4 py-3 text-muted">{{ r.date }}</td>
                  <td class="px-4 py-3 text-primary">{{ r.employee }}</td>
                  <td class="px-4 py-3 text-muted">{{ r.ip }}</td>
                  <td class="px-4 py-3 text-muted">{{ r.device }}</td>
                  <td class="px-4 py-3">
                    <span [class]="severityBadgeClass(r.severity)">{{ r.severity }}</span>
                  </td>
                  <td class="px-4 py-3 text-muted">{{ r.departement }}</td>
                  <td class="px-4 py-3 text-muted">{{ r.pole }}</td>
                  <td class="px-4 py-3 text-muted">{{ r.cellule }}</td>
                  <td class="px-4 py-3 text-primary">{{ r.roleMetier }}</td>
                  <td class="px-4 py-3">
                    <span
                      [title]="'Action technique: ' + r.actionCode"
                      class="inline-flex px-2 py-0.5 text-xs rounded-md border border-blue-500/30 text-blue-200"
                    >
                      {{ r.action }}
                    </span>
                  </td>
                  <td class="px-4 py-3 text-muted">{{ r.item }}</td>
                  <td class="px-4 py-3">
                    <span [class]="workflowStatusBadgeClass(r.status)">{{ workflowStatusLabel(r.status) }}</span>
                  </td>
                  <td class="px-4 py-3">
                    <button
                      type="button"
                      (click)="setSelected(r)"
                      class="inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded-md border border-blue-500/30 bg-blue-600/15 hover:bg-blue-500/30 hover:shadow-[0_0_14px_rgba(37,99,235,0.28)] text-blue-200 text-xs transition-all"
                    >
                      <app-lucide-icon [icon]="icons.eye" className="w-3.5 h-3.5" />
                      Voir
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (showDataTable()) {
        <div class="flex items-center justify-between text-sm text-muted">
          <span>Page {{ safePage() }} / {{ totalPages() }}</span>
          <div class="flex gap-2">
            <button
              type="button"
              [disabled]="safePage() <= 1"
              (click)="prevPage()"
              class="px-3 py-1.5 rounded-md border border-default bg-card text-slate-200 hover:bg-navy-800 disabled:cursor-not-allowed disabled:bg-navy-900/80 disabled:text-slate-500"
            >
              Précédent
            </button>
            <button
              type="button"
              [disabled]="safePage() >= totalPages()"
              (click)="nextPage()"
              class="px-3 py-1.5 rounded-md border border-default bg-card text-slate-200 hover:bg-navy-800 disabled:cursor-not-allowed disabled:bg-navy-900/80 disabled:text-slate-500"
            >
              Suivant
            </button>
          </div>
        </div>
      }

      @if (primeSection.activeAuditSection() === 'anomalies') {
        <div class="space-y-3">
          <div
            class="bg-card border border-default rounded-xl p-4 flex items-center gap-2 text-sm text-muted"
          >
            <app-lucide-icon [icon]="icons.alert" className="w-4 h-4 text-rose-300" />
            Suppression massive, accès hors horaires et IP inhabituelle détectés.
          </div>
          @for (a of anomalyCards(); track a.id) {
            <div
              class="bg-card border border-rose-900/40 rounded-xl p-4 flex items-center justify-between gap-3"
            >
              <div class="text-sm text-primary">
                <span [class]="anomalyBadgeClass(a.severity)">{{ a.severity }}</span>
                {{ a.text }}
              </div>
              <div class="flex gap-2 shrink-0">
                <button
                  type="button"
                  (click)="investigateAnomaly(a.user)"
                  class="px-3 py-1.5 rounded-md border border-blue-500/40 text-blue-200 text-xs"
                >
                  Investiguer
                </button>
                <button
                  type="button"
                  (click)="setTimelineUser(a.user)"
                  class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-default text-xs text-muted"
                >
                  <app-lucide-icon [icon]="icons.git" className="w-3.5 h-3.5" />
                  Voir timeline
                </button>
              </div>
            </div>
          }
        </div>
      }

      @if (primeSection.activeAuditSection() === 'access-history') {
        <div class="bg-card border border-default rounded-xl overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-app/60">
              <tr>
                <th class="px-4 py-3 text-left">Utilisateur</th>
                <th class="px-4 py-3 text-left">Date / heure</th>
                <th class="px-4 py-3 text-left">IP</th>
                <th class="px-4 py-3 text-left">Localisation</th>
                <th class="px-4 py-3 text-left">Statut</th>
                <th class="px-4 py-3 text-left">Type</th>
                <th class="px-4 py-3 text-left">Rôle</th>
                <th class="px-4 py-3 text-left">Département</th>
                <th class="px-4 py-3 text-left">Sécurité</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              @for (r of accessRows; track r.id) {
                <tr>
                  <td class="px-4 py-3 text-primary">{{ r.user }}</td>
                  <td class="px-4 py-3 text-muted">{{ r.datetime }}</td>
                  <td class="px-4 py-3 text-muted">{{ r.ip }}</td>
                  <td class="px-4 py-3 text-muted">{{ r.location }}</td>
                  <td class="px-4 py-3">
                    @if (r.success) {
                      <span class="text-emerald-300 text-xs">Succès</span>
                    } @else {
                      <span class="text-rose-300 text-xs">Échec</span>
                    }
                  </td>
                  <td class="px-4 py-3 text-primary">
                    {{ r.type.includes('LOGOUT') ? 'Logout' : 'Login' }}
                  </td>
                  <td class="px-4 py-3 text-primary">{{ r.role }}</td>
                  <td class="px-4 py-3 text-primary">{{ r.departement }}</td>
                  <td class="px-4 py-3">
                    @if (isBruteForce(r)) {
                      <span
                        class="px-2 py-0.5 rounded border border-amber-500/50 text-[10px] text-amber-200 mr-2"
                        >Brute force</span
                      >
                    }
                    @if (r.type === 'SUSPICIOUS') {
                      <span
                        class="px-2 py-0.5 rounded border border-rose-500/50 text-[10px] text-rose-200"
                        >Tentative suspecte</span
                      >
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (selected(); as sel) {
        <div class="fixed inset-0 z-50 flex justify-end bg-navy-950/55">
          <div
            class="w-full max-w-md h-full bg-card border-l border-default p-5 space-y-4 transition-transform duration-300"
            [class.translate-x-0]="drawerOpen()"
            [class.translate-x-full]="!drawerOpen()"
          >
            <div class="flex items-center justify-between">
              <h4 class="text-lg font-semibold text-primary">Détail</h4>
              <button
                type="button"
                (click)="closeDrawer()"
                class="p-1.5 rounded-md hover:bg-app"
              >
                <app-lucide-icon [icon]="icons.x" className="w-4 h-4 text-muted" />
              </button>
            </div>
            <div class="text-sm space-y-3">
              <div>
                <span class="text-muted">Département</span>
                <p class="text-primary">{{ sel.departement }}</p>
              </div>
              <div>
                <span class="text-muted">Pôle</span>
                <p class="text-primary">{{ sel.pole }}</p>
              </div>
              <div>
                <span class="text-muted">Cellule</span>
                <p class="text-primary">{{ sel.cellule }}</p>
              </div>
              <div>
                <span class="text-muted">Rôle</span>
                <p class="text-primary">{{ sel.roleMetier }}</p>
              </div>
              <div>
                <span class="text-muted">Élément</span>
                <p class="text-primary">{{ sel.item }}</p>
              </div>
              <div>
                <span class="text-muted">Modifié par</span>
                <p class="text-primary">{{ sel.employee }}</p>
              </div>
              <div>
                <span class="text-muted">Date</span>
                <p class="text-primary">{{ sel.date }}</p>
              </div>
              <div>
                <span class="text-muted">Statut</span>
                <p><span [class]="workflowStatusBadgeClass(sel.status)">{{ workflowStatusLabel(sel.status) }}</span></p>
              </div>
              <div>
                <span class="text-muted">IP / Device</span>
                <p class="text-primary">{{ sel.ip }} · {{ sel.device }}</p>
              </div>
              <div>
                <span class="text-muted">Avant / Après</span>
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 mt-1">
                  <pre class="p-2 rounded bg-app text-[11px] text-muted overflow-x-auto">{{
                    formatJson(sel.beforeState)
                  }}</pre>
                  <pre class="p-2 rounded bg-app text-[11px] text-emerald-200 overflow-x-auto">{{
                    formatJson(sel.afterState)
                  }}</pre>
                </div>
              </div>
              <div>
                <span class="text-muted">Metadata</span>
                <pre class="mt-1 p-2 rounded bg-app text-[11px] text-muted overflow-x-auto">{{
                  formatJson(sel.metadata)
                }}</pre>
              </div>
              <button
                type="button"
                (click)="searchByEmployee(sel.employee)"
                class="w-full py-2 rounded-lg border border-blue-500/40 bg-blue-600/15 text-blue-200 text-sm"
              >
                Voir toutes les actions de cet utilisateur
              </button>
            </div>
          </div>
        </div>
      }

      @if (timelineUser(); as tu) {
        <div class="fixed inset-0 z-40 bg-navy-950/60 flex items-center justify-center p-4">
          <div class="w-full max-w-2xl bg-card border border-default rounded-xl p-4">
            <div class="flex items-center justify-between">
              <h4 class="text-primary font-semibold inline-flex items-center gap-2">
                <app-lucide-icon [icon]="icons.shield" className="w-4 h-4" />
                Timeline utilisateur
              </h4>
              <button
                type="button"
                (click)="setTimelineUser(null)"
                class="text-muted hover:text-primary"
              >
                <app-lucide-icon [icon]="icons.x" className="w-4 h-4" />
              </button>
            </div>
            <div class="mt-3 space-y-2 max-h-[50vh] overflow-auto">
              @for (r of timelineRows(); track r.id) {
                <div class="text-sm border border-default rounded p-2">
                  <p class="text-primary">{{ r.date }} · {{ r.action }}</p>
                  <p class="text-muted">{{ r.item }}</p>
                </div>
              }
            </div>
          </div>
        </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditRootComponent implements OnInit {
  readonly primeSection = inject(PrimeSectionService);
  private readonly admin = inject(PrimeAdminService);

  readonly icons = {
    alert: AlertTriangle,
    checkCircle: CheckCircle2,
    clock: Clock3,
    database: Database,
    eye: Eye,
    git: GitBranch,
    inbox: Inbox,
    shield: Shield,
    x: X,
  };

  readonly roleFilterOptions = ROLE_FILTER_OPTIONS;
  readonly severityOptions = SEVERITY_OPTIONS;
  readonly actionChips = ACTION_CHIPS;
  readonly filterBarClass =
    'bg-app border border-default rounded-lg px-3 py-2 text-sm text-primary';
  readonly orgSelectClass =
    'bg-app border border-default rounded-lg px-3 py-2 text-sm text-primary min-w-[140px]';

  readonly accessRows: AccessRow[] = [
    {
      id: 'acc-1',
      user: 'siham.lahlou@kyntus.ma',
      datetime: '2026-03-30 09:16:05',
      ip: '105.66.12.99',
      location: 'Oujda, MA',
      success: false,
      type: 'LOGIN_FAILED',
      role: 'RP',
      departement: 'RH',
    },
    {
      id: 'acc-2',
      user: 'siham.lahlou@kyntus.ma',
      datetime: '2026-03-30 09:16:20',
      ip: '105.66.12.99',
      location: 'Oujda, MA',
      success: false,
      type: 'LOGIN_FAILED',
      role: 'RP',
      departement: 'RH',
    },
    {
      id: 'acc-3',
      user: 'siham.lahlou@kyntus.ma',
      datetime: '2026-03-30 09:16:35',
      ip: '105.66.12.99',
      location: 'Oujda, MA',
      success: false,
      type: 'LOGIN_FAILED',
      role: 'RP',
      departement: 'RH',
    },
    {
      id: 'acc-4',
      user: 'siham.lahlou@kyntus.ma',
      datetime: '2026-03-30 09:16:55',
      ip: '105.66.12.99',
      location: 'Oujda, MA',
      success: false,
      type: 'LOGIN_FAILED',
      role: 'RP',
      departement: 'RH',
    },
    {
      id: 'acc-5',
      user: 'siham.lahlou@kyntus.ma',
      datetime: '2026-03-30 09:17:05',
      ip: '105.66.12.99',
      location: 'Oujda, MA',
      success: false,
      type: 'SUSPICIOUS',
      role: 'RP',
      departement: 'RH',
    },
    {
      id: 'acc-6',
      user: 'nadia.benjelloun@kyntus.ma',
      datetime: '2026-03-30 08:12:04',
      ip: '105.66.12.44',
      location: 'Oujda, MA',
      success: true,
      type: 'LOGIN_SUCCESS',
      role: 'Manager',
      departement: 'Sales',
    },
    {
      id: 'acc-7',
      user: 'yassine.touimi@kyntus.ma',
      datetime: '2026-03-30 09:15:00',
      ip: '10.0.0.5',
      location: 'Réseau interne',
      success: true,
      type: 'LOGOUT',
      role: 'Admin',
      departement: 'IT',
    },
  ];

  readonly auditLogs = signal<AuditLogDto[]>([]);
  readonly anomalies = signal<AnomalyDto[]>([]);
  readonly dashboard = signal<AuditDashboardState>({
    totalPrimes: 0,
    validations: 0,
    anomalies: 0,
    conformityRate: 0,
  });
  readonly reportingError = signal<string | null>(null);
  readonly search = signal('');
  readonly dateFilter = signal('');
  readonly userFilter = signal('Tous');
  readonly actionFilter = signal('Tous');
  readonly deptFilter = signal('Tous');
  readonly poleFilter = signal('Tous');
  readonly celluleFilter = signal('Tous');
  readonly roleMetierFilter = signal<string>('Tous');
  readonly severityFilter = signal<(typeof SEVERITY_OPTIONS)[number]>('Tous');
  readonly actionChip = signal<(typeof ACTION_CHIPS)[number]>('Tous');
  readonly investigationMode = signal(false);
  readonly page = signal(1);
  readonly sortKey = signal<SortKey>('date');
  readonly sortDir = signal<SortDir>('desc');
  readonly selected = signal<JournalRow | null>(null);
  readonly drawerOpen = signal(false);
  readonly timelineUser = signal<string | null>(null);

  private drawerTimer: ReturnType<typeof setTimeout> | null = null;

  readonly rows = computed<JournalRow[]>(() => this.auditLogs().map(mapAuditLogToJournalRow));

  readonly deptOptions = computed(() => ['Tous', ...ORG.map((d) => d.dept)]);
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
  readonly actions = computed(() => ['Tous', ...Array.from(new Set(this.rows().map((r) => r.action)))]);

  readonly filtered = computed<JournalRow[]>(() => {
    const q = this.search().trim().toLowerCase();
    const data = this.rows().filter((r) => {
      const qOk =
        !q ||
        `${r.date} ${r.employee} ${r.action} ${r.item} ${r.departement} ${r.pole} ${r.cellule} ${r.roleMetier} ${r.ip} ${r.device}`
          .toLowerCase()
          .includes(q);
      const dOk = !this.dateFilter() || r.date.startsWith(this.dateFilter());
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
    const k = this.sortKey();
    const dir = this.sortDir();
    return [...data].sort((a, b) => {
      const va = String(a[k]).toLowerCase();
      const vb = String(b[k]).toLowerCase();
      if (va === vb) return 0;
      return dir === 'asc' ? (va > vb ? 1 : -1) : va < vb ? 1 : -1;
    });
  });

  readonly pageSize = 8;
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  readonly safePage = computed(() => Math.min(this.page(), this.totalPages()));
  readonly paged = computed(() =>
    this.filtered().slice((this.safePage() - 1) * this.pageSize, this.safePage() * this.pageSize),
  );

  readonly hasNoData = computed(() => this.auditLogs().length === 0);
  readonly anomalyCards = computed<AnomalyCard[]>(() =>
    this.anomalies().slice(0, 8).map((a) => ({
      id: a.id,
      text: a.description,
      user: a.resolvedByUserId || a.targetEntityId || '—',
      severity: a.severity === 'Critical' || a.severity === 'High' ? 'CRITICAL' : 'WARNING',
    })),
  );

  readonly title = computed(() => auditPageTitles[this.primeSection.activeAuditSection()] ?? 'Journal d’audit');
  readonly showDataTable = computed(() => this.primeSection.activeAuditSection() === 'journal');

  readonly validatedCount = computed(() => this.rows().filter((r) => r.status === 'Validé').length);
  readonly pendingCount = computed(() => this.rows().filter((r) => r.status !== 'Validé').length);
  readonly resolvedAnomalyCount = computed(
    () => this.anomalies().filter((a) => String(a.status).toLowerCase() === 'resolved').length,
  );

  readonly timelineRows = computed(() => {
    const u = this.timelineUser();
    return u ? this.rows().filter((r) => r.employee === u) : [];
  });

  constructor() {
    effect(() => {
      void this.deptFilter();
      this.poleFilter.set('Tous');
      this.celluleFilter.set('Tous');
    });

    effect(() => {
      void this.poleFilter();
      this.celluleFilter.set('Tous');
    });

    effect((onCleanup) => {
      const sel = this.selected();
      if (this.drawerTimer) {
        clearTimeout(this.drawerTimer);
        this.drawerTimer = null;
      }
      if (sel) {
        this.drawerTimer = setTimeout(() => this.drawerOpen.set(true), 10);
        onCleanup(() => {
          if (this.drawerTimer) clearTimeout(this.drawerTimer);
        });
      } else {
        this.drawerOpen.set(false);
      }
    });
  }

  ngOnInit(): void {
    this.admin.listAuditLogs({ take: 500 }).subscribe({
      next: (logs) => this.auditLogs.set(logs),
      error: () => this.auditLogs.set([]),
    });
    this.admin.listAnomalies({}).subscribe({
      next: (rows) => this.anomalies.set(rows),
      error: () => this.anomalies.set([]),
    });
    void this.loadAuditDashboard();
  }

  private async loadAuditDashboard(): Promise<void> {
    try {
      const data = await AuditPrimeService.getDashboard();
      this.dashboard.set({
        totalPrimes: data.kpis.totalPrimes ?? 0,
        validations: data.kpis.validations ?? 0,
        anomalies: data.kpis.anomalies ?? 0,
        conformityRate: data.kpis.conformityRate ?? 0,
      });
      this.reportingError.set(null);
    } catch {
      this.reportingError.set('Reporting audit indisponible temporairement (source API).');
    }
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
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortKey.set(k);
      this.sortDir.set('asc');
    }
  }

  toggleInvestigationMode(): void {
    this.investigationMode.update((v) => !v);
  }

  onDeptChange(ev: Event): void {
    this.deptFilter.set((ev.target as HTMLSelectElement).value);
    this.page.set(1);
  }
  onPoleChange(ev: Event): void {
    this.poleFilter.set((ev.target as HTMLSelectElement).value);
    this.page.set(1);
  }
  onCelluleChange(ev: Event): void {
    this.celluleFilter.set((ev.target as HTMLSelectElement).value);
    this.page.set(1);
  }
  onRoleMetierChange(ev: Event): void {
    this.roleMetierFilter.set((ev.target as HTMLSelectElement).value);
    this.page.set(1);
  }
  onSeverityChange(ev: Event): void {
    this.severityFilter.set(
      (ev.target as HTMLSelectElement).value as (typeof SEVERITY_OPTIONS)[number],
    );
    this.page.set(1);
  }
  onSearchInput(ev: Event): void {
    this.search.set((ev.target as HTMLInputElement).value);
    this.page.set(1);
  }
  onDateInput(ev: Event): void {
    this.dateFilter.set((ev.target as HTMLInputElement).value);
    this.page.set(1);
  }
  onUserChange(ev: Event): void {
    this.userFilter.set((ev.target as HTMLSelectElement).value);
    this.page.set(1);
  }
  onActionChange(ev: Event): void {
    this.actionFilter.set((ev.target as HTMLSelectElement).value);
    this.page.set(1);
  }

  setActionChip(c: (typeof ACTION_CHIPS)[number]): void {
    this.actionChip.set(c);
    this.page.set(1);
  }

  prevPage(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  nextPage(): void {
    this.page.update((p) => Math.min(this.totalPages(), p + 1));
  }

  setSelected(r: JournalRow): void {
    this.selected.set(r);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
    setTimeout(() => this.selected.set(null), 220);
  }

  searchByEmployee(employee: string): void {
    this.search.set(employee);
    this.selected.set(null);
  }

  setTimelineUser(user: string | null): void {
    this.timelineUser.set(user);
  }

  investigateAnomaly(user: string): void {
    this.search.set(user);
    this.page.set(1);
  }

  isBruteForce(r: AccessRow): boolean {
    return (
      r.user === 'siham.lahlou@kyntus.ma' &&
      (r.type === 'LOGIN_FAILED' || r.type === 'SUSPICIOUS')
    );
  }

  severityBadgeClass(s: SeverityLevel): string {
    const base = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ';
    if (s === 'CRITICAL') return base + 'bg-rose-100 text-rose-800';
    if (s === 'WARNING') return base + 'bg-amber-100 text-amber-800';
    return base + 'bg-emerald-100 text-emerald-800';
  }

  anomalyBadgeClass(severity: 'CRITICAL' | 'WARNING'): string {
    const base = 'inline-flex mr-2 items-center px-2.5 py-0.5 rounded-full text-xs font-medium ';
    return severity === 'CRITICAL'
      ? base + 'bg-rose-100 text-rose-800'
      : base + 'bg-amber-100 text-amber-800';
  }

  workflowStatusBadgeClass(status: string): string {
    const normalized = status.trim().toLowerCase();
    const base = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ';
    if (normalized.includes('rejet') || normalized.includes('reject'))
      return base + 'bg-rose-100 text-rose-800';
    if (normalized.includes('valid') || normalized.includes('approved'))
      return base + 'bg-emerald-100 text-emerald-800';
    if (normalized.includes('pending') || normalized.includes('attente'))
      return base + 'bg-amber-100 text-amber-800';
    return base + 'bg-sky-100 text-sky-800';
  }

  workflowStatusLabel(status: string): string {
    if (status === 'Pending') return 'En attente';
    if (status === 'Rejected') return 'Rejeté';
    if (status === 'RH Approved') return 'RH validé';
    if (status === 'Référent technique Approved') return 'Réf. technique validé';
    if (status === 'Superviseur Approved') return 'Superviseur validé';
    if (status === 'Chef de projet Approved') return 'Chef de projet validé';
    return status;
  }

  actionChipClass(c: (typeof ACTION_CHIPS)[number]): string {
    const base = 'px-2.5 py-1 rounded-full text-xs border ';
    return this.actionChip() === c
      ? base + 'bg-blue-600/25 border-blue-500/50 text-blue-200'
      : base + 'border-default text-muted';
  }

  investigationButtonClass(): string {
    const base = 'px-3 py-2 rounded-lg border text-sm whitespace-nowrap ';
    return this.investigationMode()
      ? base + 'border-amber-500/50 text-amber-200 bg-amber-500/10'
      : base + 'border-default text-muted';
  }

  formatJson(value: unknown): string {
    return JSON.stringify(value, null, 2);
  }

  exportCsv(): void {
    const exportRows = this.filtered().filter((r) => r.status === 'Validé');
    const csv = toCsv(
      [
        'Employé',
        'Département',
        'Pôle',
        'Cellule',
        'Rôle',
        'Montant',
        'Type de prime',
        'Date validation',
        'Statut',
      ],
      exportRows.map((r) => [
        r.employee,
        r.departement,
        r.pole,
        r.cellule,
        r.roleMetier,
        this.resolveAmount(r),
        'Prime performance',
        r.date,
        r.status,
      ]),
    );
    downloadFile('prime_validees_audit.csv', csv, 'text/csv;charset=utf-8');
  }

  exportExcel(): void {
    const exportRows = this.filtered().filter((r) => r.status === 'Validé');
    const tsv = toCsv(
      [
        'Employé',
        'Département',
        'Pôle',
        'Cellule',
        'Rôle',
        'Montant',
        'Type de prime',
        'Date validation',
        'Statut',
      ],
      exportRows.map((r) => [
        r.employee,
        r.departement,
        r.pole,
        r.cellule,
        r.roleMetier,
        this.resolveAmount(r),
        'Prime performance',
        r.date,
        r.status,
      ]),
    );
    downloadFile('prime_validees_audit.xls', tsv, 'application/vnd.ms-excel');
  }

  private resolveAmount(row: JournalRow): number {
    const m = row.metadata;
    const total = Number((m['totalAmount'] as number | string | undefined) ?? NaN);
    if (Number.isFinite(total)) return total;
    const prime = Number((m['primeAmount'] as number | string | undefined) ?? NaN);
    const challenge = Number((m['challengeAmount'] as number | string | undefined) ?? NaN);
    if (Number.isFinite(prime) || Number.isFinite(challenge)) {
      return (Number.isFinite(prime) ? prime : 0) + (Number.isFinite(challenge) ? challenge : 0);
    }
    return 0;
  }
}
