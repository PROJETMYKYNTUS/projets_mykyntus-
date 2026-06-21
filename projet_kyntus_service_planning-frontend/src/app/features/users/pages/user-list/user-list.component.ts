import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { UserService } from '../../services/user.service';
import { User } from '../../users-module';
import { EmployeeFieldService } from '../../services/employee-field.service';
import type { EmployeeImportFieldConfig } from '../../services/employee-import.service';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { AlertTriangle, Eye, Pencil, Search, Trash2 } from 'lucide';
import type { Department } from '../../../prime/models';
import type { OrgAssignmentsOverview } from '../../../prime/services/prime-org-api.service';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import type { SubService } from '../../../sub-services/sub-services-module';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import {
  enrichUserOrgPerimeter,
  orgCellLabel,
  type BusinessDepartmentRef,
  type DirectoryEmployeeOrgRef,
  type UserOrgPerimeterView,
} from '../../../../core/org/user-org-perimeter';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './user-list.component.html',
  styleUrls: ['./user-list.component.css']
})
export class UserListComponent implements OnInit {
  readonly icons = { warn: AlertTriangle, eye: Eye, edit: Pencil, trash: Trash2, search: Search };
  readonly orgCellLabel = orgCellLabel;
  users: User[] = [];
  filteredUsers: User[] = [];
  customFieldColumns: EmployeeImportFieldConfig[] = [];
  private perimeterByUserId = new Map<number, UserOrgPerimeterView>();
  private directoryEmployees: DirectoryEmployeeOrgRef[] = [];
  private businessDepartments: BusinessDepartmentRef[] = [];
  searchTerm = '';
  loading = false;
  error: string | null = null;

  constructor(
    private userService: UserService,
    private subServiceService: SubServiceService,
    private orgApi: PrimeOrgApiService,
    private fieldService: EmployeeFieldService,
    private http: HttpClient,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.fieldService.getFields(true).subscribe({
      next: (fields) => {
        this.customFieldColumns = fields.filter((f) => f.isSystemField === false);
        this.cdr.detectChanges();
      },
    });
    this.loadUsers();
  }

  customFieldValue(user: User, fieldKey: string): string {
    return user.customFields?.[fieldKey] ?? '—';
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
      ? this.users.filter(u =>
          `${u.firstName} ${u.lastName}`.toLowerCase().includes(term) ||
          `${u.lastName} ${u.firstName}`.toLowerCase().includes(term) ||
          u.email.toLowerCase().includes(term)
        )
      : this.users;
  }

  getAnciennete(hireDate: string): string {
    const debut = new Date(hireDate);
    const now = new Date();
    let ans = now.getFullYear() - debut.getFullYear();
    let mois = now.getMonth() - debut.getMonth();
    let jours = now.getDate() - debut.getDate();
    if (jours < 0) {
      mois--;
      const dernierMois = new Date(now.getFullYear(), now.getMonth(), 0);
      jours += dernierMois.getDate();
    }
    if (mois < 0) {
      ans--;
      mois += 12;
    }
    const totalJours = Math.floor((now.getTime() - debut.getTime()) / (1000 * 60 * 60 * 24));
    if (totalJours <= 0) return "Pas encore embauché";
    if (totalJours < 30) return `${totalJours} jour${totalJours > 1 ? 's' : ''}`;
    const partAns = ans > 0 ? `${ans} an${ans > 1 ? 's' : ''}` : '';
    const partMois = mois > 0 ? `${mois} mois` : '';
    const partJour = jours > 0 ? `${jours} jour${jours > 1 ? 's' : ''}` : '';
    return [partAns, partMois, partJour].filter(Boolean).join(' et ');
  }

  goimport(): void { this.router.navigate(['/import']); }
  viewUser(id: number): void { this.router.navigate(['/users', id]); }
  editUser(id: number): void { this.router.navigate(['/users', 'edit', id]); }
  createUser(): void { this.router.navigate(['/users/create']); }

  deleteUser(id: number): void {
    if (confirm('Supprimer cet employé ?')) {
      this.userService.deleteUser(id).subscribe({
        next: () => this.loadUsers(),
        error: (err) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}
