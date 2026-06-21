import { ChangeDetectionStrategy, Component, effect, inject, OnInit, signal } from '@angular/core';
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
import { redirectManagerFromAllowancesIfNeeded } from '../../lib/allowance-manager-guard';
import { AllowancesPageShellComponent } from '../../components/allowances/allowances-page-shell.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { allowanceApiErrorMessage } from '../../lib/allowance-api-error';
import { currentAllowancePeriod, validateAllowanceAmount } from '../../lib/allowance-status';

@Component({
  selector: 'app-allowances-requests-page',
  standalone: true,
  imports: [CommonModule, FormsModule, AllowanceRequestTableComponent, AllowancesPageShellComponent, PrimeCardComponent],
  template: `
    <app-allowances-page-shell
      title="Demandes de prime Support"
      subtitle="Définissez montant et motif pour votre équipe, puis soumettez au RH."
      [error]="loadError()"
    >
      <div pageActions>
        @if (isManager()) {
          <button type="button" class="btn-primary" (click)="openCreateForm()">
            Nouvelle demande
          </button>
        }
      </div>

      <app-prime-card title="Filtres" className="ky-card--compact">
        <label class="text-sm text-primary inline-flex items-center gap-2">
          Période
          <input class="doc-field" type="month" [(ngModel)]="filterPeriod" (ngModelChange)="reloadList()" />
        </label>
      </app-prime-card>

      @if (isManager()) {
        <app-prime-card title="Propositions automatiques">
          <p class="text-sm text-muted mb-3">
            Générez des brouillons à partir des règles actives, ajustez montant et motif, puis soumettez au RH.
          </p>
          <div class="flex flex-wrap gap-3 items-end">
            <label class="text-sm text-primary">
              Période
              <input class="doc-field mt-1" type="month" [(ngModel)]="proposalPeriod" />
            </label>
            <button type="button" class="prime-btn-secondary" [disabled]="generating()" (click)="generateProposals()">
              Générer les brouillons
            </button>
          </div>
          @if (proposalMessage()) {
            <div class="ky-alert text-sm mt-3" [class.ky-alert-success]="proposalSuccess()" [class.ky-alert-error]="!proposalSuccess()">
              {{ proposalMessage() }}
            </div>
          }
        </app-prime-card>
      }

      @if (showForm() && isManager()) {
        <app-prime-card [title]="editingId() ? 'Modifier le brouillon' : 'Nouvelle demande'">
          <form class="space-y-3 max-w-lg" (ngSubmit)="saveForm()">
            @if (!editingId()) {
              <label class="block text-sm text-primary">
                Collaborateur (N-1)
                <select class="doc-field w-full mt-1" [(ngModel)]="formEmployeeId" name="emp" required>
                  <option value="">— Sélectionner —</option>
                  @for (m of team(); track m.id) {
                    <option [value]="m.id">{{ memberLabel(m) }}</option>
                  }
                </select>
              </label>
            }
            <label class="block text-sm text-primary">
              Type de prime
              <select class="doc-field w-full mt-1" [(ngModel)]="formTypeId" name="type" required (ngModelChange)="onTypeChange()">
                <option value="">—</option>
                @for (t of types(); track t.id) {
                  <option [value]="t.id">{{ t.label }} ({{ t.code }})</option>
                }
              </select>
            </label>
            <label class="block text-sm text-primary">
              Période
              <input class="doc-field w-full mt-1" type="month" [(ngModel)]="formPeriod" name="period" required />
            </label>
            <label class="block text-sm text-primary">
              Montant (MAD)
              <input type="number" class="doc-field w-full mt-1" [(ngModel)]="formAmount" name="amount" required />
            </label>
            <label class="block text-sm text-primary">
              Motif
              <textarea class="doc-field w-full mt-1" rows="2" [(ngModel)]="formReason" name="reason"></textarea>
            </label>
            @if (error()) {
              <div class="ky-alert ky-alert-error text-sm">{{ error() }}</div>
            }
            <div class="flex gap-2">
              <button type="submit" class="btn-primary" [disabled]="saving()">
                {{ editingId() ? 'Enregistrer' : 'Créer brouillon' }}
              </button>
              <button type="button" class="prime-btn-secondary" (click)="closeForm()">Annuler</button>
            </div>
          </form>
        </app-prime-card>
      }

      @if (loading()) {
        <div class="flex justify-center py-12">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-500"></div>
        </div>
      } @else {
        <app-allowance-request-table
          [rows]="rows()"
          [employeeLabel]="employeeLabelFn"
          [showDraftActions]="isManager()"
          [statusViewer]="'manager'"
          (submitDraft)="submit($event)"
          (editDraft)="openEditForm($event)"
        />
      }
    </app-allowances-page-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesRequestsPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);
  private readonly dept = inject(DepartmentContextService);
  private readonly role = inject(RoleService);
  private readonly nav = inject(PrimeNavRequestService);

  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly rows = signal<AllowanceRequestDto[]>([]);
  readonly types = signal<AllowanceTypeDto[]>([]);
  readonly team = signal<AllowanceTeamMemberDto[]>([]);
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly generating = signal(false);
  readonly proposalMessage = signal('');
  readonly proposalSuccess = signal(false);

  filterPeriod = currentAllowancePeriod();
  proposalPeriod = currentAllowancePeriod();
  formEmployeeId = '';
  formTypeId = '';
  formPeriod = currentAllowancePeriod();
  formAmount = 0;
  formReason = '';

  readonly employeeLabelFn = (id: string) => this.employeeLabel(id);

  ngOnInit(): void {
    void this.load();
  }

  constructor() {
    effect(() => {
      if (!this.dept.loaded()) return;
      redirectManagerFromAllowancesIfNeeded(this.role.currentRole(), this.dept, this.nav);
    });
  }

  isManager(): boolean {
    return this.dept.isSupportManager();
  }

  memberLabel(m: AllowanceTeamMemberDto): string {
    return `${m.firstName} ${m.lastName}`.trim() || m.email || m.id;
  }

  employeeLabel(employeeId: string): string {
    const fromTeam = this.team().find((m) => m.id === employeeId);
    if (fromTeam) return this.memberLabel(fromTeam);
    const fromRole = this.role.employees().find((e) => e.id === employeeId);
    if (fromRole) return `${fromRole.firstName} ${fromRole.lastName}`.trim() || employeeId;
    return employeeId;
  }

  selectedType(): AllowanceTypeDto | undefined {
    return this.types().find((t) => t.id === this.formTypeId);
  }

  onTypeChange(): void {
    const t = this.selectedType();
    if (t?.defaultAmount != null && !this.editingId()) {
      this.formAmount = t.defaultAmount;
    }
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

  closeForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.error.set('');
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
      } else {
        await this.api.createRequest({
          employeeId: this.formEmployeeId.trim(),
          allowanceTypeId: this.formTypeId,
          period: this.formPeriod.trim(),
          amount: this.formAmount,
          reason: this.formReason,
        });
      }
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
      await this.reloadList();
    } catch (e: unknown) {
      this.loadError.set(allowanceApiErrorMessage(e, 'Erreur lors de la soumission au RH.'));
    }
  }

  async generateProposals(): Promise<void> {
    this.generating.set(true);
    this.proposalMessage.set('');
    try {
      const deptId = this.dept.context()?.managedDepartmentId;
      const result = await this.api.generateProposals(this.proposalPeriod.trim(), deptId);
      this.proposalSuccess.set(true);
      this.proposalMessage.set(`${result.created} brouillon(s) généré(s). Vérifiez montant et motif avant soumission.`);
      await this.reloadList();
    } catch (e: unknown) {
      this.proposalSuccess.set(false);
      this.proposalMessage.set(allowanceApiErrorMessage(e, 'Erreur lors de la génération.'));
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
