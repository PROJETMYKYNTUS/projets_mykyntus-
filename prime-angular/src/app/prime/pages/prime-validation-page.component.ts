import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { forkJoin } from 'rxjs';
import { AlertCircle, Check, CheckCheck, X } from 'lucide';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { PrimeCardComponent } from '../components/prime-card.component';
import {
  PrimeFilterBarComponent,
  type PrimeFilterBarFilter,
} from '../components/prime-filter-bar.component';
import type { Employee, Role } from '../models';
import { RoleService } from '../state/role.service';
import {
  PrimeFicheResultService,
  type EmployeePrimeServiceFicheValidationDto,
  type PrimeFicheValidationStatus,
  type WorkflowValidationMetaDto,
  type WorkflowValidationSummaryDto,
} from '../services/prime-fiche-result.service';
import { primeHttpErrorDetail } from '../lib/primeHttpErrorMessage';
import { formatWorkflowPipeline } from '../lib/workflow-step-rechain';
import { rolesMatchWorkflowApprover } from '../lib/workflow-role-match';

interface RoleConfig {
  /** Statut(s) d’entrée des fiches que ce rôle doit valider (workflow DB). */
  fromStatuses?: PrimeFicheValidationStatus[];
  /** Statut courant des fiches que ce rôle doit valider (entrée du flux). */
  fromStatus: PrimeFicheValidationStatus | null;
  /** Statut résultant après approbation. */
  toStatus: PrimeFicheValidationStatus | null;
  /** Libellé d'aide en haut de la page. */
  helper: string;
  /**
   * Lecture seule : sans filtre de statut, restreindre l’affichage au périmètre service (référent technique).
   * `all` = toutes les fiches de la période (vue transverse lecture seule).
   */
  readOnlyScope?: 'service' | 'all';
}

/** Fallback si workflow-meta indisponible (aligné PrimeValidationWorkflowService). */
const ROLE_STEP_MAP: Record<string, RoleConfig> = {
  'Référent technique': {
    fromStatus: 'Pending',
    toStatus: 'Référent technique Approved',
    helper:
      'Niveau 1 : vous validez les fiches en attente sur votre service avant le Superviseur.',
  },
  Coach: {
    fromStatus: 'Pending',
    toStatus: 'Référent technique Approved',
    helper:
      'Niveau 1 (rôle historique « Coach ») : même validation que le référent technique sur les fiches en attente.',
  },
  Superviseur: {
    fromStatus: 'Référent technique Approved',
    toStatus: 'Superviseur Approved',
    helper: 'Niveau 2 : vous validez les fiches après le référent technique, avant le Chef de projet.',
  },
  'Chef de projet': {
    fromStatus: 'Superviseur Approved',
    toStatus: 'Chef de projet Approved',
    helper: 'Niveau 3 : validation finale opérationnelle des fiches (après Superviseur).',
  },
  RP: {
    fromStatus: 'Superviseur Approved',
    toStatus: 'Chef de projet Approved',
    helper:
      'Niveau 3 (rôle historique « RP ») : validation finale après le Superviseur.',
  },
  Manager: {
    fromStatus: null,
    toStatus: null,
    readOnlyScope: 'all',
    helper:
      'La validation des fiches service ne concerne pas le rôle Manager. Utilisez « Synthèse globale PRIME » pour valider le fichier agrégé (Manager + RH), puis la comptabilité.',
  },
  Comptabilité: {
    fromStatus: null,
    toStatus: null,
    readOnlyScope: 'all',
    helper:
      'Lecture transverse : la comptabilité accuse réception du fichier global depuis l’écran « Synthèse globale PRIME » — pas de validation de fiche ici.',
  },
  RH: {
    fromStatus: null,
    toStatus: null,
    readOnlyScope: 'all',
    helper:
      'La RH valide le fichier synthèse globale PRIME (avec le Manager), pas les fiches individuelles — menu « Synthèse globale ».',
  },
  Admin: {
    fromStatus: null,
    toStatus: null,
    readOnlyScope: 'all',
    helper: 'Vue Administrateur : lecture seule des validations en cours (toutes étapes).',
  },
  Audit: {
    fromStatus: null,
    toStatus: null,
    readOnlyScope: 'all',
    helper: 'Vue Audit : lecture seule du journal des validations.',
  },
};

/** Rôle attendu par l'API de validation (backend métier). Manager ne valide pas les fiches. */
function mapRoleForApi(role: string): string {
  if (role === 'Coach') return 'Référent technique';
  if (role === 'RP') return 'Chef de projet';
  if (role === 'Comptable') return 'Comptabilité';
  return role;
}

@Component({
  selector: 'app-prime-validation-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent, PrimeFilterBarComponent],
  template: `
    @if (loading()) {
      <div class="p-8 flex justify-center">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
      </div>
    } @else {
      <div class="p-8 space-y-6">
        <div class="flex justify-between items-start gap-4">
          <div>
            <h1 class="text-3xl font-bold text-slate-900 tracking-tight">Validation des fiches PRIME</h1>
            <p class="text-slate-500 mt-1">
              Fiches : {{ workflowPipelineLabel() }}. RH / Manager / Comptabilité : fichier synthèse globale.
            </p>
          </div>
          @if (canBulkApprove()) {
            <button
              type="button"
              [disabled]="bulkBusy() || actionableRows().length === 0"
              (click)="bulkApprove()"
              class="bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg font-medium flex items-center gap-2 transition-colors shadow-sm"
            >
              <app-lucide-icon [icon]="icons.checkAll" className="w-4 h-4" />
              Tout approuver ({{ actionableRows().length }})
            </button>
          }
        </div>

        <div class="bg-blue-50 border border-blue-200 rounded-xl p-4 flex items-start gap-3">
          <app-lucide-icon [icon]="icons.alert" className="w-5 h-5 text-blue-600 mt-0.5" />
          <div>
            <h4 class="text-sm font-semibold text-blue-900">Rôle : {{ roleService.currentRole() }}</h4>
            <p class="text-sm text-blue-700 mt-1">{{ roleHelper() }}</p>
          </div>
        </div>

        @if (errorMessage()) {
          <app-prime-card>
            <div class="p-4 text-rose-600 text-sm">{{ errorMessage() }}</div>
          </app-prime-card>
        }

        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          @for (kpi of statusCounters(); track kpi.status) {
            <button
              type="button"
              (click)="setStatusFilter(kpi.status)"
              class="text-left rounded-xl border bg-card p-3 transition-all hover:border-indigo-300"
              [class.border-indigo-500]="statusFilter() === kpi.status"
              [class.border-default]="statusFilter() !== kpi.status"
            >
              <div class="text-xs uppercase tracking-wider text-muted">{{ kpi.label }}</div>
              <div class="mt-1 text-2xl font-bold text-primary">{{ kpi.count }}</div>
            </button>
          }
        </div>

        <app-prime-filter-bar [filters]="filterBarFilters()" />

        <app-prime-card className="p-0">
          <div class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-slate-400 uppercase bg-navy-900 border-b border-navy-800">
                <tr>
                  <th class="px-6 py-3 font-medium tracking-wider">Pilote</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Périmètre</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Période</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Montant</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Statut</th>
                  <th class="px-6 py-3 font-medium tracking-wider text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-navy-800">
                @if (filteredResults().length === 0) {
                  <tr>
                    <td colspan="6" class="px-6 py-8 text-center text-slate-500">
                      Aucune fiche à afficher pour ces critères.
                    </td>
                  </tr>
                } @else {
                  @for (item of filteredResults(); track item.id) {
                    <tr class="bg-navy-900 hover:bg-navy-800 transition-colors">
                      <td class="px-6 py-4 whitespace-nowrap">
                        @let emp = getEmployee(item.employeeId);
                        <div>
                          <div class="font-medium text-slate-200">
                            {{ displayName(emp, item.employeeId) }}
                          </div>
                          <div class="text-xs text-slate-500">{{ emp?.role || '—' }}</div>
                        </div>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-300">
                        <div class="text-xs uppercase tracking-wider text-slate-500">Cellule</div>
                        <div class="font-medium">{{ item.celluleId }}</div>
                        <div class="text-xs text-slate-500 mt-1">Service: {{ item.serviceId }}</div>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap font-mono text-slate-200">{{ item.period }}</td>
                      <td class="px-6 py-4 whitespace-nowrap">
                        <div class="font-semibold text-slate-200">
                          {{ formatAmount(item.totalAmount) }}
                        </div>
                        <div class="text-xs text-slate-500">
                          Prime {{ formatAmount(item.primeAmount) }} • Chal.
                          {{ formatAmount(item.challengeAmount) }}
                        </div>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap">
                        <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-800">
                          {{ statusLabel(item.validationStatus) }}
                        </span>
                        @if (item.validationStatus === 'Rejected' && item.rejectionReason) {
                          <div class="text-xs text-rose-500 mt-1 italic max-w-xs truncate" [title]="item.rejectionReason">
                            « {{ item.rejectionReason }} »
                          </div>
                        }
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-right">
                        <div class="flex items-center justify-end gap-2">
                          @if (canApproveRow(item)) {
                            <button
                              type="button"
                              [disabled]="busyId() === item.id"
                              (click)="approve(item.id)"
                              class="p-1.5 text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 rounded-md transition-colors border border-transparent hover:border-emerald-200 disabled:opacity-50"
                              title="Approuver"
                            >
                              <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                            </button>
                            <button
                              type="button"
                              [disabled]="busyId() === item.id"
                              (click)="reject(item.id)"
                              class="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-md transition-colors border border-transparent hover:border-rose-200 disabled:opacity-50"
                              title="Rejeter (motif obligatoire)"
                            >
                              <app-lucide-icon [icon]="icons.x" className="w-4 h-4" />
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </app-prime-card>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeValidationPageComponent {
  readonly roleService = inject(RoleService);
  private readonly api = inject(PrimeFicheResultService);

  readonly icons = { alert: AlertCircle, check: Check, x: X, checkAll: CheckCheck };

  readonly workflowMeta = signal<WorkflowValidationMetaDto | null>(null);
  readonly summaryDto = signal<WorkflowValidationSummaryDto | null>(null);
  readonly periodOptions = signal<{ label: string; value: string }[]>([]);
  readonly results = signal<EmployeePrimeServiceFicheValidationDto[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);
  readonly bulkBusy = signal(false);
  readonly statusFilter = signal<PrimeFicheValidationStatus | ''>('');
  readonly periodFilter = signal('');

  readonly setStatusFilter = (value: PrimeFicheValidationStatus | '') => {
    this.statusFilter.set(this.statusFilter() === value ? '' : value);
  };
  readonly setPeriodFilter = (value: string): void => {
    this.periodFilter.set(value);
  };

  readonly workflowPipelineLabel = computed(() =>
    formatWorkflowPipeline(this.workflowMeta()?.steps),
  );

  readonly roleConfig = computed<RoleConfig>(() => {
    const uiRole = this.roleService.currentRole() as string;
    const r = mapRoleForApi(uiRole);
    const meta = this.workflowMeta();
    const pipeline = this.workflowPipelineLabel();
    const actionable =
      meta?.actionableFromStatuses?.length
        ? meta.actionableFromStatuses
        : meta?.steps
            .filter((s) => s.isActive && rolesMatchWorkflowApprover(r, s.approverRole))
            .map((s) => s.fromStatus) ?? [];
    const fromStatuses = [...new Set(actionable)] as PrimeFicheValidationStatus[];
    const step = meta?.steps
      .filter((s) => s.isActive && rolesMatchWorkflowApprover(r, s.approverRole))
      .sort((a, b) => a.sortOrder - b.sortOrder)[0];
    if (step) {
      const level =
        [...(meta?.steps ?? [])]
          .filter((s) => s.isActive)
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .findIndex((s) => s.id === step.id) + 1;
      const statusHint =
        fromStatuses.length > 1
          ? fromStatuses.map((s) => `« ${s} »`).join(' ou ')
          : `« ${step.fromStatus} »`;
      return {
        fromStatuses,
        fromStatus: (fromStatuses[0] ?? step.fromStatus) as PrimeFicheValidationStatus,
        toStatus: step.toStatus as PrimeFicheValidationStatus,
        helper: `Niveau ${level} — validez les fiches au statut ${statusHint} (après approbation : « ${step.toStatus} »). Chaîne active : ${pipeline}.`,
      };
    }
    const fallback = ROLE_STEP_MAP[uiRole];
    if (fallback) {
      const fs = fallback.fromStatus ? [fallback.fromStatus] : [];
      return {
        ...fallback,
        fromStatuses: fs,
        helper: `${fallback.helper} Chaîne active : ${pipeline}.`,
      };
    }
    return {
      fromStatuses: [],
      fromStatus: null,
      toStatus: null,
      helper: `Aucune étape de validation pour ce rôle. Chaîne active : ${pipeline}.`,
    };
  });

  readonly roleHelper = computed(() => this.roleConfig().helper);

  readonly canBulkApprove = computed(() => {
    const role = this.roleService.currentRole() as Role;
    return (
      role !== 'Pilote' &&
      role !== 'Audit' &&
      role !== 'Manager' &&
      role !== 'Comptabilité' &&
      (this.roleConfig().fromStatuses?.length ?? 0) > 0
    );
  });

  readonly actionableRows = computed(() => {
    const cfg = this.roleConfig();
    const statuses = cfg.fromStatuses ?? [];
    if (statuses.length === 0) return [];
    const set = new Set(statuses);
    return this.results().filter((r) => set.has(r.validationStatus));
  });

  readonly statusCounters = computed(() => {
    const sum = this.summaryDto();
    if (sum?.statusCounts?.length) {
      return sum.statusCounts.map((sc) => ({
        status: sc.status as PrimeFicheValidationStatus,
        label: sc.status,
        count: sc.count,
      }));
    }
    const rows = this.results();
    const distinct = [...new Set(rows.map((r) => r.validationStatus))].sort();
    return distinct.map((status) => ({
      status: status as PrimeFicheValidationStatus,
      label: status,
      count: rows.filter((r) => r.validationStatus === status).length,
    }));
  });

  readonly filteredResults = computed(() => {
    const status = this.statusFilter();
    const rows = this.results();
    if (status) return rows.filter((r) => r.validationStatus === status);
    const cfg = this.roleConfig();
    const statuses = cfg.fromStatuses ?? [];
    if (statuses.length > 0) {
      const set = new Set(statuses);
      return rows.filter((r) => set.has(r.validationStatus));
    }
    if (cfg.readOnlyScope === 'service') {
      const sid = this.roleService.currentUser().serviceId;
      return rows.filter((r) => r.serviceId === sid);
    }
    if (cfg.readOnlyScope === 'all') return rows;
    return rows;
  });

  readonly filterBarFilters = computed<PrimeFilterBarFilter[]>(() => {
    const opts = this.periodOptions();
    const list = opts.length > 0 ? opts : [{ label: '—', value: '' }];
    return [
      {
        name: 'Période',
        value: this.periodFilter() || list[0]?.value || '',
        onChange: this.setPeriodFilter,
        options: list,
      },
    ];
  });

  constructor() {
    effect(() => {
      void this.roleService.currentRole();
      void this.roleService.currentUser().id;
      void this.periodFilter();
      this.fetch();
    });
  }

  private fetch(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    const u = this.roleService.currentUser();
    const r = mapRoleForApi(this.roleService.currentRole() as string);
    const period = this.periodFilter() || undefined;
    forkJoin({
      meta: this.api.workflowMeta(r),
      periods: this.api.periods(),
      rows: this.api.list({ period, userId: u.id, role: r }),
      summary: this.api.summary({ period, userId: u.id, role: r }),
    }).subscribe({
      next: ({ meta, periods, rows, summary }) => {
        this.workflowMeta.set(meta);
        this.summaryDto.set(summary);
        const opts = periods.map((p) => ({ label: p, value: p }));
        this.periodOptions.set(opts);
        if (!this.periodFilter() && periods.length > 0) this.periodFilter.set(periods[0]!);
        this.results.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('[PrimeValidationPage] fetch error', err);
        const detail = primeHttpErrorDetail(err);
        this.errorMessage.set(
          detail
            ? `Impossible de charger les fiches PRIME. ${detail}`
            : 'Impossible de charger les fiches PRIME depuis le backend. Vérifiez que le service est démarré.',
        );
        this.results.set([]);
        this.loading.set(false);
      },
    });
  }

  getEmployee(id: string): Employee | undefined {
    return this.roleService.employees().find((e) => e.id === id);
  }

  displayName(emp: Employee | undefined, fallback: string): string {
    return emp ? `${emp.firstName} ${emp.lastName}` : fallback;
  }

  statusLabel(status: string): string {
    return status;
  }

  formatAmount(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `${value.toFixed(2)} MAD`;
  }

  canApproveRow(row: EmployeePrimeServiceFicheValidationDto): boolean {
    const cfg = this.roleConfig();
    const statuses = cfg.fromStatuses ?? [];
    if (statuses.length === 0) return false;
    return statuses.includes(row.validationStatus);
  }

  approve(id: string): void {
    const role = mapRoleForApi(this.roleService.currentRole() as string);
    const user = this.roleService.currentUser();
    this.busyId.set(id);
    this.api
      .approve(id, { userId: user.id, role })
      .subscribe({
        next: (updated) => {
          this.results.update((rows) => rows.map((r) => (r.id === id ? updated : r)));
          this.busyId.set(null);
        },
        error: (err) => {
          console.error('[PrimeValidationPage] approve error', err);
          this.errorMessage.set(err?.error?.error || 'Erreur lors de l’approbation.');
          this.busyId.set(null);
        },
      });
  }

  reject(id: string): void {
    const role = mapRoleForApi(this.roleService.currentRole() as string);
    const user = this.roleService.currentUser();
    const reason = (window.prompt('Motif de rejet (obligatoire) :') ?? '').trim();
    if (!reason) return;
    this.busyId.set(id);
    this.api
      .reject(id, { userId: user.id, role, reason })
      .subscribe({
        next: (updated) => {
          this.results.update((rows) => rows.map((r) => (r.id === id ? updated : r)));
          this.busyId.set(null);
        },
        error: (err) => {
          console.error('[PrimeValidationPage] reject error', err);
          this.errorMessage.set(err?.error?.error || 'Erreur lors du rejet.');
          this.busyId.set(null);
        },
      });
  }

  bulkApprove(): void {
    const role = mapRoleForApi(this.roleService.currentRole() as string);
    const user = this.roleService.currentUser();
    const ids = this.actionableRows().map((r) => r.id);
    if (ids.length === 0) return;
    this.bulkBusy.set(true);
    this.api
      .bulkApprove({ userId: user.id, role, ficheIds: ids })
      .subscribe({
        next: () => {
          this.bulkBusy.set(false);
          this.fetch();
        },
        error: (err) => {
          console.error('[PrimeValidationPage] bulkApprove error', err);
          this.errorMessage.set(err?.error?.error || 'Erreur lors de l’approbation groupée.');
          this.bulkBusy.set(false);
        },
      });
  }
}
