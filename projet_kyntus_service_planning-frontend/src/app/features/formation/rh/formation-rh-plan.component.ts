import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Search } from 'lucide';
import { FormationTrainingService } from '../../../core/services/formation-training.service';
import type { TrainingSessionDto } from '../../../core/models/formation-training.models';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { UserService } from '../../users/services/user.service';
import type { User } from '../../users/users-module';
import {
  buildEmployeePickerRows,
  filterEmployeePickerRows,
  type EmployeePickerRow,
} from '../../contract/lib/contract-employee-filter';
import { resolveUserGuid } from '../../../core/lib/user-guid.util';

@Component({
  selector: 'app-formation-rh-plan',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent, LucideIconComponent],
  templateUrl: './formation-rh-plan.component.html',
  styleUrls: ['./formation-rh-plan.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormationRhPlanComponent implements OnInit {
  private readonly api = inject(FormationTrainingService);
  private readonly usersApi = inject(UserService);

  readonly icons = { search: Search };
  readonly sessions = signal<TrainingSessionDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly assignSessionId = signal<string | null>(null);
  readonly assignMsg = signal<string | null>(null);

  private employeeRows: EmployeePickerRow[] = [];

  animatorSearch = '';
  beneficiarySearch = '';
  assignSearch = '';

  readonly selectedAnimator = signal<EmployeePickerRow | null>(null);
  readonly animatorSessions = signal<TrainingSessionDto[]>([]);
  readonly animatorSessionsLoading = signal(false);

  readonly selectedBeneficiaries = signal<EmployeePickerRow[]>([]);
  readonly assignSelected = signal<EmployeePickerRow[]>([]);

  /** Bump to refresh search computeds (OnPush + plain search fields). */
  private readonly searchTick = signal(0);

  readonly visibleAnimatorRows = computed(() => {
    this.searchTick();
    const selectedGuid = resolveUserGuid(this.selectedAnimator()?.user);
    const { visible } = filterEmployeePickerRows(
      this.employeeRows.filter((r) => {
        const g = resolveUserGuid(r.user);
        return !!g && g !== selectedGuid;
      }),
      { search: this.animatorSearch },
      25,
    );
    return visible;
  });

  readonly visibleBeneficiaryRows = computed(() => {
    this.searchTick();
    const selected = new Set(this.selectedBeneficiaries().map((r) => resolveUserGuid(r.user)));
    const { visible } = filterEmployeePickerRows(
      this.employeeRows.filter((r) => {
        const g = resolveUserGuid(r.user);
        return !!g && !selected.has(g);
      }),
      { search: this.beneficiarySearch },
      25,
    );
    return visible;
  });

  readonly visibleAssignRows = computed(() => {
    this.searchTick();
    const selected = new Set(this.assignSelected().map((r) => resolveUserGuid(r.user)));
    const { visible } = filterEmployeePickerRows(
      this.employeeRows.filter((r) => {
        const g = resolveUserGuid(r.user);
        return !!g && !selected.has(g);
      }),
      { search: this.assignSearch },
      25,
    );
    return visible;
  });

  form = {
    title: '',
    description: '',
    capacity: 10,
    plannedStart: '',
    plannedEnd: '',
    animatorKind: 'Internal' as 'Internal' | 'External',
    animatorUserId: '',
    externalAnimatorName: '',
    externalAnimatorOrganization: '',
    externalAnimatorEmail: '',
    externalAnimatorPhone: '',
  };

  ngOnInit(): void {
    void this.reload();
    void this.loadEmployees();
  }

  onAnimatorSearchChange(value: string): void {
    this.animatorSearch = value;
    this.searchTick.update((n) => n + 1);
  }

  onBeneficiarySearchChange(value: string): void {
    this.beneficiarySearch = value;
    this.searchTick.update((n) => n + 1);
  }

  onAssignSearchChange(value: string): void {
    this.assignSearch = value;
    this.searchTick.update((n) => n + 1);
  }

  /** Expose tick so template can depend on it for search reactivity. */
  searchRevision(): number {
    return this.searchTick();
  }

  selectAnimator(row: EmployeePickerRow): void {
    const guid = resolveUserGuid(row.user);
    if (!guid) {
      this.error.set('GUID employé introuvable pour cet animateur.');
      return;
    }
    this.selectedAnimator.set(row);
    this.form.animatorUserId = guid;
    this.animatorSearch = '';
    this.searchTick.update((n) => n + 1);
    void this.loadAnimatorSessions(guid);
  }

  clearAnimator(): void {
    this.selectedAnimator.set(null);
    this.form.animatorUserId = '';
    this.animatorSessions.set([]);
  }

  onAnimatorKindChange(kind: 'Internal' | 'External'): void {
    this.form.animatorKind = kind;
    if (kind === 'External') {
      this.clearAnimator();
    } else {
      this.form.externalAnimatorName = '';
      this.form.externalAnimatorOrganization = '';
      this.form.externalAnimatorEmail = '';
      this.form.externalAnimatorPhone = '';
    }
  }

  addBeneficiary(row: EmployeePickerRow): void {
    if (this.selectedBeneficiaries().length >= this.form.capacity) {
      this.error.set(`Capacité maximale atteinte (${this.form.capacity}).`);
      return;
    }
    this.selectedBeneficiaries.update((list) => [...list, row]);
    this.beneficiarySearch = '';
    this.searchTick.update((n) => n + 1);
    this.error.set(null);
  }

  removeBeneficiary(guid: string): void {
    this.selectedBeneficiaries.update((list) =>
      list.filter((r) => resolveUserGuid(r.user) !== guid),
    );
  }

  addAssignEmployee(row: EmployeePickerRow): void {
    this.assignSelected.update((list) => [...list, row]);
    this.assignSearch = '';
    this.searchTick.update((n) => n + 1);
  }

  removeAssignEmployee(guid: string): void {
    this.assignSelected.update((list) =>
      list.filter((r) => resolveUserGuid(r.user) !== guid),
    );
  }

  private async loadAnimatorSessions(guid: string): Promise<void> {
    this.animatorSessionsLoading.set(true);
    try {
      this.animatorSessions.set(await this.api.listMyAnimatedSessions(guid));
    } catch {
      this.animatorSessions.set([]);
    } finally {
      this.animatorSessionsLoading.set(false);
    }
  }

  private async reload(): Promise<void> {
    try {
      this.sessions.set(await this.api.listSessions());
    } catch {
      this.sessions.set([]);
    }
  }

  private async loadEmployees(): Promise<void> {
    try {
      const rows = await firstValueFrom(this.usersApi.getAllUsers());
      const active = (rows ?? []).filter((u) => u.isActive && !!resolveUserGuid(u));
      this.employeeRows = buildEmployeePickerRows(active, new Map());
      this.searchTick.update((n) => n + 1);
    } catch {
      this.employeeRows = [];
    }
  }

  openAssign(sessionId: string): void {
    this.assignSessionId.set(sessionId);
    this.assignSelected.set([]);
    this.assignSearch = '';
    this.assignMsg.set(null);
    this.searchTick.update((n) => n + 1);
  }

  async confirmAssign(): Promise<void> {
    const sessionId = this.assignSessionId();
    if (!sessionId) return;
    const selected = this.assignSelected();
    if (selected.length === 0) {
      this.assignMsg.set('Sélectionnez au moins un bénéficiaire.');
      return;
    }
    this.busy.set(true);
    this.assignMsg.set(null);
    try {
      await this.api.assignEmployees(
        sessionId,
        selected.map((r) => ({
          employeeId: resolveUserGuid(r.user),
          employeeName: r.displayName,
        })),
      );
      this.assignMsg.set(`${selected.length} bénéficiaire(s) affecté(s).`);
      this.assignSessionId.set(null);
      await this.reload();
    } catch (e) {
      this.assignMsg.set(e instanceof Error ? e.message : 'Échec de l’affectation');
    } finally {
      this.busy.set(false);
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Draft':
        return 'Brouillon';
      case 'Scheduled':
        return 'Planifiée';
      case 'InProgress':
        return 'En cours';
      case 'Completed':
        return 'Terminée';
      case 'Cancelled':
        return 'Annulée';
      default:
        return status;
    }
  }

  async publish(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      if (!this.form.title.trim()) {
        throw new Error('L’intitulé est obligatoire.');
      }
      if (!this.form.plannedStart || !this.form.plannedEnd) {
        throw new Error('Les dates de début et de fin sont obligatoires.');
      }
      if (this.form.animatorKind === 'Internal' && !this.form.animatorUserId) {
        throw new Error('Sélectionnez un animateur interne.');
      }
      if (this.form.animatorKind === 'External') {
        if (!this.form.externalAnimatorName.trim() || !this.form.externalAnimatorEmail.trim()) {
          throw new Error('Nom et email de l’animateur externe sont obligatoires.');
        }
      }
      const beneficiaries = this.selectedBeneficiaries();
      if (beneficiaries.length > this.form.capacity) {
        throw new Error(`Trop de bénéficiaires pour la capacité (${this.form.capacity}).`);
      }

      const created = await this.api.createSession({
        title: this.form.title,
        description: this.form.description,
        capacity: this.form.capacity,
        plannedStart: toIsoDateTime(this.form.plannedStart),
        plannedEnd: toIsoDateTime(this.form.plannedEnd),
        animatorKind: this.form.animatorKind === 'Internal' ? 0 : 1,
        animatorUserId: this.form.animatorKind === 'Internal' ? this.form.animatorUserId : null,
        externalAnimatorName: this.form.externalAnimatorName,
        externalAnimatorOrganization: this.form.externalAnimatorOrganization,
        externalAnimatorEmail: this.form.externalAnimatorEmail,
        externalAnimatorPhone: this.form.externalAnimatorPhone,
        createdByUserId: 'rh-ui',
        publish: true,
      });

      if (beneficiaries.length > 0 && created?.id) {
        await this.api.assignEmployees(
          created.id,
          beneficiaries.map((r) => ({
            employeeId: resolveUserGuid(r.user),
            employeeName: r.displayName,
          })),
        );
      }

      this.resetForm();
      await this.reload();
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Échec de la création');
    } finally {
      this.busy.set(false);
    }
  }

  private resetForm(): void {
    this.form = {
      title: '',
      description: '',
      capacity: 10,
      plannedStart: '',
      plannedEnd: '',
      animatorKind: 'Internal',
      animatorUserId: '',
      externalAnimatorName: '',
      externalAnimatorOrganization: '',
      externalAnimatorEmail: '',
      externalAnimatorPhone: '',
    };
    this.clearAnimator();
    this.selectedBeneficiaries.set([]);
    this.beneficiarySearch = '';
    this.searchTick.update((n) => n + 1);
  }

  fillRate(s: TrainingSessionDto): number {
    return s.capacity > 0 ? Math.round((s.assignmentCount / s.capacity) * 100) : 0;
  }

  formatSessionDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
  }

  userGuid(user: User): string {
    return resolveUserGuid(user);
  }
}

/** datetime-local → ISO UTC pour Npgsql (timestamp with time zone). */
function toIsoDateTime(localValue: string): string {
  const raw = localValue?.trim();
  if (!raw) return raw;
  const d = new Date(raw.length === 16 ? `${raw}:00` : raw);
  if (Number.isNaN(d.getTime())) return raw;
  return d.toISOString();
}
