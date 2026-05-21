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
import type { Department, Employee, Role } from '../models';
import { PrimeService } from '../services/prime.service';
import { RoleService } from '../state/role.service';
import {
  PrimeFicheResultService,
  type EmployeePrimeServiceFicheValidationDto,
  type PrimeFicheValidationStatus,
  type WorkflowValidationMetaDto,
  type WorkflowValidationSummaryDto,
} from '../services/prime-fiche-result.service';
import { PRIME_USER_LOAD_ERROR, primeHttpErrorDetail } from '../lib/primeHttpErrorMessage';
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
      <div class="prime-page-shell">
        <div class="flex justify-between items-start gap-4">
          <div>
            <h1 class="prime-page-title">Validation des fiches PRIME</h1>
            <p class="prime-page-subtitle">
              Validez les fiches de votre périmètre selon le circuit
              {{ workflowPipelineLabel() }}. RH, Manager et Comptabilité traitent la synthèse globale.
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

        <div class="prime-callout-info">
          <app-lucide-icon [icon]="icons.alert" className="w-5 h-5 text-blue-500 mt-0.5 shrink-0" />
          <div>
            <h4 class="prime-callout-title">Rôle : {{ roleService.currentRole() }}</h4>
            <p class="prime-callout-body">{{ roleHelper() }}</p>
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
            <table class="prime-table">
              <thead>
                <tr>
                  <th>Pilote</th>
                  <th>Périmètre</th>
                  <th>Période</th>
                  <th>Montant</th>
                  <th>Statut</th>
                  <th class="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                @if (filteredResults().length === 0) {
                  <tr>
                    <td colspan="6" class="text-center prime-cell-muted py-8">
                      {{ emptyListMessage() }}
                    </td>
                  </tr>
                } @else {
                  @for (item of filteredResults(); track item.id) {
                    <tr>
                      <td>
                        @let emp = getEmployee(item.employeeId);
                        <div>
                          <div class="prime-cell-strong">
                            {{ displayPilotName(item, emp) }}
                          </div>
                          <div class="text-xs prime-cell-muted">{{ item.employeeRole || emp?.role || '—' }}</div>
                        </div>
                      </td>
                      <td>
                        @let org = orgLabels(item);
                        <div class="text-xs uppercase tracking-wider prime-cell-muted">Cellule</div>
                        <div class="prime-cell-strong">{{ org.cellule }}</div>
                        <div class="text-xs prime-cell-muted mt-1">Service: {{ org.service }}</div>
                        @if (org.pole) {
                          <div class="text-xs prime-cell-muted mt-0.5">Pôle: {{ org.pole }}</div>
                        }
                      </td>
                      <td class="font-mono">{{ item.period }}</td>
                      <td>
                        <div class="font-semibold">
                          {{ formatAmount(item.totalAmount) }}
                        </div>
                        <div class="text-xs prime-cell-muted">
                          Prime {{ formatAmount(item.primeAmount) }} • Chal.
                          {{ formatAmount(item.challengeAmount) }}
                        </div>
                      </td>
                      <td>
                        <span class="prime-status-badge">
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
                              class="p-1.5 text-muted hover:text-emerald-400 hover:bg-navy-800 rounded-md transition-colors border border-transparent hover:border-emerald-500/40 disabled:opacity-50"
                              title="Approuver"
                            >
                              <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                            </button>
                            <button
                              type="button"
                              [disabled]="busyId() === item.id"
                              (click)="reject(item.id)"
                              class="p-1.5 text-muted hover:text-rose-400 hover:bg-navy-800 rounded-md transition-colors border border-transparent hover:border-rose-500/40 disabled:opacity-50"
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
  readonly departments = signal<Department[]>([]);
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
      return {
        fromStatuses,
        fromStatus: (fromStatuses[0] ?? step.fromStatus) as PrimeFicheValidationStatus,
        toStatus: step.toStatus as PrimeFicheValidationStatus,
        helper: `Niveau ${level} — validez les fiches en attente à votre étape du circuit de validation.`,
      };
    }
    const fallback = ROLE_STEP_MAP[uiRole];
    if (fallback) {
      const fs = fallback.fromStatus ? [fallback.fromStatus] : [];
      return {
        ...fallback,
        fromStatuses: fs,
        helper: fallback.helper,
      };
    }
    return {
      fromStatuses: [],
      fromStatus: null,
      toStatus: null,
      helper: 'Aucune étape de validation pour ce rôle.',
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

  readonly emptyListMessage = computed(() => {
    const cfg = this.roleConfig();
    if ((cfg.fromStatuses?.length ?? 0) > 0) {
      return 'Aucune fiche prête dans votre périmètre pour cette période (partie commune validée et saisie cellule complète).';
    }
    return 'Aucune fiche à afficher pour ces critères.';
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
    const uiRole = this.roleService.currentRole() as string;
    const r = mapRoleForApi(uiRole);
    const period = this.periodFilter() || undefined;
    const listFilters = {
      period,
      userId: u.id,
      role: r,
      readyOnly: true as const,
    };
    forkJoin({
      meta: this.api.workflowMeta(r),
      periods: this.api.periods(),
      rows: this.api.list(listFilters),
      summary: this.api.summary(listFilters),
      departments: PrimeService.getDepartments(),
    }).subscribe({
      next: ({ meta, periods, rows, summary, departments }) => {
        this.workflowMeta.set(meta);
        this.summaryDto.set(summary);
        this.departments.set(departments);
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
            : PRIME_USER_LOAD_ERROR,
        );
        this.results.set([]);
        this.loading.set(false);
      },
    });
  }

  getEmployee(id: string): Employee | undefined {
    return this.roleService.employees().find((e) => e.id === id);
  }

  displayPilotName(
    item: EmployeePrimeServiceFicheValidationDto,
    emp: Employee | undefined,
  ): string {
    const fromDto = (item.employeeDisplayName ?? '').trim();
    if (fromDto) return fromDto;
    return emp ? `${emp.firstName} ${emp.lastName}` : item.employeeId;
  }

  orgLabels(item: EmployeePrimeServiceFicheValidationDto): {
    cellule: string;
    service: string;
    pole: string | null;
  } {
    const cellFromDto = (item.celluleName ?? '').trim();
    const svcFromDto = (item.serviceName ?? '').trim();
    if (cellFromDto && svcFromDto) {
      return {
        cellule: cellFromDto,
        service: svcFromDto,
        pole: item.poleName?.trim() || null,
      };
    }
    const emp = this.getEmployee(item.employeeId);
    const deptById = new Map(this.departments().map((d) => [d.id, d]));
    const dept = deptById.get(emp?.departementId ?? emp?.poleId ?? '');
    const pole = dept?.poles.find((p) => p.id === (emp?.poleId ?? item.celluleId));
    const cellule = pole?.cells.find((c) => c.id === item.celluleId);
    const service = cellule?.teams.find((t) => t.id === item.serviceId);
    return {
      cellule: cellule?.name ?? item.celluleId,
      service: service?.name ?? item.serviceId,
      pole: item.poleName?.trim() || pole?.name || null,
    };
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
