import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { UserService } from '../../services/user.service';
import { User } from '../../users-module';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { AlertTriangle, Eye, History, Pencil, Search, Trash2 } from 'lucide';
import type { Department } from '../../../prime/models';
import type { OrgAssignmentsOverview } from '../../../prime/services/prime-org-api.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import { PilotRotationHistoryModalComponent } from '../../../prime/components/pilot-rotation-history-modal.component';
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

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LucideIconComponent,
    KyntusPageHeaderComponent,
    PilotRotationHistoryModalComponent,
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

  constructor(
    private userService: UserService,
    private subServiceService: SubServiceService,
    private orgApi: PrimeOrgApiService,
    private http: HttpClient,
    private router: Router,
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
        const departments = overview?.departments?.length
          ? overview.departments
          : [];
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
      alert('Identifiant employé (GUID) introuvable — historique indisponible.');
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

  deleteUser(id: number): void {
    if (confirm('Supprimer cet employé ?')) {
      this.userService.deleteUser(id).subscribe({
        next: () => this.loadUsers(),
        error: (err) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}
