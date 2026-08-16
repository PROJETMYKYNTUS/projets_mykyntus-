import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { UserService } from '../../services/user.service';
import { User } from '../../users-module';
import { copyTextToClipboard } from '../../../../core/lib/clipboard.util';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { AlertTriangle, Award, Eye, History, KeyRound, Pencil, Search, Trash2 } from 'lucide';
import type { Department } from '../../../prime/models';
import type { OrgAssignmentsOverview } from '../../../prime/services/prime-org-api.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import { PilotRotationHistoryModalComponent } from '../../../prime/components/pilot-rotation-history-modal.component';
import { BodyPortalDirective } from '../../../../shared/directives/body-portal.directive';
import type { SubService } from '../../../sub-services/sub-services-module';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import {
  enrichUserOrgPerimeter,
  orgCellLabel,
  orgDepartmentLabel,
  type BusinessDepartmentRef,
  type DirectoryEmployeeOrgRef,
  type UserOrgPerimeterView,
} from '../../../../core/org/user-org-perimeter';
import {
  contractLevelLabel,
  expertiseLevelLabel,
  matriculeDisplay,
  formatSeniorityDuration,
  seniorityReferenceDate,
  telephoneDisplay,
  userMatchesSearch,
} from '../../../../core/hr/user-hr-display.util';
import { resolveUserGuid } from '../../../../core/lib/user-guid.util';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LucideIconComponent,
    KyntusPageHeaderComponent,
    PilotRotationHistoryModalComponent,
    BodyPortalDirective,
  ],
  templateUrl: './user-list.component.html',
  styleUrls: ['./user-list.component.css']
})
export class UserListComponent implements OnInit {
  readonly icons = {
    warn: AlertTriangle,
    eye: Eye,
    edit: Pencil,
    trash: Trash2,
    search: Search,
    history: History,
    level: Award,
    key: KeyRound,
  };
  readonly orgCellLabel = orgCellLabel;
  readonly orgDepartmentLabel = orgDepartmentLabel;
  readonly contractLevelLabel = contractLevelLabel;
  readonly expertiseLevelLabel = expertiseLevelLabel;
  readonly matriculeDisplay = matriculeDisplay;
  readonly telephoneDisplay = telephoneDisplay;
  users: User[] = [];
  filteredUsers: User[] = [];
  private perimeterByUserId = new Map<number, UserOrgPerimeterView>();
  private directoryEmployees: DirectoryEmployeeOrgRef[] = [];
  private businessDepartments: BusinessDepartmentRef[] = [];
  searchTerm = '';
  loading = false;
  error: string | null = null;

  rotationHistoryOpen = false;
  rotationHistoryEmployeeId = '';
  rotationHistoryEmployeeName = '';

  levelModalOpen = false;
  levelModalUser: User | null = null;
  levelDraft: 1 | 2 | 3 = 1;
  levelSaving = false;
  levelError = '';
  resetCredentials: { email: string; password: string; firstName?: string; lastName?: string } | null = null;
  showResetPassword = false;
  resettingUserId: number | null = null;
  downloadingCredentialsExcel = false;

  constructor(
    private userService: UserService,
    private subServiceService: SubServiceService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private router: Router,
    private confirmService: KyntusConfirmService,
    private toastService: KyntusToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadUsers();
  }

  userPerimeter(user: User): UserOrgPerimeterView {
    return this.perimeterByUserId.get(user.id) ?? enrichUserOrgPerimeter(user, [], null, []);
  }

  loadUsers(): void {
    this.loading = true;
    this.error = null;
    forkJoin({
      users: this.userService.getAllUsers(),
      overview: this.orgApi.loadOverview(),
      subServices: this.subServiceService.getAllSubServices(),
      directoryEmployees: this.http.get<DirectoryEmployeeOrgRef[]>('/api/directory/employees'),
      businessDepartments: this.http.get<BusinessDepartmentRef[]>('/api/directory/business-departments'),
    }).subscribe({
      next: ({ users, overview, subServices, directoryEmployees, businessDepartments }) => {
        this.users = users;
        this.filteredUsers = users;
        this.directoryEmployees = directoryEmployees ?? [];
        this.businessDepartments = businessDepartments ?? [];
        const departments = overview?.departments?.length ? overview.departments : [];
        this.rebuildPerimeters(users, departments, overview, subServices ?? []);
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.userService.getAllUsers().subscribe({
          next: (users) => {
            this.users = users;
            this.filteredUsers = users;
            this.rebuildPerimeters(users, [], null, []);
            this.loading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            this.error = `Erreur: ${err.status}`;
            this.loading = false;
            this.cdr.detectChanges();
          },
        });
      }
    });
  }

  private rebuildPerimeters(
    users: User[],
    departments: Department[],
    overview: OrgAssignmentsOverview | null,
    subServices: SubService[],
  ): void {
    this.perimeterByUserId.clear();
    for (const user of users) {
      this.perimeterByUserId.set(
        user.id,
        enrichUserOrgPerimeter(
          user,
          departments,
          overview,
          subServices,
          this.directoryEmployees,
          this.businessDepartments,
        ),
      );
    }
  }

  onSearch(): void {
    const term = this.searchTerm.toLowerCase().trim();
    this.filteredUsers = term
      ? this.users.filter((u) => userMatchesSearch(u, term))
      : this.users;
  }

  getAnciennete(user: User): string {
    return formatSeniorityDuration(seniorityReferenceDate(user));
  }

  goimport(): void { this.router.navigate(['/import']); }
  viewUser(id: number): void { this.router.navigate(['/users', id]); }
  editUser(id: number): void { this.router.navigate(['/users', 'edit', id]); }
  createUser(): void { this.router.navigate(['/users/create']); }

  openPilotRotationHistory(user: User, event?: Event): void {
    event?.stopPropagation();
    const guid = resolveUserGuid(user);
    if (!guid) {
      this.toastService.error('Identifiant employé (GUID) introuvable — historique indisponible.');
      return;
    }
    this.rotationHistoryEmployeeId = guid;
    this.rotationHistoryEmployeeName = `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim();
    this.rotationHistoryOpen = true;
  }

  closePilotRotationHistory(): void {
    this.rotationHistoryOpen = false;
    this.rotationHistoryEmployeeId = '';
    this.rotationHistoryEmployeeName = '';
  }

  openLevelModal(user: User, event?: Event): void {
    event?.stopPropagation();
    this.levelModalUser = user;
    this.levelDraft = (user.level === 2 || user.level === 3 ? user.level : 1) as 1 | 2 | 3;
    this.levelError = '';
    this.levelModalOpen = true;
  }

  closeLevelModal(): void {
    this.levelModalOpen = false;
    this.levelModalUser = null;
    this.levelError = '';
  }

  saveContractLevel(): void {
    if (!this.levelModalUser) return;
    this.levelSaving = true;
    this.levelError = '';
    this.userService.patchContractualLevel(this.levelModalUser.id, this.levelDraft).subscribe({
      next: (updated) => {
        this.users = this.users.map((u) =>
          u.id === updated.id ? { ...u, level: updated.level } : u,
        );
        this.onSearch();
        this.levelSaving = false;
        this.closeLevelModal();
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.levelSaving = false;
        this.levelError = err?.error?.message ?? 'Échec de la mise à jour du niveau.';
        this.cdr.detectChanges();
      },
    });
  }

  async deleteUser(id: number): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Supprimer l\'employé',
      message: 'Supprimer cet employé ?',
      confirmLabel: 'Supprimer',
      variant: 'danger',
    });
    if (!ok) return;
    this.userService.deleteUser(id).subscribe({
      next: () => this.loadUsers(),
      error: (err) => this.toastService.error(`Erreur: ${err.error?.message}`),
    });
  }

  async resetPassword(user: User, event?: Event): Promise<void> {
    event?.stopPropagation();
    if (this.resettingUserId != null) return;
    const ok = await this.confirmService.confirm({
      title: 'Réinitialiser le mot de passe',
      message: `Réinitialiser le mot de passe de ${user.email} ?`,
      confirmLabel: 'Réinitialiser',
    });
    if (!ok) return;
    this.resettingUserId = user.id;
    this.userService.resetPassword(user.id).subscribe({
      next: (result) => {
        this.resetCredentials = {
          email: result.email,
          password: result.temporaryPassword,
          firstName: user.firstName,
          lastName: user.lastName,
        };
        this.showResetPassword = true;
        this.resettingUserId = null;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.resettingUserId = null;
        this.toastService.error(err?.error?.message ?? 'Échec de la réinitialisation.');
        this.cdr.detectChanges();
      },
    });
  }

  closeResetCredentials(): void {
    this.resetCredentials = null;
    this.showResetPassword = false;
  }

  async copyResetPassword(): Promise<void> {
    if (!this.resetCredentials?.password) return;
    try {
      await copyTextToClipboard(this.resetCredentials.password);
    } catch (err) {
      console.error('copyResetPassword', err);
      this.toastService.error('Impossible de copier le mot de passe.');
    }
  }

  async downloadResetCredentialsExcel(): Promise<void> {
    if (!this.resetCredentials || this.downloadingCredentialsExcel) return;
    this.downloadingCredentialsExcel = true;
    try {
      const { downloadCredentialsExcel } = await import('../../../../core/lib/credentials-excel.util');
      await downloadCredentialsExcel(
        [
          {
            email: this.resetCredentials.email,
            password: this.resetCredentials.password,
            firstName: this.resetCredentials.firstName,
            lastName: this.resetCredentials.lastName,
          },
        ],
        { fileNamePrefix: 'identifiants-mykyntus-reinit' },
      );
    } catch {
      this.toastService.error('Échec du téléchargement Excel.');
    } finally {
      this.downloadingCredentialsExcel = false;
      this.cdr.detectChanges();
    }
  }

  lifecycleBadge(user: User): { phase: string; label: string } | null {
    const ls = user.lifecycleStatus;
    if (ls && ls.phase && ls.phase !== 'active') {
      return { phase: ls.phase, label: ls.label };
    }
    if (user.hrProfile?.enFormation) {
      return { phase: 'onboarding_formation', label: 'En formation' };
    }
    return null;
  }
}
