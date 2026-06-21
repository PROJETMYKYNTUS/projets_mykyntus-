import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AllowanceApiService,
  AllowanceRequestDto,
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
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          @for (kpi of kpis(); track kpi.label) {
            <div class="prime-kpi-card">
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

        <div class="flex flex-wrap gap-2">
          @if (isManagerView()) {
            <button type="button" class="btn-primary" (click)="go('/allowances/requests')">Nouvelle demande</button>
            <button type="button" class="prime-btn-secondary" (click)="go('/allowances/requests')">Suivi des demandes</button>
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
              Les propositions sont générées par chaque manager Support depuis la page Demandes de prime,
              puis soumises au RH après ajustement du montant et du motif.
            </p>
          </app-prime-card>
        }

        <app-prime-card title="Workflow" description="Étapes de validation des primes Support">
          Brouillon → Soumis au RH → Validé RH → Validé compta → Payé
        </app-prime-card>
      }
    </app-allowances-page-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesDashboardPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);
  readonly dept = inject(DepartmentContextService);
  private readonly role = inject(RoleService);
  private readonly nav = inject(PrimeNavRequestService);

  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly requests = signal<AllowanceRequestDto[]>([]);
  readonly inboxCount = signal(0);
  readonly departments = signal<BusinessDepartmentMirrorDto[]>([]);

  filterPeriod = currentAllowancePeriod();
  filterDeptId = '';

  readonly isManagerView = computed(() => this.dept.isSupportManager());
  readonly isRhView = computed(() => this.role.currentRole() === 'RH' && !this.dept.isSupportManager());
  readonly isAdminView = computed(() => this.role.currentRole() === 'Admin');

  readonly pageTitle = computed(() => {
    if (this.isManagerView()) return 'Primes Support — Tableau de bord';
    if (this.isRhView()) return 'Primes Support — Vue RH';
    if (this.isAdminView()) return 'Primes Support — Supervision Admin';
    return 'Primes Support — Tableau de bord';
  });

  readonly pageSubtitle = computed(() => {
    if (this.isManagerView()) {
      return `Département ${this.dept.managedDepartmentLabel()} · Effectif N-1 : ${this.dept.directReportCount()}`;
    }
    if (this.isRhView()) return 'Suivi transversal des demandes Support tous départements.';
    if (this.isAdminView()) return 'Indicateurs globaux et accès aux outils de configuration.';
    return '';
  });

  readonly kpis = computed(() => {
    const rows = this.requests();
    const total = rows.length;
    const amount = rows.reduce((s, r) => s + r.amount, 0);

    if (this.isManagerView()) {
      const pendingRh = rows.filter((r) => isPendingRhValidation(r.status)).length;
      const drafts = rows.filter((r) => r.status === ALLOWANCE_STATUSES.Draft).length;
      return [
        { label: 'Demandes du mois', value: total },
        { label: 'Brouillons', value: drafts },
        { label: 'En attente RH', value: pendingRh },
        { label: 'Montant total', value: `${Math.round(amount)} MAD` },
      ];
    }
    if (this.isRhView()) {
      const rhPending = rows.filter((r) => r.status === ALLOWANCE_STATUSES.ManagerApproved).length;
      return [
        { label: 'Demandes période', value: total },
        { label: 'En attente RH', value: this.inboxCount() },
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
      const [rows, inbox] = await Promise.all([
        this.api.listRequests(deptId, period),
        this.isRhView() ? this.api.inbox() : Promise.resolve([]),
      ]);
      this.requests.set(rows);
      this.inboxCount.set(inbox.length);
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
