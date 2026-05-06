import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { UserService } from '../../services/user.service';
import { SubServiceService } from '../../../sub-services/services/sub-service.service';
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

  roles: RoleOption[] = [
    { id: 1, name: 'RH' },
    { id: 2, name: 'Employé' },
    { id: 3, name: 'Manager' },
    { id: 4, name: 'Coach' }
  ];

  readonly managerRoleId = 3;
  readonly coachRoleId = 4;

  form = {
    roleId: 0,
    subServiceId: null as number | null,
    managedSubServiceIds: [] as number[],
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
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadSubServices();
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.userId = Number(id);
      this.loadUser(this.userId);
    }
  }

  get isManagerOrCoach(): boolean {
    return this.form.roleId === this.managerRoleId ||
           this.form.roleId === this.coachRoleId;
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
        this.form = {
          roleId: user.roleId,
          subServiceId: user.subServiceId ?? null,
          managedSubServiceIds: user.managedSubServices?.map(s => s.id) ?? [],
          firstName: user.firstName,
          lastName: user.lastName,
          email: user.email,
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

  isManagedSubService(id: number): boolean {
    return this.form.managedSubServiceIds.includes(id);
  }

  toggleManagedSubService(id: number): void {
    const idx = this.form.managedSubServiceIds.indexOf(id);
    if (idx === -1) {
      this.form.managedSubServiceIds.push(id);
    } else {
      this.form.managedSubServiceIds.splice(idx, 1);
    }
  }
  onRoleChange(roleId: number): void {
  this.form.roleId = Number(roleId);
  if (!this.isManagerOrCoach) {
    this.form.managedSubServiceIds = [];
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
    this.error = null;

    // ✅ Conversion propre avant envoi au backend
    const hireDateISO = this.toISOString(this.form.hireDate);

    if (this.isEditMode && this.userId) {
      const dto: UpdateUserDto = {
        roleId: this.form.roleId,
        subServiceId: this.form.subServiceId ?? undefined,
        managedSubServiceIds: this.isManagerOrCoach ? this.form.managedSubServiceIds : [],
        firstName: this.form.firstName,
        lastName: this.form.lastName,
        email: this.form.email,
        hireDate: hireDateISO,   // ✅ ISO complet
        isActive: this.form.isActive,
          level:                this.form.level 
      };
      this.userService.updateUser(this.userId, dto).subscribe({
        next: () => this.router.navigate(['/users', this.userId]),
        error: (err) => {
          this.error = `Erreur: ${err.error?.message || err.status}`;
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });

    } else {
      const dto: CreateUserDto = {
        roleId: this.form.roleId,
        subServiceId: this.form.subServiceId ?? undefined,
        managedSubServiceIds: this.isManagerOrCoach ? this.form.managedSubServiceIds : [],
        firstName: this.form.firstName,
        lastName: this.form.lastName,
        email: this.form.email,
        hireDate: hireDateISO  ,  
          level:                this.form.level// ✅ ISO complet
      };
      this.userService.createUser(dto).subscribe({
        next: (user) => this.router.navigate(['/users', user.id]),
        error: (err) => {
          this.error = `Erreur: ${err.error?.message || err.status}`;
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