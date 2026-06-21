import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AllowanceApiService,
  AllowanceRequestDto,
  AllowanceTeamProgressSummaryDto,
  BusinessDepartmentMirrorDto,
  DepartmentContextService,
} from '../../services/allowance-api.service';
import { RoleService } from '../../state/role.service';
import { PrimeNavRequestService } from '../../services/prime-nav-request.service';
import { redirectManagerFromAllowancesIfNeeded } from '../../lib/allowance-manager-guard';
import { allowanceApiErrorMessage } from '../../lib/allowance-api-error';
import { AllowancesPageShellComponent } from '../../components/allowances/allowances-page-shell.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import {
  ALLOWANCE_STATUSES,
  allowanceStatusLabel,
  countByStatus,
  currentAllowancePeriod,
  isPendingRhValidation,
} from '../../lib/allowance-status';

@Component({
  selector: 'app-allowances-dashboard-page',
  standalone: true,
  imports: [CommonModule, FormsModule, AllowancesPageShellComponent, PrimeCardComponent],
  template: `
    <app-allowances-page-shell
      [title]="pageTitle()"
      [subtitle]="pageSubtitle()"
      [error]="loadError()"
    >
      @if (isRhView() || isAdminView()) {
        <app-prime-card title="Filtres" className="ky-card--compact">
          <div class="flex flex-wrap gap-4 items-end">
            <label class="text-sm text-primary">
              Période
              <input class="doc-field mt-1" type="month" [(ngModel)]="filterPeriod" (ngModelChange)="reload()" />
            </label>
            @if (isRhView()) {
              <label class="text-sm text-primary">
                Département
                <select class="doc-field mt-1 min-w-[220px]" [(ngModel)]="filterDeptId" (ngModelChange)="reload()">
                  <option value="">Tous</option>
                  @for (d of departments(); track d.id) {
                    <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
                  }
                </select>
              </label>
            }
          </div>
        </app-prime-card>
      }

      @if (loading()) {
        <div class="flex justify-center py-12">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-500"></div>
        </div>
      } @else {
        @if (isManagerView() && showGettingStarted()) {
          <app-prime-card title="Commencer ici">
            <p class="text-sm text-muted mb-3">
              Parcourez votre équipe N-1, affectez un ou plusieurs types de prime par collaborateur, puis soumettez au RH.
            </p>
            <p class="text-sm text-primary mb-4">
              Équipe N-1 : {{ dept.directReportCount() }} collaborateur(s)
            </p>
            <button type="button" class="btn-primary" (click)="goPilotage()">
              Piloter l'équipe
            </button>
          </app-prime-card>
        }

        @if (isManagerView() && teamSummary() && !showGettingStarted()) {
          <app-prime-card title="Avancement équipe" className="ky-card--compact">
            <p class="text-sm text-primary mb-2">
              {{ teamReviewedCount() }}/{{ teamSummary()!.totalEmployees }} collaborateurs passés en revue
              · {{ teamSummary()!.notStartedCount }} à traiter
              @if (teamSummary()!.inProgressCount > 0) {
                · {{ teamSummary()!.inProgressCount }} avec brouillon(s)
              }
            </p>
            <button type="button" class="btn-primary" (click)="goPilotage()">Piloter l'équipe</button>
          </app-prime-card>
        }

        @if (isManagerView()) {
          <app-prime-card title="À savoir" className="ky-card--compact">
            <p class="text-sm text-muted">
              Les <strong>types de prime</strong> sont définis par le RH (menu Types de prime).
              Vous choisissez le type lors de la création d'une demande.
            </p>
          </app-prime-card>
        }

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          @for (kpi of kpis(); track kpi.label) {
            <div
              class="prime-kpi-card"
              [class.prime-kpi-card--clickable]="kpi.action"
              (click)="kpi.action ? kpi.action() : null"
            >
              <div>
                <p class="prime-kpi-label">{{ kpi.label }}</p>
                <p class="prime-kpi-value">{{ kpi.value }}</p>
              </div>
            </div>
          }
        </div>

        @if (statusBreakdown().length > 0) {
          <app-prime-card title="Répartition par statut">
            <div class="flex flex-wrap gap-2">
              @for (item of statusBreakdown(); track item.status) {
                <span class="prime-status-badge">{{ item.label }} : {{ item.count }}</span>
              }
            </div>
          </app-prime-card>
        }

        @if (isRhView() && inboxCount() > 0) {
          <app-prime-card title="Actions en attente">
            <p class="text-sm text-muted mb-3">
              {{ inboxCount() }} demande(s) en attente de validation RH.
            </p>
            <ul class="space-y-2 mb-4">
              @for (r of inboxPreview(); track r.id) {
                <li class="text-sm text-primary flex flex-wrap justify-between gap-2">
                  <span>{{ r.typeLabel }} — {{ employeeLabel(r.employeeId) }}</span>
                  <span class="text-muted">{{ r.amount | number:'1.0-0' }} MAD</span>
                </li>
              }
            </ul>
            <button type="button" class="btn-primary" (click)="go('/allowances/inbox')">
              Tout valider
            </button>
          </app-prime-card>
        }

        <div class="flex flex-wrap gap-2">
          @if (isManagerView()) {
            <button type="button" class="btn-primary" (click)="goPilotage()">Piloter l'équipe</button>
            <button type="button" class="prime-btn-secondary" (click)="go('/allowances/allocation')">Affectation équipe</button>
          }
          @if (isRhView()) {
            <button type="button" class="btn-primary" (click)="go('/allowances/inbox')">
              Validation RH
              @if (inboxCount() > 0) {
                <span class="ky-badge ml-1">{{ inboxCount() }}</span>
              }
            </button>
            <button type="button" class="prime-btn-secondary" (click)="go('/allowances/supervision')">Suivi global</button>
            <button type="button" class="prime-btn-secondary" (click)="go('/allowances/catalog')">Catalogue</button>
          }
          @if (isAdminView()) {
            <button type="button" class="btn-primary" (click)="go('/allowances/supervision')">Supervision</button>
            <button type="button" class="prime-btn-secondary" (click)="go('/allowances/catalog')">Catalogue</button>
            <button type="button" class="prime-btn-secondary" (click)="goOrgSupport()">Organisation support</button>
          }
        </div>

        @if (isRhView() && showProposalPanel()) {
          <app-prime-card title="Propositions automatiques">
            <p class="text-sm text-muted">
              Les propositions sont générées par chaque manager Support depuis la page Affectation équipe,
              puis soumises au RH après ajustement du montant et du motif.
            </p>
          </app-prime-card>
        }

        <app-prime-card title="Workflow" description="Étapes de validation des primes Support">
          <div class="allowance-workflow-steps">
            @for (step of workflowSteps(); track step.status; let last = $last) {
              <button
                type="button"
                class="allowance-workflow-step"
                [disabled]="step.count === 0"
                (click)="goRequestsForStatus(step.status)"
              >
                <span class="allowance-workflow-step__label">{{ step.label }}</span>
                <span class="allowance-workflow-step__count">{{ step.count }}</span>
              </button>
              @if (!last) {
                <span class="allowance-workflow-step__sep" aria-hidden="true">→</span>
              }
            }
          </div>
        </app-prime-card>
      }
    </app-allowances-page-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .prime-kpi-card--clickable { cursor: pointer; }
    .prime-kpi-card--clickable:hover { opacity: 0.92; }
    .allowance-workflow-steps {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.35rem 0.5rem;
    }
    .allowance-workflow-step {
      display: inline-flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.15rem;
      padding: 0.5rem 0.75rem;
      border-radius: 0.5rem;
      border: 1px solid color-mix(in srgb, var(--border-default, #334155) 80%, transparent);
      background: color-mix(in srgb, var(--bg-input, #0f172a) 50%, transparent);
      color: inherit;
      font: inherit;
      cursor: pointer;
    }
    .allowance-workflow-step:disabled {
      opacity: 0.55;
      cursor: default;
    }
    .allowance-workflow-step__label { font-size: 0.75rem; font-weight: 600; }
    .allowance-workflow-step__count { font-size: 0.875rem; font-weight: 700; }
    .allowance-workflow-step__sep { color: var(--text-muted, #94a3b8); font-size: 0.875rem; }
  `],
})
export class AllowancesDashboardPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);
  readonly dept = inject(DepartmentContextService);
  private readonly role = inject(RoleService);
  private readonly nav = inject(PrimeNavRequestService);

  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly requests = signal<AllowanceRequestDto[]>([]);
  readonly teamSummary = signal<AllowanceTeamProgressSummaryDto | null>(null);
  readonly inboxCount = signal(0);
  readonly inboxRows = signal<AllowanceRequestDto[]>([]);
  readonly departments = signal<BusinessDepartmentMirrorDto[]>([]);

  filterPeriod = currentAllowancePeriod();
  filterDeptId = '';

  readonly isManagerView = computed(() => this.dept.isSupportManager());
  readonly isRhView = computed(() => this.role.currentRole() === 'RH' && !this.dept.isSupportManager());
  readonly isAdminView = computed(() => this.role.currentRole() === 'Admin');

  readonly pageTitle = computed(() => {
    if (this.isManagerView()) return 'Primes Support — Vue d\'ensemble';
    if (this.isRhView()) return 'Primes Support — Synthèse RH';
    if (this.isAdminView()) return 'Primes Support — Supervision Admin';
    return 'Primes Support';
  });

  readonly pageSubtitle = computed(() => {
    if (this.isManagerView()) {
      return `Département ${this.dept.managedDepartmentLabel()} · Effectif N-1 : ${this.dept.directReportCount()}`;
    }
    if (this.isRhView()) return 'Suivi transversal des demandes Support tous départements.';
    if (this.isAdminView()) return 'Indicateurs globaux et accès aux outils de configuration.';
    return '';
  });

  readonly kpis = computed((): Array<{ label: string; value: string | number; action?: () => void }> => {
    const rows = this.requests();
    const total = rows.length;
    const amount = rows.reduce((s, r) => s + r.amount, 0);

    if (this.isManagerView()) {
      const summary = this.teamSummary();
      const total = summary?.totalEmployees ?? this.dept.directReportCount();
      const notStarted = summary?.notStartedCount ?? total;
      const drafts = summary?.inProgressCount ?? 0;
      const submitted = summary?.submittedCount ?? 0;
      const validated = summary?.validatedCount ?? 0;
      const amount = summary?.totalAmount ?? this.requests().reduce((s, r) => s + r.amount, 0);
      return [
        { label: 'Collaborateurs', value: total, action: () => this.goPilotage() },
        { label: 'À traiter', value: notStarted, action: () => this.goPilotage() },
        { label: 'Brouillons', value: drafts, action: () => this.goPilotage() },
        { label: 'Soumis / validés', value: submitted + validated, action: () => this.goPilotage() },
        { label: 'Montant total', value: `${Math.round(amount)} MAD` },
      ];
    }
    if (this.isRhView()) {
      const rhPending = rows.filter((r) => r.status === ALLOWANCE_STATUSES.ManagerApproved).length;
      return [
        { label: 'Demandes période', value: total },
        { label: 'En attente RH', value: this.inboxCount(), action: () => this.go('/allowances/inbox') },
        { label: 'Soumises managers', value: rhPending },
        { label: 'Montant total', value: `${Math.round(amount)} MAD` },
      ];
    }
    if (this.isAdminView()) {
      const paid = rows.filter((r) => r.status === ALLOWANCE_STATUSES.Paid).length;
      const rejected = rows.filter((r) => r.status === ALLOWANCE_STATUSES.Rejected).length;
      return [
        { label: 'Total demandes', value: total },
        { label: 'Payées', value: paid },
        { label: 'Rejetées', value: rejected },
        { label: 'Montant total', value: `${Math.round(amount)} MAD` },
      ];
    }
    return [{ label: 'Demandes', value: total }];
  });

  readonly inboxPreview = computed(() => this.inboxRows().slice(0, 3));

  readonly statusBreakdown = computed(() => {
    const counts = countByStatus(this.requests());
    const viewer = this.isManagerView() ? 'manager' as const : 'stakeholder' as const;
    return Object.entries(counts).map(([status, count]) => ({
      status,
      label: allowanceStatusLabel(status, viewer),
      count,
    }));
  });

  ngOnInit(): void {
    void this.load();
  }

  constructor() {
    effect(() => {
      if (!this.dept.loaded()) return;
      redirectManagerFromAllowancesIfNeeded(this.role.currentRole(), this.dept, this.nav);
    });
  }

  showProposalPanel(): boolean {
    return this.departments().length > 0;
  }

  readonly workflowSteps = computed(() => {
    const counts = countByStatus(this.requests());
    const viewer = this.isManagerView() ? 'manager' as const : 'stakeholder' as const;
    const statuses = [
      ALLOWANCE_STATUSES.Draft,
      ALLOWANCE_STATUSES.ManagerApproved,
      ALLOWANCE_STATUSES.RhApproved,
      ALLOWANCE_STATUSES.ComptaApproved,
      ALLOWANCE_STATUSES.Paid,
    ];
    return statuses.map((status) => ({
      status,
      label: allowanceStatusLabel(status, viewer),
      count: counts[status] ?? 0,
    }));
  });

  readonly teamReviewedCount = computed(() => {
    const s = this.teamSummary();
    if (!s) return 0;
    return Math.max(0, s.totalEmployees - s.notStartedCount);
  });

  showGettingStarted(): boolean {
    const s = this.teamSummary();
    if (s) return s.totalEmployees > 0 && s.notStartedCount === s.totalEmployees && s.inProgressCount === 0;
    return this.requests().length === 0;
  }

  goPilotage(): void {
    this.nav.requestViewWithPeriod('/allowances/allocation', this.filterPeriod.trim() || currentAllowancePeriod());
  }

  goCreateRequest(): void {
    this.nav.requestViewWithAction('/allowances/allocation', 'create');
  }

  goRequestsForStatus(status: string): void {
    this.nav.requestViewWithStatusFilter('/allowances/allocation', status);
  }

  employeeLabel(employeeId: string): string {
    const emp = this.role.employees().find((e) => e.id === employeeId);
    if (emp) return `${emp.firstName} ${emp.lastName}`.trim() || employeeId;
    return employeeId;
  }

  go(path: string): void {
    this.nav.requestView(path);
  }

  goOrgSupport(): void {
    window.location.href = '/departements-metier';
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    this.loadError.set('');
    try {
      const deptId = this.isManagerView()
        ? this.dept.context()?.managedDepartmentId
        : this.filterDeptId.trim() || undefined;
      const period = this.isRhView() || this.isAdminView() ? this.filterPeriod.trim() : currentAllowancePeriod();
      const managerDeptId = this.dept.context()?.managedDepartmentId;
      const [rows, inbox, teamProgress] = await Promise.all([
        this.api.listRequests(deptId, period),
        this.isRhView() ? this.api.inbox() : Promise.resolve([]),
        this.isManagerView() && managerDeptId
          ? this.api.getTeamProgress(period).catch(() => null)
          : Promise.resolve(null),
      ]);
      this.requests.set(rows);
      this.teamSummary.set(teamProgress?.summary ?? null);
      if (this.isRhView()) {
        this.inboxRows.set(inbox);
        this.inboxCount.set(inbox.length);
      } else {
        this.inboxRows.set([]);
        this.inboxCount.set(0);
      }
    } catch (e: unknown) {
      this.loadError.set(allowanceApiErrorMessage(e, 'Impossible de charger les données Primes Support.'));
    } finally {
      this.loading.set(false);
    }
  }

  private async load(): Promise<void> {
    await this.dept.load();
    try {
      if (this.isRhView() || this.isAdminView()) {
        const depts = await this.api.listBusinessDepartments();
        this.departments.set(depts.filter((d) => d.kind === 'Support' && d.isActive));
      }
      await this.reload();
    } catch (e: unknown) {
      this.loadError.set(allowanceApiErrorMessage(e, 'Impossible de charger les données Primes Support.'));
      this.loading.set(false);
    }
  }
}
