import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SubServiceService } from '../../services/sub-service.service';
import { ServiceService } from '../../../services/services/service';
import { Service } from '../../../services/services-module';
import { CreateSubServiceDto, UpdateSubServiceDto } from '../../sub-services-module';

@Component({
  selector: 'app-sub-service-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sub-service-form.component.html',
  styleUrls: ['./sub-service-form.component.css']
})
export class SubServiceFormComponent implements OnInit {
  isEditMode = false;
  subServiceId: number | null = null;
  services: Service[] = [];
  loading = false;
  submitting = false;
  error: string | null = null;
  codeError: string | null = null;

  form = {
    serviceId: 0,
    name: '',
    code: ''
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private subServiceService: SubServiceService,
    private serviceService: ServiceService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadServices();
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.subServiceId = Number(id);
      this.loadSubService(this.subServiceId);
    }
  }

  loadServices(): void {
    this.serviceService.getAllServices().subscribe({
      next: (services) => {
        this.services = services;
        this.cdr.detectChanges();
      },
      error: () => this.error = 'Impossible de charger les services.'
    });
  }

  loadSubService(id: number): void {
    this.loading = true;
    this.subServiceService.getSubServiceById(id).subscribe({
      next: (sub) => {
        this.form = {
          serviceId: sub.serviceId,
          name: sub.name,
          code: sub.code
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

  checkCode(): void {
    if (!this.form.code.trim()) return;
    this.subServiceService.checkCodeUnique(this.form.code, this.subServiceId ?? undefined).subscribe({
      next: (res) => {
        this.codeError = res.isUnique ? null : 'Ce code est déjà utilisé.';
        this.cdr.detectChanges();
      }
    });
  }

  submit(): void {
    if (!this.form.serviceId || !this.form.name.trim() || !this.form.code.trim()) {
      this.error = 'Tous les champs sont obligatoires.';
      return;
    }
    if (this.codeError) return;

    this.submitting = true;
    this.error = null;

    if (this.isEditMode && this.subServiceId) {
      const dto: UpdateSubServiceDto = { ...this.form };
      this.subServiceService.updateSubService(this.subServiceId, dto).subscribe({
        next: () => this.router.navigate(['/sub-services', this.subServiceId]),
        error: (err) => {
          this.error = `Erreur: ${err.error?.message || err.status}`;
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      const dto: CreateSubServiceDto = { ...this.form };
      this.subServiceService.createSubService(dto).subscribe({
        next: (sub) => this.router.navigate(['/sub-services', sub.id]),
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
      ? this.router.navigate(['/sub-services', this.subServiceId])
      : this.router.navigate(['/sub-services']);
  }
}