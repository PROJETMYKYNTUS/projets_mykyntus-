import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { UserService } from '../../services/user.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
import { ServiceService } from '../../../services/services/service';   // 🆕
import { ServiceDetail } from '../../../services/services-module';         
import { SubService } from '../../../sub-services/sub-services-module';
import { CreateUserDto, UpdateUserDto } from '../../users-module';

interface RoleOption { id: number; name: string; }

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css']
})
export class UserFormComponent implements OnInit {
  isEditMode = false;
  userId: number | null = null;
  subServices: SubService[] = [];
  loading = false;
  submitting = false;
  error: string | null = null;
  emailError: string | null = null;

roles: RoleOption[] = [];
  servicesTree: ServiceDetail[] = [];
  readonly managerRoleId = 3;
  readonly coachRoleId = 4;

  form = {
    roleId: 0,
    subServiceId: null as number | null,
    managedSubServiceIds: [] as number[],
    managedServiceIds:      [] as number[],  
    firstName: '',
    lastName: '',
    email: '',
    hireDate: this.toDateInputValue(new Date()),  // ✅ format yyyy-MM-dd
    isActive: true ,
     level:                1
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private userService: UserService,
    private subServiceService: SubServiceService,
        private serviceService: ServiceService,
    private cdr: ChangeDetectorRef
  ) {}

ngOnInit(): void {
    this.loadSubServices();
    this.loadServicesTree(); 
    this.loadRoles(); // 🆕

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
        this.isEditMode = true;
        this.userId = Number(id);
        this.loadUser(this.userId);
    }
}

// 🆕 Charger les rôles depuis le backend
loadRoles(): void {
    this.userService.getRoles().subscribe({
        next: (roles) => {
            this.roles = roles;
            this.cdr.detectChanges();
        },
        error: () => {
            // Fallback si API échoue
            this.roles = [
                { id: 1, name: 'Employee' },
                { id: 2, name: 'RH' },
                { id: 3, name: 'Manager' },
                { id: 4, name: 'Coach' },
                { id: 5, name: 'RP' },
                { id: 6, name: 'Admin' },
                { id: 7, name: 'Audit' },
                { id: 8, name: 'Equipe formation' }
            ];
            this.cdr.detectChanges();
        }
    });
}
loadServicesTree(): void {
    this.serviceService.getAllServicesWithSubServices().subscribe({
      next: (data) => { this.servicesTree = data; this.cdr.detectChanges(); },
      error: () => { this.error = 'Impossible de charger les services.'; }
    });
  }


readonly employeeRoleId = 1; // ← seul rôle sans hiérarchie

get canManageHierarchy(): boolean {
  return this.form.roleId !== 0 &&                    // un rôle sélectionné
         this.form.roleId !== this.employeeRoleId;    // pas simple Employee
}

  // ✅ Convertit une Date en "yyyy-MM-dd" pour input type="date"
  private toDateInputValue(date: Date): string {
    return date.toLocaleDateString('en-CA'); // retourne "yyyy-MM-dd"
  }

  // ✅ Convertit "yyyy-MM-dd" en ISO string pour le backend
  private toISOString(dateStr: string): string {
    if (!dateStr) return new Date().toISOString();
    const [year, month, day] = dateStr.split('-').map(Number);
    return new Date(Date.UTC(year, month - 1, day, 12, 0, 0)).toISOString();
  }

  loadSubServices(): void {
    this.subServiceService.getAllSubServices().subscribe({
      next: (subs) => { this.subServices = subs; this.cdr.detectChanges(); },
      error: () => { this.error = 'Impossible de charger les sous-services.'; }
    });
  }

  loadUser(id: number): void {
    this.loading = true;
    this.userService.getUserById(id).subscribe({
      next: (user) => {
      console.log('✅ User complet:', JSON.stringify(user)); // 🆕
      this.form = {
          roleId: user.roleId,
          subServiceId: user.subServiceId ?? null,
          managedSubServiceIds: user.managedSubServices?.map(s => s.id) ?? [],
          firstName: user.firstName,
          lastName: user.lastName,
          email: user.email,
           managedServiceIds:    user.managedServices?.map(s => s.id) ?? [], 
          // ✅ Convertir DateTime backend → "yyyy-MM-dd" pour l'input
          hireDate: user.hireDate
            ? this.toDateInputValue(new Date(user.hireDate))
            : this.toDateInputValue(new Date()),
          isActive: user.isActive,
           level:                user.level ?? 1 
        };
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = `Erreur: ${err.status}`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
// 🔍 Vérifier que ces 4 méthodes sont bien présentes dans le .ts

isManagedService(id: number): boolean {
  return this.form.managedServiceIds.includes(id);
}

toggleManagedService(id: number): void {
  const idx = this.form.managedServiceIds.indexOf(id);
  if (idx === -1) {
    this.form.managedServiceIds = [...this.form.managedServiceIds, id]; // 🆕 nouveau tableau
  } else {
    this.form.managedServiceIds = this.form.managedServiceIds.filter(x => x !== id); // 🆕 nouveau tableau
  }
  this.cdr.detectChanges(); // 🆕
}

isManagedSubService(id: number): boolean {
  return this.form.managedSubServiceIds.includes(id);
}
toggleManagedSubService(id: number): void {
  const idx = this.form.managedSubServiceIds.indexOf(id);
  if (idx === -1) {
    this.form.managedSubServiceIds = [...this.form.managedSubServiceIds, id];
  } else {
    this.form.managedSubServiceIds = this.form.managedSubServiceIds.filter(x => x !== id);
  }
  this.cdr.detectChanges();
}
onRoleChange(roleId: number): void {
  this.form.roleId = Number(roleId);
  if (!this.canManageHierarchy) {       // 🆕
    this.form.managedSubServiceIds = [];
    this.form.managedServiceIds    = [];
  }
  this.cdr.detectChanges();
}
  checkEmail(): void {
    if (!this.form.email.trim()) return;
    this.userService.checkEmailUnique(this.form.email, this.userId ?? undefined).subscribe({
      next: (res) => {
        this.emailError = res.isUnique ? null : 'Cet email est déjà utilisé.';
        this.cdr.detectChanges();
      }
    });
  }

  submit(): void {
    if (!this.form.roleId || !this.form.firstName.trim() ||
        !this.form.lastName.trim() || !this.form.email.trim() || !this.form.hireDate) {
      this.error = 'Tous les champs obligatoires doivent être remplis.';
      return;
    }
    if (this.emailError) return;

    this.submitting = true;
    this.error      = null;
    const hireDateISO = this.toISOString(this.form.hireDate);

    if (this.isEditMode && this.userId) {
      const dto: UpdateUserDto = {
        roleId:               this.form.roleId,
        subServiceId:         this.form.subServiceId ?? undefined,
   managedSubServiceIds: this.canManageHierarchy ? this.form.managedSubServiceIds : [],
managedServiceIds:    this.canManageHierarchy ? this.form.managedServiceIds    : [],
        firstName:            this.form.firstName,
        lastName:             this.form.lastName,
        email:                this.form.email,
        hireDate:             hireDateISO,
        isActive:             this.form.isActive,
        level:                this.form.level
      };
      this.userService.updateUser(this.userId, dto).subscribe({
        next: () => this.router.navigate(['/users', this.userId]),
        error: (err) => {
          this.error      = `Erreur: ${err.error?.message || err.status}`;
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      const dto: CreateUserDto = {
        roleId:               this.form.roleId,
        subServiceId:         this.form.subServiceId ?? undefined,
     managedSubServiceIds: this.canManageHierarchy ? this.form.managedSubServiceIds : [],
managedServiceIds:    this.canManageHierarchy ? this.form.managedServiceIds    : [],
        firstName:            this.form.firstName,
        lastName:             this.form.lastName,
        email:                this.form.email,
        hireDate:             hireDateISO,
        level:                this.form.level
      };
      this.userService.createUser(dto).subscribe({
        next: (user) => this.router.navigate(['/users', user.id]),
        error: (err) => {
          this.error      = `Erreur: ${err.error?.message || err.status}`;
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  goBack(): void {
    this.isEditMode
      ? this.router.navigate(['/users', this.userId])
      : this.router.navigate(['/users']);
  }
}
