import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AllowanceApiService,
  AllowanceRequestDto,
  BusinessDepartmentMirrorDto,
} from '../../services/allowance-api.service';
import { RoleService } from '../../state/role.service';
import { PrimeNavRequestService } from '../../services/prime-nav-request.service';
import { AllowanceRequestTableComponent } from '../../components/allowances/allowance-request-table.component';
import { AllowancesPageShellComponent } from '../../components/allowances/allowances-page-shell.component';
import {
  ALLOWANCE_STATUSES,
  allowanceStatusLabel,
  canValidateAtStep,
  currentAllowancePeriod,
} from '../../lib/allowance-status';

@Component({
  selector: 'app-allowances-supervision-page',
  standalone: true,
  imports: [CommonModule, FormsModule, AllowanceRequestTableComponent, AllowancesPageShellComponent],
  template: `
    <app-allowances-page-shell
      title="Supervision — Primes Support"
      subtitle="Vue globale des demandes tous départements Support (lecture seule)."
    >
      <div pageActions>
        <button
          type="button"
          class="prime-btn-secondary"
          (click)="exportCsv()"
          [disabled]="filteredRows().length === 0"
        >
          Exporter CSV
        </button>
      </div>

      <div class="sup-toolbar">
        <label class="sup-field">
          <span>Période</span>
          <input class="doc-field" type="month" [(ngModel)]="filterPeriod" (ngModelChange)="reload()" />
        </label>
        <label class="sup-field">
          <span>Département</span>
          <select class="doc-field" [(ngModel)]="filterDeptId" (ngModelChange)="reload()">
            <option value="">Tous</option>
            @for (d of departments(); track d.id) {
              <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
            }
          </select>
        </label>
        <label class="sup-field">
          <span>Statut</span>
          <select class="doc-field" [(ngModel)]="filterStatus" (ngModelChange)="applyStatusFilter()">
            <option value="">Tous</option>
            @for (s of statusOptions; track s) {
              <option [value]="s">{{ statusLabel(s) }}</option>
            }
          </select>
        </label>
      </div>

      @if (loading()) {
        <p class="sup-hint">Chargement…</p>
      } @else {
        <div class="sup-meta">
          <p class="sup-count">{{ filteredRows().length }} demande(s)</p>
          @if (rhActionableCount() > 0 && isRh()) {
            <button type="button" class="sup-inbox-link" (click)="goInbox()">
              {{ rhActionableCount() }} demande(s) en attente dans votre file RH
            </button>
          }
        </div>

        <app-allowance-request-table
          [rows]="filteredRows()"
          [employeeLabel]="employeeLabelFn"
          [departmentLabel]="deptLabelFn"
          [showDepartment]="true"
          [showDraftActions]="false"
          emptyTitle="Aucune demande"
          emptyText="Aucune demande pour ces filtres. Les managers Support créent les primes depuis leur espace."
        />
      }
    </app-allowances-page-shell>
  `,
  styles: [`
    .sup-toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem 1.25rem;
      align-items: flex-end;
      padding: 1rem 1.15rem;
      background: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-card, 0.875rem);
    }
    .sup-field {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      min-width: 10rem;
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--text-primary);
    }
    .sup-field .doc-field {
      min-width: 11rem;
      font-weight: 500;
    }
    .sup-hint {
      margin: 0;
      font-size: 0.875rem;
      color: var(--text-muted);
    }
    .sup-meta {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem 1rem;
      margin-top: 0.15rem;
    }
    .sup-count {
      margin: 0;
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--text-muted);
    }
    .sup-inbox-link {
      border: none;
      background: none;
      padding: 0;
      font: inherit;
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--electric-blue);
      cursor: pointer;
      text-decoration: underline;
      text-underline-offset: 2px;
    }
    .sup-inbox-link:hover {
      color: var(--text-primary);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesSupervisionPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);
  private readonly role = inject(RoleService);
  private readonly nav = inject(PrimeNavRequestService);

  readonly loading = signal(true);
  readonly rows = signal<AllowanceRequestDto[]>([]);
  readonly departments = signal<BusinessDepartmentMirrorDto[]>([]);

  filterPeriod = currentAllowancePeriod();
  filterDeptId = '';
  filterStatus = '';

  readonly statusOptions = Object.values(ALLOWANCE_STATUSES);

  readonly filteredRows = computed(() => {
    let list = this.rows();
    if (this.filterStatus.trim()) {
      list = list.filter((r) => r.status === this.filterStatus.trim());
    }
    return list;
  });

  readonly rhActionableCount = computed(() =>
    this.rows().filter((r) => canValidateAtStep('RH', r.status)).length,
  );

  readonly employeeLabelFn = (id: string) => this.employeeLabel(id);
  readonly deptLabelFn = (id: string) => this.deptLabel(id);

  ngOnInit(): void {
    void this.init();
  }

  isRh(): boolean {
    return this.role.currentRole() === 'RH';
  }

  statusLabel(status: string): string {
    return allowanceStatusLabel(status);
  }

  employeeLabel(employeeId: string): string {
    const emp = this.role.employees().find((e) => e.id === employeeId);
    if (emp) return `${emp.firstName} ${emp.lastName}`.trim() || employeeId;
    return employeeId;
  }

  deptLabel(deptId: string): string {
    const d = this.departments().find((x) => x.id === deptId);
    if (d) return `${d.code} — ${d.name}`;
    return deptId || '—';
  }

  goInbox(): void {
    this.nav.requestView('/allowances/inbox');
  }

  applyStatusFilter(): void {
    // computed handles filtering
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    try {
      const dept = this.filterDeptId.trim() || undefined;
      const period = this.filterPeriod.trim() || undefined;
      this.rows.set(await this.api.listRequests(dept, period));
    } finally {
      this.loading.set(false);
    }
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;
    const header = ['Employé', 'Type', 'Période', 'Montant', 'Devise', 'Département', 'Statut', 'Source'];
    const lines = rows.map((r) =>
      [
        this.employeeLabel(r.employeeId),
        r.typeLabel,
        r.period,
        String(r.amount),
        r.currency,
        this.deptLabel(r.businessDepartmentId),
        allowanceStatusLabel(r.status),
        r.source,
      ]
        .map((c) => `"${String(c).replace(/"/g, '""')}"`)
        .join(','),
    );
    const csv = [header.join(','), ...lines].join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `primes-support-${this.filterPeriod || 'export'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  private async init(): Promise<void> {
    try {
      const depts = await this.api.listBusinessDepartments();
      this.departments.set(depts.filter((d) => d.kind === 'Support' && d.isActive));
      await this.reload();
    } finally {
      this.loading.set(false);
    }
  }
}
