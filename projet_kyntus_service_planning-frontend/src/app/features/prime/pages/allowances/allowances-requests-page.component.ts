import { ChangeDetectionStrategy, Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AllowanceApiService,
  AllowanceRequestDto,
  AllowanceTeamMemberDto,
  AllowanceTypeDto,
  DepartmentContextService,
} from '../../services/allowance-api.service';
import { RoleService } from '../../state/role.service';
import { PrimeNavRequestService } from '../../services/prime-nav-request.service';
import { AllowanceRequestTableComponent } from '../../components/allowances/allowance-request-table.component';
import { AllowanceRequestFormModalComponent } from '../../components/allowances/allowance-request-form-modal.component';
import { AllowanceStatusBadgeComponent } from '../../components/allowances/allowance-status-badge.component';
import { redirectManagerFromAllowancesIfNeeded } from '../../lib/allowance-manager-guard';
import { AllowancesPageShellComponent } from '../../components/allowances/allowances-page-shell.component';
import { allowanceApiErrorMessage } from '../../lib/allowance-api-error';
import {
  ALLOWANCE_STATUSES,
  currentAllowancePeriod,
  isPendingRhValidation,
  validateAllowanceAmount,
} from '../../lib/allowance-status';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';
import { KyntusFormDraftService } from '../../../../core/drafts/kyntus-form-draft.service';
import { KyntusObjectDraftBinder } from '../../../../core/drafts/kyntus-object-draft.binder';
import { BodyPortalDirective } from '../../../../shared/directives/body-portal.directive';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';

type KpiFilter = 'pending' | 'validated' | 'rejected';
interface AllowanceKpi {
  label: string;
  count: number;
  color: string;
  filterStatus: KpiFilter;
}

@Component({
  selector: 'app-allowances-requests-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AllowanceRequestTableComponent,
    AllowanceRequestFormModalComponent,
    AllowancesPageShellComponent,
    AllowanceStatusBadgeComponent,
    BodyPortalDirective,
  ],
  template: `
    <div class="allowance-requests-page">
      <app-allowances-page-shell
        title="Créer / gérer les demandes"
        subtitle="Prime extra pour votre équipe — soumission au RH en un clic."
        [error]="loadError()"
      >
        <div pageActions>
          @if (isManager()) {
            <button type="button" class="allowance-cta" (click)="openCreateForm()">
              + Créer une demande
            </button>
          }
        </div>

        <div class="allowance-toolbar">
          <label class="allowance-toolbar__period">
            <span>Période</span>
            <input type="month" [(ngModel)]="filterPeriod" (ngModelChange)="reloadList()" />
          </label>
          @if (statusFilter()) {
            <button type="button" class="allowance-toolbar__chip allowance-toolbar__chip--active" (click)="clearStatusFilter()">
              Filtre actif ×
            </button>
          }
          @if (isManager()) {
            <button type="button" class="allowance-toolbar__link" (click)="showAdvanced.set(!showAdvanced())">
              {{ showAdvanced() ? 'Masquer options' : 'Options avancées' }}
            </button>
          }
        </div>

        <div class="allowance-kpis">
          @for (kpi of kpis(); track kpi.label) {
            <button
              type="button"
              class="allowance-kpi"
              [class.allowance-kpi--active]="statusFilter() === kpi.filterStatus"
              [disabled]="kpi.count === 0"
              (click)="applyKpiFilter(kpi.filterStatus)"
            >
              <span class="allowance-kpi__value" [style.color]="kpi.color">{{ kpi.count }}</span>
              <span class="allowance-kpi__label">{{ kpi.label }}</span>
            </button>
          }
        </div>

        @if (isManager() && team().length > 0) {
          <div class="allowance-team-bar">
            <span class="allowance-team-bar__label">Équipe N-1</span>
            <div class="allowance-team-bar__chips">
              @for (m of team(); track m.id) {
                <button type="button" class="allowance-team-chip" (click)="openCreateFormForMember(m.id)">
                  <span>{{ memberLabel(m) }}</span>
                  <span class="allowance-team-chip__action">+ Demande</span>
                </button>
              }
            </div>
          </div>
        }

        @if (showAdvanced() && isManager()) {
          <details class="allowance-advanced">
            <summary>Génération automatique de brouillons (optionnel)</summary>
            <div class="allowance-advanced__body">
              <p>Règles RH → brouillons pré-remplis à ajuster avant soumission.</p>
              <div class="allowance-advanced__row">
                <input type="month" [(ngModel)]="proposalPeriod" />
                <button type="button" class="allowance-btn-secondary" [disabled]="generating()" (click)="generateProposals()">
                  @if (generating()) { <span class="allowance-spinner allowance-spinner--dark"></span> }
                  Générer
                </button>
              </div>
            </div>
          </details>
        }

        @if (loading()) {
          <div class="allowance-loading">
            <span class="allowance-spinner allowance-spinner--lg"></span>
            <span>Chargement…</span>
          </div>
        } @else {
          <app-allowance-request-table
            [rows]="displayRows()"
            [employeeLabel]="employeeLabelFn"
            [showDraftActions]="isManager()"
            [showEmptyCreateAction]="isManager()"
            [compact]="true"
            [statusViewer]="'manager'"
            (submitDraft)="submit($event)"
            (editDraft)="openEditForm($event)"
            (viewDetail)="openDetail($event)"
            (createRequest)="openCreateForm()"
          />
        }
      </app-allowances-page-shell>
    </div>

    <app-allowance-request-form-modal
      [open]="showForm()"
      [title]="editingId() ? 'Modifier le brouillon' : 'Créer une demande'"
      [submitLabel]="editingId() ? 'Enregistrer' : 'Créer brouillon'"
      [saving]="saving()"
      [error]="error()"
      [editingId]="editingId()"
      [employeeName]="employeeLabel(formEmployeeId)"
      [team]="team()"
      [types]="types()"
      [employeeId]="formEmployeeId"
      [typeId]="formTypeId"
      [period]="formPeriod"
      [amount]="formAmount"
      [reason]="formReason"
      (employeeIdChange)="formEmployeeId = $event; touchDraft()"
      (typeIdChange)="onTypeIdChange($event)"
      (periodChange)="formPeriod = $event; touchDraft()"
      (amountChange)="formAmount = $event; touchDraft()"
      (reasonChange)="formReason = $event; touchDraft()"
      (submitted)="saveForm()"
      (cancelled)="closeForm()"
      (resetRequested)="resetDraftForm()"
    />

    @if (detailRow()) {
      <div class="allowance-modal-backdrop" appBodyPortal (click)="closeDetail()">
        <div class="allowance-detail-modal" role="dialog" (click)="$event.stopPropagation()">
          <header class="allowance-detail-modal__header">
            <h2>Détail demande</h2>
            <button type="button" class="allowance-modal__close" (click)="closeDetail()">×</button>
          </header>
          <dl class="allowance-detail-modal__grid">
            <div><dt>Collaborateur</dt><dd>{{ employeeLabel(detailRow()!.employeeId) }}</dd></div>
            <div><dt>Type</dt><dd>{{ detailRow()!.typeLabel }}</dd></div>
            <div><dt>Période</dt><dd>{{ detailRow()!.period }}</dd></div>
            <div><dt>Montant</dt><dd class="allowance-detail-modal__amount">{{ detailRow()!.amount | number:'1.0-0' }} MAD</dd></div>
            <div class="allowance-detail-modal__full"><dt>Motif</dt><dd>{{ detailRow()!.reason || '—' }}</dd></div>
            <div><dt>Statut</dt><dd><app-allowance-status-badge [status]="detailRow()!.status" [viewer]="'manager'" /></dd></div>
          </dl>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .allowance-requests-page { --allowance-primary: var(--electric-blue); --allowance-success: var(--success); --allowance-danger: var(--danger); }
    .allowance-cta {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.625rem 1.25rem;
      border: none;
      border-radius: var(--radius-md);
      background-color: var(--blue-600);
      background-image: var(--ky-gradient);
      color: white;
      font-size: 0.875rem;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 14px color-mix(in srgb, var(--allowance-primary) 35%, transparent);
      transition: transform 0.1s, box-shadow 0.15s;
    }
    .allowance-cta:hover { transform: translateY(-1px); box-shadow: 0 6px 20px color-mix(in srgb, var(--allowance-primary) 45%, transparent); }
    .allowance-toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }
    .allowance-toolbar__period {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.8125rem;
      color: var(--text-muted);
    }
    .allowance-toolbar__period input {
      padding: 0.375rem 0.625rem;
      border-radius: var(--radius-md);
      border: 1px solid var(--border-color);
      background: var(--bg-input);
      font-size: 0.875rem;
    }
    .allowance-toolbar__link {
      margin-left: auto;
      background: none;
      border: none;
      font-size: 0.8125rem;
      color: var(--allowance-primary);
      cursor: pointer;
      font-weight: 600;
    }
    .allowance-toolbar__chip {
      padding: 0.25rem 0.625rem;
      border-radius: var(--radius-pill);
      font-size: 0.75rem;
      border: 1px solid var(--allowance-primary);
      background: color-mix(in srgb, var(--allowance-primary) 10%, transparent);
      color: var(--allowance-primary);
      cursor: pointer;
    }
    .allowance-kpis {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.75rem;
      margin-bottom: 1rem;
    }
    @media (max-width: 640px) { .allowance-kpis { grid-template-columns: 1fr; } }
    .allowance-kpi {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.125rem;
      padding: 1rem 1.125rem;
      border-radius: var(--radius-card);
      border: 1px solid color-mix(in srgb, var(--border-color) 90%, transparent);
      background: var(--bg-card);
      cursor: pointer;
      text-align: left;
      transition: border-color 0.15s, box-shadow 0.15s, transform 0.1s;
    }
    .allowance-kpi:hover:not(:disabled) {
      border-color: color-mix(in srgb, var(--allowance-primary) 40%, transparent);
      box-shadow: 0 4px 12px color-mix(in srgb, var(--allowance-primary) 8%, transparent);
      transform: translateY(-1px);
    }
    .allowance-kpi:disabled { cursor: default; opacity: 0.85; }
    .allowance-kpi--active { border-color: var(--allowance-primary); box-shadow: 0 0 0 2px color-mix(in srgb, var(--allowance-primary) 15%, transparent); }
    .allowance-kpi__value { font-size: 1.75rem; font-weight: 800; line-height: 1; }
    .allowance-kpi__label { font-size: 0.75rem; font-weight: 600; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.04em; }
    .allowance-team-bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.625rem;
      margin-bottom: 1rem;
      padding: 0.75rem 1rem;
      border-radius: var(--radius-md);
      background: color-mix(in srgb, var(--allowance-primary) 5%, var(--bg-card));
      border: 1px solid color-mix(in srgb, var(--allowance-primary) 12%, transparent);
    }
    .allowance-team-bar__label { font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.05em; color: var(--text-muted); }
    .allowance-team-bar__chips { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .allowance-team-chip {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.75rem;
      border-radius: var(--radius-pill);
      border: 1px solid var(--border-color);
      background: var(--bg-card);
      font-size: 0.8125rem;
      cursor: pointer;
      transition: border-color 0.12s, box-shadow 0.12s;
    }
    .allowance-team-chip:hover {
      border-color: var(--allowance-primary);
      box-shadow: 0 2px 8px color-mix(in srgb, var(--allowance-primary) 12%, transparent);
    }
    .allowance-team-chip__action { font-weight: 700; color: var(--allowance-primary); font-size: 0.75rem; }
    .allowance-advanced {
      margin-bottom: 1rem;
      border-radius: var(--radius-md);
      border: 1px dashed var(--border-color);
      padding: 0.5rem 0.875rem;
      font-size: 0.8125rem;
      color: var(--text-muted);
    }
    .allowance-advanced summary { cursor: pointer; font-weight: 600; color: var(--text-primary); }
    .allowance-advanced__body { padding-top: 0.625rem; }
    .allowance-advanced__row { display: flex; gap: 0.5rem; margin-top: 0.5rem; align-items: center; }
    .allowance-btn-secondary {
      padding: 0.375rem 0.875rem;
      border-radius: var(--radius-md);
      border: 1px solid var(--border-color);
      background: var(--bg-card);
      font-size: 0.8125rem;
      font-weight: 600;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
    }
    .allowance-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      padding: 3rem;
      color: var(--text-muted);
      font-size: 0.875rem;
    }
    .allowance-spinner {
      width: 1rem;
      height: 1rem;
      border: 2px solid color-mix(in srgb, var(--allowance-primary) 20%, transparent);
      border-top-color: var(--allowance-primary);
      border-radius: 50%;
      animation: allowance-spin 0.6s linear infinite;
    }
    .allowance-spinner--lg { width: 1.75rem; height: 1.75rem; border-width: 3px; }
    .allowance-spinner--dark { border-color: color-mix(in srgb, var(--text-primary) 10%, transparent); border-top-color: var(--text-primary); width: 0.875rem; height: 0.875rem; }
    .allowance-modal-backdrop {
      position: fixed; inset: 0; z-index: 55;
      background: color-mix(in srgb, var(--bg-primary) 55%, transparent);
      backdrop-filter: blur(4px);
      display: flex; align-items: center; justify-content: center; padding: 1rem;
    }
    .allowance-detail-modal {
      width: 100%; max-width: 24rem;
      background: var(--bg-card);
      border-radius: var(--radius-card);
      padding: 1.25rem;
      box-shadow: var(--shadow-3);
    }
    .allowance-detail-modal__header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .allowance-detail-modal__header h2 { margin: 0; font-size: 1rem; font-weight: 700; }
    .allowance-detail-modal__grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; font-size: 0.875rem; }
    .allowance-detail-modal__full { grid-column: 1 / -1; }
    .allowance-detail-modal__grid dt { font-size: 0.6875rem; text-transform: uppercase; letter-spacing: 0.04em; color: var(--text-muted); margin-bottom: 0.125rem; }
    .allowance-detail-modal__grid dd { margin: 0; font-weight: 600; color: var(--text-primary); }
    .allowance-detail-modal__amount { color: var(--allowance-primary) !important; font-size: 1.125rem !important; }
    @keyframes allowance-spin { to { transform: rotate(360deg); } }
  `],
})
export class AllowancesRequestsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(AllowanceApiService);
  private readonly dept = inject(DepartmentContextService);
  private readonly role = inject(RoleService);
  private readonly nav = inject(PrimeNavRequestService);
  private readonly toast = inject(KyntusToastService);
  private readonly confirmService = inject(KyntusConfirmService);
  private readonly formDrafts = inject(KyntusFormDraftService);
  private draftBinder?: KyntusObjectDraftBinder<{
    showForm: boolean;
    editingId: string | null;
    formEmployeeId: string;
    formTypeId: string;
    formPeriod: string;
    formAmount: number;
    formReason: string;
  }>;

  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly rows = signal<AllowanceRequestDto[]>([]);
  readonly types = signal<AllowanceTypeDto[]>([]);
  readonly team = signal<AllowanceTeamMemberDto[]>([]);
  readonly showForm = signal(false);
  readonly showAdvanced = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly generating = signal(false);
  readonly statusFilter = signal<string | null>(null);
  readonly detailRow = signal<AllowanceRequestDto | null>(null);

  filterPeriod = currentAllowancePeriod();
  proposalPeriod = currentAllowancePeriod();
  formEmployeeId = '';
  formTypeId = '';
  formPeriod = currentAllowancePeriod();
  formAmount = 0;
  formReason = '';

  readonly employeeLabelFn = (id: string) => this.employeeLabel(id);

  readonly displayRows = computed(() => {
    const filter = this.statusFilter();
    if (!filter) return this.rows();
    if (filter === 'pending') {
      return this.rows().filter((r) =>
        r.status === ALLOWANCE_STATUSES.Draft || isPendingRhValidation(r.status),
      );
    }
    if (filter === 'validated') {
      const validatedStatuses: string[] = [
        ALLOWANCE_STATUSES.RhApproved,
        ALLOWANCE_STATUSES.ComptaApproved,
        ALLOWANCE_STATUSES.Paid,
      ];
      return this.rows().filter((r) => validatedStatuses.includes(r.status));
    }
    if (filter === 'rejected') {
      return this.rows().filter((r) => r.status === ALLOWANCE_STATUSES.Rejected);
    }
    return this.rows().filter((r) => r.status === filter);
  });

  readonly kpis = computed((): AllowanceKpi[] => {
    const rows = this.rows();
    const pending = rows.filter((r) =>
      r.status === ALLOWANCE_STATUSES.Draft || isPendingRhValidation(r.status),
    ).length;
    const validatedStatuses: string[] = [
      ALLOWANCE_STATUSES.RhApproved,
      ALLOWANCE_STATUSES.ComptaApproved,
      ALLOWANCE_STATUSES.Paid,
    ];
    const validated = rows.filter((r) => validatedStatuses.includes(r.status)).length;
    const rejected = rows.filter((r) => r.status === ALLOWANCE_STATUSES.Rejected).length;
    return [
      { label: 'En attente', count: pending, color: 'var(--warning-text)', filterStatus: 'pending' },
      { label: 'Validées', count: validated, color: 'var(--success-text)', filterStatus: 'validated' },
      { label: 'Refusées', count: rejected, color: 'var(--danger-text)', filterStatus: 'rejected' },
    ];
  });

  ngOnInit(): void {
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'allowance-request-form',
      () => ({
        showForm: this.showForm(),
        editingId: this.editingId(),
        formEmployeeId: this.formEmployeeId,
        formTypeId: this.formTypeId,
        formPeriod: this.formPeriod,
        formAmount: this.formAmount,
        formReason: this.formReason,
      }),
      (s) => {
        if (typeof s.formEmployeeId === 'string') this.formEmployeeId = s.formEmployeeId;
        if (typeof s.formTypeId === 'string') this.formTypeId = s.formTypeId;
        if (typeof s.formPeriod === 'string') this.formPeriod = s.formPeriod;
        if (typeof s.formAmount === 'number') this.formAmount = s.formAmount;
        if (typeof s.formReason === 'string') this.formReason = s.formReason;
        if (s.editingId) this.editingId.set(s.editingId);
        if (s.showForm && (s.formReason || s.formEmployeeId || s.formTypeId)) {
          this.showForm.set(true);
        }
      },
    );
    this.draftBinder.start();
    void this.load();
  }

  ngOnDestroy(): void {
    this.draftBinder?.destroy();
  }

  touchDraft(): void {
    this.draftBinder?.touch();
  }

  async resetDraftForm(): Promise<void> {
    if (!this.showForm()) return;
    const ok = await this.confirmService.confirm({
      title: 'Réinitialiser',
      message: 'Réinitialiser le formulaire de demande et le brouillon ?',
      confirmLabel: 'Réinitialiser',
    });
    if (!ok) return;
    this.draftBinder?.discard();
    const editId = this.editingId();
    if (editId) {
      const row = this.rows().find((r) => r.id === editId);
      if (row) this.openEditForm(row);
      else this.openCreateForm();
    } else {
      this.openCreateForm();
    }
    this.restartDraftBinder();
  }

  private restartDraftBinder(): void {
    this.draftBinder?.destroy();
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'allowance-request-form',
      () => ({
        showForm: this.showForm(),
        editingId: this.editingId(),
        formEmployeeId: this.formEmployeeId,
        formTypeId: this.formTypeId,
        formPeriod: this.formPeriod,
        formAmount: this.formAmount,
        formReason: this.formReason,
      }),
      (s) => {
        if (typeof s.formEmployeeId === 'string') this.formEmployeeId = s.formEmployeeId;
        if (typeof s.formTypeId === 'string') this.formTypeId = s.formTypeId;
        if (typeof s.formPeriod === 'string') this.formPeriod = s.formPeriod;
        if (typeof s.formAmount === 'number') this.formAmount = s.formAmount;
        if (typeof s.formReason === 'string') this.formReason = s.formReason;
        if (s.editingId) this.editingId.set(s.editingId);
        if (s.showForm && (s.formReason || s.formEmployeeId || s.formTypeId)) {
          this.showForm.set(true);
        }
      },
    );
    this.draftBinder.start();
  }

  constructor() {
    effect(() => {
      if (!this.dept.loaded()) return;
      redirectManagerFromAllowancesIfNeeded(this.role.currentRole(), this.dept, this.nav);
    });

    effect(() => {
      const action = this.nav.requestedAction();
      if (action !== 'create') return;
      if (!this.dept.loaded() || !this.isManager()) return;
      this.openCreateForm();
      this.nav.clearRequestedAction();
    });

    effect(() => {
      const status = this.nav.requestedStatusFilter();
      if (!status) return;
      if (!this.dept.loaded()) return;
      this.statusFilter.set(status === ALLOWANCE_STATUSES.Draft ? 'pending' : status);
      this.nav.clearRequestedStatusFilter();
    });
  }

  isManager(): boolean {
    return this.dept.isSupportManager();
  }

  memberLabel(m: AllowanceTeamMemberDto): string {
    return `${m.firstName} ${m.lastName}`.trim() || m.email || m.id;
  }

  employeeLabel(employeeId: string): string {
    if (!employeeId) return '—';
    const fromTeam = this.team().find((m) => m.id === employeeId);
    if (fromTeam) return this.memberLabel(fromTeam);
    const fromRole = this.role.employees().find((e) => e.id === employeeId);
    if (fromRole) return `${fromRole.firstName} ${fromRole.lastName}`.trim() || employeeId;
    return employeeId;
  }

  selectedType(): AllowanceTypeDto | undefined {
    return this.types().find((t) => t.id === this.formTypeId);
  }

  onTypeIdChange(typeId: string): void {
    this.formTypeId = typeId;
    const t = this.types().find((x) => x.id === typeId);
    if (t?.defaultAmount != null && !this.editingId()) {
      this.formAmount = t.defaultAmount;
    }
    this.draftBinder?.touch();
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.formEmployeeId = '';
    this.formTypeId = '';
    this.formPeriod = this.filterPeriod.trim() || currentAllowancePeriod();
    this.formAmount = 0;
    this.formReason = '';
    this.error.set('');
    this.showForm.set(true);
  }

  openCreateFormForMember(employeeId: string): void {
    this.openCreateForm();
    this.formEmployeeId = employeeId;
  }

  openEditForm(row: AllowanceRequestDto): void {
    this.editingId.set(row.id);
    this.formEmployeeId = row.employeeId;
    this.formTypeId = row.allowanceTypeId;
    this.formPeriod = row.period;
    this.formAmount = row.amount;
    this.formReason = row.reason;
    this.error.set('');
    this.showForm.set(true);
  }

  openDetail(row: AllowanceRequestDto): void {
    this.detailRow.set(row);
  }

  closeDetail(): void {
    this.detailRow.set(null);
  }

  closeForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.error.set('');
  }

  clearStatusFilter(): void {
    this.statusFilter.set(null);
  }

  applyKpiFilter(filter: KpiFilter): void {
    this.statusFilter.set(this.statusFilter() === filter ? null : filter);
  }

  async saveForm(): Promise<void> {
    const type = this.selectedType();
    if (type) {
      const validationError = validateAllowanceAmount(this.formAmount, type, this.formReason);
      if (validationError) {
        this.error.set(validationError);
        return;
      }
    }
    this.saving.set(true);
    this.error.set('');
    try {
      const editId = this.editingId();
      if (editId) {
        await this.api.updateDraft(editId, {
          allowanceTypeId: this.formTypeId,
          period: this.formPeriod.trim(),
          amount: this.formAmount,
          reason: this.formReason,
        });
        this.toast.success('Brouillon mis à jour');
      } else {
        await this.api.createRequest({
          employeeId: this.formEmployeeId.trim(),
          allowanceTypeId: this.formTypeId,
          period: this.formPeriod.trim(),
          amount: this.formAmount,
          reason: this.formReason,
        });
        this.toast.success('Demande créée — soumettez-la au RH quand vous êtes prêt');
      }
      this.draftBinder?.clear();
      this.closeForm();
      await this.reloadList();
    } catch (e: unknown) {
      this.error.set(allowanceApiErrorMessage(e, 'Erreur enregistrement'));
    } finally {
      this.saving.set(false);
    }
  }

  async submit(id: string): Promise<void> {
    try {
      await this.api.submit(id);
      this.toast.success('Demande soumise au RH');
      await this.reloadList();
    } catch (e: unknown) {
      this.toast.error(allowanceApiErrorMessage(e, 'Erreur lors de la soumission au RH.'));
    }
  }

  async generateProposals(): Promise<void> {
    this.generating.set(true);
    try {
      const deptId = this.dept.context()?.managedDepartmentId;
      const result = await this.api.generateProposals(this.proposalPeriod.trim(), deptId);
      this.toast.success(`${result.created} brouillon(s) généré(s)`);
      await this.reloadList();
    } catch (e: unknown) {
      this.toast.error(allowanceApiErrorMessage(e, 'Erreur lors de la génération.'));
    } finally {
      this.generating.set(false);
    }
  }

  async reloadList(): Promise<void> {
    this.loadError.set('');
    try {
      const deptId = this.dept.context()?.managedDepartmentId;
      this.rows.set(await this.api.listRequests(deptId, this.filterPeriod.trim()));
    } catch (e: unknown) {
      this.loadError.set(allowanceApiErrorMessage(e, 'Impossible de charger les demandes.'));
    }
  }

  private async load(): Promise<void> {
    await this.dept.load();
    try {
      const deptId = this.dept.context()?.managedDepartmentId;
      const tasks: Promise<void>[] = [
        this.api.listRequests(deptId, this.filterPeriod.trim()).then((rows) => this.rows.set(rows)),
        this.api.listEligibleTypes(deptId).then((types) => this.types.set(types)),
      ];
      if (this.dept.isSupportManager()) {
        tasks.push(this.api.listTeamMembers().then((team) => this.team.set(team)));
      }
      await Promise.all(tasks);
    } finally {
      this.loading.set(false);
    }
  }
}
