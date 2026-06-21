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
import {
  ALLOWANCE_STATUSES,
  allowanceStatusLabel,
  canValidateAtStep,
  currentAllowancePeriod,
} from '../../lib/allowance-status';

@Component({
  selector: 'app-allowances-supervision-page',
  standalone: true,
  imports: [CommonModule, FormsModule, AllowanceRequestTableComponent],
  template: `
    <div class="space-y-6">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-xl font-semibold text-primary">Supervision — Primes Support</h1>
          <p class="text-sm text-muted mt-1">Vue globale des demandes tous départements Support (lecture seule).</p>
        </div>
        <button type="button" class="btn-secondary text-sm" (click)="exportCsv()" [disabled]="filteredRows().length === 0">
          Exporter CSV
        </button>
      </div>

      <div class="flex flex-wrap gap-3 items-end">
        <label class="text-sm">
          Période
          <input class="input mt-1" type="month" [(ngModel)]="filterPeriod" (ngModelChange)="reload()" />
        </label>
        <label class="text-sm">
          Département
          <select class="input mt-1 min-w-[200px]" [(ngModel)]="filterDeptId" (ngModelChange)="reload()">
            <option value="">Tous</option>
            @for (d of departments(); track d.id) {
              <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
            }
          </select>
        </label>
        <label class="text-sm">
          Statut
          <select class="input mt-1 min-w-[180px]" [(ngModel)]="filterStatus" (ngModelChange)="applyStatusFilter()">
            <option value="">Tous</option>
            @for (s of statusOptions; track s) {
              <option [value]="s">{{ statusLabel(s) }}</option>
            }
          </select>
        </label>
      </div>

      @if (loading()) {
        <p class="text-muted text-sm">Chargement…</p>
      } @else {
        <p class="text-sm text-muted">{{ filteredRows().length }} demande(s)</p>
        <app-allowance-request-table
          [rows]="filteredRows()"
          [employeeLabel]="employeeLabelFn"
          [departmentLabel]="deptLabelFn"
          [showDepartment]="true"
          [showDraftActions]="false"
        />
        @if (rhActionableCount() > 0 && isRh()) {
          <p class="text-sm">
            <button type="button" class="text-blue-400 underline" (click)="goInbox()">
              {{ rhActionableCount() }} demande(s) en attente dans votre file RH
            </button>
          </p>
        }
      }
    </div>
  `,
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
