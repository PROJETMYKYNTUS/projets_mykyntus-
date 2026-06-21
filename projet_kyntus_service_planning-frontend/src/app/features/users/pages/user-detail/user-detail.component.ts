import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { UserService } from '../../services/user.service';
import { User } from '../../users-module';
import { EmployeeFieldService } from '../../services/employee-field.service';
import type { EmployeeImportFieldConfig } from '../../services/employee-import.service';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { ArrowLeft, Pencil, Trash2 } from 'lucide';
import type { Department } from '../../../prime/models';
import { PrimeOrgApiService } from '../../../prime/services/prime-org-api.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import {
  enrichUserOrgPerimeter,
  orgCellLabel,
  orgPerimeterSummary,
  type BusinessDepartmentRef,
  type DirectoryEmployeeOrgRef,
  type UserOrgPerimeterView,
} from '../../../../core/org/user-org-perimeter';

@Component({
  selector: 'app-user-detail',
  standalone: true,
  imports: [CommonModule, LucideIconComponent, KyntusPageHeaderComponent],
  templateUrl: './user-detail.component.html',
  styleUrls: ['./user-detail.component.css']
})
export class UserDetailComponent implements OnInit {
  readonly icons = { back: ArrowLeft, edit: Pencil, trash: Trash2 };
  readonly orgCellLabel = orgCellLabel;
  user: User | null = null;
  customFields: EmployeeImportFieldConfig[] = [];
  perimeter: UserOrgPerimeterView = { operationalDepartment: null, pole: null, cellule: null, service: null };
  loading = false;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private userService: UserService,
    private orgApi: PrimeOrgApiService,
    private subServiceService: SubServiceService,
    private fieldService: EmployeeFieldService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.fieldService.getFields(true).subscribe({
      next: (fields) => {
        this.customFields = fields.filter((f) => f.isSystemField === false);
        this.cdr.detectChanges();
      },
    });
    this.loadUser(id);
  }

  customFieldValue(fieldKey: string): string {
    return this.user?.customFields?.[fieldKey] ?? '—';
  }

  get perimeterSummary(): string {
    return orgPerimeterSummary(this.perimeter);
  }

  loadUser(id: number): void {
    this.loading = true;
    this.error = null;
    forkJoin({
      user: this.userService.getUserById(id),
      departments: this.http.get<Department[]>('/api/prime/departments'),
      overview: this.orgApi.loadOverview(),
      subServices: this.subServiceService.getAllSubServices(),
      directoryEmployees: this.http.get<DirectoryEmployeeOrgRef[]>('/api/directory/employees'),
      businessDepartments: this.http.get<BusinessDepartmentRef[]>('/api/directory/business-departments'),
    }).subscribe({
      next: ({ user, departments, overview, subServices, directoryEmployees, businessDepartments }) => {
        this.user = user;
        this.perimeter = enrichUserOrgPerimeter(
          user,
          departments ?? [],
          overview,
          subServices ?? [],
          directoryEmployees ?? [],
          businessDepartments ?? [],
        );
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.userService.getUserById(id).subscribe({
          next: (user) => {
            this.user = user;
            this.perimeter = enrichUserOrgPerimeter(user, [], null, []);
            this.loading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            this.error = 'Impossible de récupérer le profil.';
            this.loading = false;
            this.cdr.detectChanges();
          },
        });
      },
    });
  }

  getAnciennete(hireDate: string): string {
    const debut = new Date(hireDate);
    const now = new Date();
    const totalMois = (now.getFullYear() - debut.getFullYear()) * 12
                    + (now.getMonth() - debut.getMonth());
    const ans = Math.floor(totalMois / 12);
    const mois = totalMois % 12;
    if (totalMois <= 0) return "Moins d'1 mois";
    if (ans === 0) return `${mois} mois`;
    if (mois === 0) return `${ans} an${ans > 1 ? 's' : ''}`;
    return `${ans} an${ans > 1 ? 's' : ''} et ${mois} mois`;
  }

  levelLabel(level: number): string {
    if (level === 2) return 'Intermédiaire';
    if (level === 3) return 'Expert';
    return 'Débutant';
  }

  goBack(): void { this.router.navigate(['/users']); }
  editUser(): void { this.router.navigate(['/users', 'edit', this.user?.id]); }

  deleteUser(): void {
    if (!this.user) return;
    if (confirm('Supprimer cet employé ?')) {
      this.userService.deleteUser(this.user.id).subscribe({
        next: () => this.router.navigate(['/users']),
        error: (err) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}
