import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ServiceService } from '../../services/service';
import { FloorService } from '../../../floors/services/floor.service';
import { Floor } from '../../../floors/models/floor.model';
import { CreateServiceDto, UpdateServiceDto } from '../../services-module';

@Component({
  selector: 'app-service-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './service-form.component.html',
  styleUrls: ['./service-form.component.css']
})
export class ServiceFormComponent implements OnInit {
  isEditMode = false;
  serviceId: number | null = null;
  floors: Floor[] = [];
  loading = false;
  submitting = false;
  error: string | null = null;
  codeError: string | null = null;

  form = {
    floorId: 0,
    name: '',
    code: ''
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private serviceService: ServiceService,
    private floorService: FloorService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadFloors();
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.serviceId = Number(id);
      this.loadService(this.serviceId);
    }
  }

  loadFloors(): void {
    this.floorService.getAllFloors().subscribe({
      next: (floors) => {
        this.floors = floors;
        this.cdr.detectChanges();
      },
      error: () => this.error = 'Impossible de charger les étages.'
    });
  }

  loadService(id: number): void {
    this.loading = true;
    this.serviceService.getServiceById(id).subscribe({
      next: (service) => {
        this.form = {
          floorId: service.floorId,
          name: service.name,
          code: service.code
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
    this.serviceService.checkCodeUnique(this.form.code, this.serviceId ?? undefined).subscribe({
      next: (res) => {
        this.codeError = res.isUnique ? null : 'Ce code est déjà utilisé.';
        this.cdr.detectChanges();
      }
    });
  }

  submit(): void {
    if (!this.form.floorId || !this.form.name.trim() || !this.form.code.trim()) {
      this.error = 'Tous les champs sont obligatoires.';
      return;
    }
    if (this.codeError) return;

    this.submitting = true;
    this.error = null;

    if (this.isEditMode && this.serviceId) {
      const dto: UpdateServiceDto = { ...this.form };
      this.serviceService.updateService(this.serviceId, dto).subscribe({
        next: () => this.router.navigate(['/services', this.serviceId]),
        error: (err) => {
          this.error = `Erreur: ${err.error?.message || err.status}`;
          this.submitting = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      const dto: CreateServiceDto = { ...this.form };
      this.serviceService.createService(dto).subscribe({
        next: (service) => this.router.navigate(['/services', service.id]),
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
      ? this.router.navigate(['/services', this.serviceId])
      : this.router.navigate(['/services']);
  }
}