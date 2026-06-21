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
import { AllowanceStatusBadgeComponent } from '../../components/allowances/allowance-status-badge.component';
import { AllowanceRejectDialogComponent } from '../../components/allowances/allowance-reject-dialog.component';
import { AllowanceRejectDialogService } from '../../components/allowances/allowance-reject-dialog.service';
import { AllowanceInboxBadgeService } from '../../services/allowance-inbox-badge.service';
import { PrimeNavRequestService } from '../../services/prime-nav-request.service';
import { redirectManagerFromAllowancesIfNeeded } from '../../lib/allowance-manager-guard';
import { allowanceApiErrorMessage } from '../../lib/allowance-api-error';
import { AllowancesPageShellComponent } from '../../components/allowances/allowances-page-shell.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import {
  canValidateAtStep,
  currentAllowancePeriod,
  inboxStepLabel,
  allowanceSourceLabel,
} from '../../lib/allowance-status';

@Component({
  selector: 'app-allowances-inbox-page',
  standalone: true,
  imports: [CommonModule, FormsModule, AllowanceStatusBadgeComponent, AllowanceRejectDialogComponent, AllowancesPageShellComponent, PrimeCardComponent],
  template: `
    <app-allowance-reject-dialog />
    <app-allowances-page-shell [title]="pageTitle()" [subtitle]="pageSubtitle()" [error]="error()">
      @if (showFilters()) {
        <app-prime-card title="Filtres" className="ky-card--compact">
          <div class="flex flex-wrap gap-4 items-end">
            <label class="text-sm text-primary">
              Période
              <input class="doc-field mt-1" type="month" [(ngModel)]="filterPeriod" (ngModelChange)="applyFilters()" />
            </label>
            <label class="text-sm text-primary">
              Département
              <select class="doc-field mt-1 min-w-[220px]" [(ngModel)]="filterDeptId" (ngModelChange)="applyFilters()">
                <option value="">Tous</option>
                @for (d of departments(); track d.id) {
                  <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
                }
              </select>
            </label>
          </div>
        </app-prime-card>
      }

      @if (loading()) {
        <div class="flex justify-center py-12">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-500"></div>
        </div>
      } @else if (filteredRows().length === 0) {
        <app-prime-card description="Aucune demande en attente pour votre file de validation." />
      } @else {
        <div class="space-y-4">
          @for (r of filteredRows(); track r.id) {
            <app-prime-card>
              <div class="flex flex-wrap gap-4 items-start justify-between">
                <div class="space-y-2 min-w-0 flex-1">
                  <p class="font-semibold text-primary">{{ r.typeLabel }} — {{ r.amount | number:'1.0-2' }} {{ r.currency }}</p>
                  <p class="text-sm text-muted">
                    {{ employeeLabel(r.employeeId) }} · {{ r.period }} · {{ deptLabel(r.businessDepartmentId) }}
                  </p>
                  <p class="text-xs text-muted">Source : {{ sourceLabel(r.source) }}</p>
                  <app-allowance-status-badge [status]="r.status" />
                  @if (r.reason) {
                    <p class="text-sm mt-1 text-primary">{{ r.reason }}</p>
                  }
                </div>
                @if (canActOn(r)) {
                  <div class="flex gap-2 shrink-0">
                    <button type="button" class="btn-primary text-sm" [disabled]="acting()" (click)="approve(r.id)">Valider</button>
                    <button type="button" class="btn-danger text-sm" [disabled]="acting()" (click)="reject(r.id)">Rejeter</button>
                  </div>
                }
              </div>
            </app-prime-card>
          }
        </div>
      }
    </app-allowances-page-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesInboxPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);
  private readonly role = inject(RoleService);
  private readonly rejectDialog = inject(AllowanceRejectDialogService);
  private readonly inboxBadge = inject(AllowanceInboxBadgeService);
  private readonly dept = inject(DepartmentContextService);
  private readonly nav = inject(PrimeNavRequestService);

  readonly loading = signal(true);
  readonly acting = signal(false);
  readonly error = signal('');
  readonly rows = signal<AllowanceRequestDto[]>([]);
  readonly departments = signal<BusinessDepartmentMirrorDto[]>([]);

  filterPeriod = currentAllowancePeriod();
  filterDeptId = '';

  readonly pageTitle = computed(() => inboxStepLabel(this.role.currentRole()));
  readonly pageSubtitle = computed(() => {
    const role = this.role.currentRole();
    if (role === 'RH') return 'Demandes soumises par les managers Support, en attente de votre validation.';
    if (role === 'Comptabilité' || role === 'Comptable') {
      return 'Demandes validées RH, en attente de validation comptabilité.';
    }
    return 'Demandes en attente de validation.';
  });

  readonly showFilters = computed(() => this.role.currentRole() === 'RH');

  readonly filteredRows = computed(() => {
    let list = this.rows();
    if (this.filterPeriod.trim()) {
      list = list.filter((r) => r.period === this.filterPeriod.trim());
    }
    if (this.filterDeptId.trim()) {
      list = list.filter((r) => r.businessDepartmentId === this.filterDeptId.trim());
    }
    return list;
  });

  ngOnInit(): void {
    void this.load();
  }

  constructor() {
    effect(() => {
      if (!this.dept.loaded()) return;
      if (redirectManagerFromAllowancesIfNeeded(this.role.currentRole(), this.dept, this.nav)) return;
      if (this.dept.isSupportManager()) {
        this.nav.requestView('/allowances/requests');
      }
    });
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

  sourceLabel(source: string): string {
    return allowanceSourceLabel(source);
  }

  canActOn(r: AllowanceRequestDto): boolean {
    return canValidateAtStep(this.role.currentRole(), r.status);
  }

  async approve(id: string): Promise<void> {
    this.acting.set(true);
    this.error.set('');
    try {
      await this.api.approve(id);
      await this.load();
    } catch (e: unknown) {
      this.error.set(allowanceApiErrorMessage(e, 'Erreur lors de la validation.'));
    } finally {
      this.acting.set(false);
    }
  }

  async reject(id: string): Promise<void> {
    const reason = await this.rejectDialog.open('Motif de rejet');
    if (!reason) return;
    this.acting.set(true);
    this.error.set('');
    try {
      await this.api.reject(id, reason);
      await this.load();
    } catch (e: unknown) {
      this.error.set(allowanceApiErrorMessage(e, 'Erreur lors du rejet.'));
    } finally {
      this.acting.set(false);
    }
  }

  applyFilters(): void {
    // computed filteredRows reacts automatically
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const tasks: Promise<void>[] = [
        this.api.inbox().then((rows) => this.rows.set(rows)),
      ];
      if (this.role.currentRole() === 'RH') {
        tasks.push(
          this.api.listBusinessDepartments().then((depts) => {
            this.departments.set(depts.filter((d) => d.kind === 'Support' && d.isActive));
          }),
        );
      }
      await Promise.all(tasks);
      await this.inboxBadge.refreshForRole(this.role.currentRole());
    } finally {
      this.loading.set(false);
    }
  }
}
