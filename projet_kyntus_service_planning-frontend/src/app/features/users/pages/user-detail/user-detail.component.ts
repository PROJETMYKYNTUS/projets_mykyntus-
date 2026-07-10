import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { forkJoin, of, firstValueFrom } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { UserService } from '../../services/user.service';
import { User } from '../../users-module';
import { EmployeeFieldService } from '../../services/employee-field.service';
import type { EmployeeImportFieldConfig } from '../../services/employee-import.service';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { ArrowLeft, Pencil, Trash2 } from 'lucide';
import type { Department } from '../../../prime/models';
import { PrimeOrgApiService, type OrgAssignmentsOverview } from '../../../prime/services/prime-org-api.service';
import type { SubService } from '../../../sub-services/sub-services-module';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { ParrainageApiService } from '../../../parrainage/services/parrainage-api.service';
import type { Referral } from '../../../parrainage/models/referral.model';
import { ContractService, type ContractResponse } from '../../../contract/services/contract.service';
import { resolveUserGuid } from '../../../../core/lib/user-guid.util';
import {
  enrichUserOrgPerimeter,
  orgCellLabel,
  orgDepartmentLabel,
  orgPerimeterSummary,
  type BusinessDepartmentRef,
  type DirectoryEmployeeOrgRef,
  type UserOrgPerimeterView,
} from '../../../../core/org/user-org-perimeter';
import {
  buildContractDisplayRows,
  buildEmployeeDetailSections,
  contractLevelLabel,
  expertiseLevelLabel,
  seniorityReferenceDate,
  type EmployeeDetailSection,
} from '../../../../core/hr/user-hr-display.util';
import {
  DirectoryEmployeeApiService,
  type PilotRotationHistoryEntryDto,
} from '../../../../core/directory/directory-employee-api.service';

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
  readonly orgDepartmentLabel = orgDepartmentLabel;
  readonly contractLevelLabel = contractLevelLabel;
  readonly expertiseLevelLabel = expertiseLevelLabel;
  user: User | null = null;
  detailSections: EmployeeDetailSection[] = [];
  linkedReferral: Referral | null = null;
  perimeter: UserOrgPerimeterView = { operationalDepartment: null, pole: null, cellule: null, service: null };
  pilotRotationHistory: PilotRotationHistoryEntryDto[] = [];
  pilotRotationLoading = false;
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
    private contractService: ContractService,
    private parrainageApi: ParrainageApiService,
    private directoryEmployeeApi: DirectoryEmployeeApiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadUser(id);
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
      customFields: this.fieldService.getFields(true),
      contracts: this.contractService.getByUser(id).pipe(catchError(() => of([] as ContractResponse[]))),
    }).subscribe({
      next: ({ user, departments, overview, subServices, directoryEmployees, businessDepartments, customFields, contracts }) => {
        this.applyUserData(
          user,
          departments ?? [],
          overview,
          subServices ?? [],
          directoryEmployees ?? [],
          businessDepartments ?? [],
          customFields.filter((f) => f.isSystemField === false),
          contracts ?? [],
        );
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.userService.getUserById(id).subscribe({
          next: (user) => {
            this.applyUserData(user, [], null, [], [], [], [], []);
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

  private applyUserData(
    user: User,
    departments: Department[],
    overview: OrgAssignmentsOverview | null,
    subServices: SubService[],
    directoryEmployees: DirectoryEmployeeOrgRef[],
    businessDepartments: BusinessDepartmentRef[],
    customFields: EmployeeImportFieldConfig[],
    contracts: ContractResponse[],
  ): void {
    this.user = user;
    this.perimeter = enrichUserOrgPerimeter(
      user,
      departments,
      overview,
      subServices,
      directoryEmployees,
      businessDepartments,
    );

    const mentorEmployees = (overview?.employees ?? []).map((e) => ({
      id: e.id,
      firstName: e.firstName,
      lastName: e.lastName,
    }));

    const latestContract = contracts.length
      ? [...contracts].sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime())[0]
      : null;

    const contractRows = latestContract
      ? [
          { label: 'Référence contrat', value: String(latestContract.id) },
          ...buildContractDisplayRows(latestContract),
        ]
      : [];

    const customFieldRows = customFields.map((field) => ({
      label: field.label,
      value: user.customFields?.[field.fieldKey]?.trim() ? user.customFields[field.fieldKey]!.trim() : '—',
    }));

    this.detailSections = buildEmployeeDetailSections(user, {
      mentorEmployees,
      contractRows,
      customFieldRows,
    });

    void this.loadLinkedReferral(user);
    void this.loadPilotRotationHistory(user);
  }

  get showPilotRotationHistory(): boolean {
    if (!this.user) return false;
    if (this.pilotRotationHistory.length > 0) return true;
    return (this.user.roleName ?? '').toLowerCase() === 'pilote';
  }

  formatRotationDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('fr-FR');
  }

  formatRotationDuration(days?: number | null): string {
    if (days == null) return '—';
    if (days < 30) return `${days} jour${days > 1 ? 's' : ''}`;
    const months = Math.floor(days / 30);
    const rem = days % 30;
    if (rem === 0) return `${months} mois`;
    return `${months} mois ${rem} j`;
  }

  private async loadPilotRotationHistory(user: User): Promise<void> {
    const guid = resolveUserGuid(user);
    if (!guid) return;

    this.pilotRotationLoading = true;
    try {
      this.pilotRotationHistory = await firstValueFrom(
        this.directoryEmployeeApi.getPilotRotationHistory(guid).pipe(catchError(() => of([]))),
      );
    } finally {
      this.pilotRotationLoading = false;
      this.cdr.detectChanges();
    }
  }

  private async loadLinkedReferral(user: User): Promise<void> {
    const guid = resolveUserGuid(user);
    if (!guid) return;
    try {
      const referrals = await this.parrainageApi.getReferrals();
      this.linkedReferral = referrals.find((r) => r.candidateEmployeeId === guid) ?? null;
      this.cdr.detectChanges();
    } catch {
      this.linkedReferral = null;
    }
  }

  getAnciennete(): string {
    if (!this.user) return '—';
    const ref = seniorityReferenceDate(this.user);
    const debut = new Date(ref);
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
