import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ServiceService } from '../../services/service';
import { ServiceDetail } from '../../services-module';

@Component({
  selector: 'app-service-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './service-detail.component.html',
  styleUrls: ['./service-detail.component.css']
})
export class ServiceDetailComponent implements OnInit {
  service: ServiceDetail | null = null;
  loading = false;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private serviceService: ServiceService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadService(id);
  }

  loadService(id: number): void {
    this.loading = true;
    this.error = null;
    this.serviceService.getServiceDetails(id).subscribe({
      next: (service: ServiceDetail) => {
        this.service = service;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.error = `Erreur: ${err.status}`;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  editService(): void {
    this.router.navigate(['/services', 'edit', this.service?.id]);
  }

  goBack(): void {
    this.router.navigate(['/services']);
  }

  deleteService(): void {
    if (!this.service) return;
    if (confirm('Supprimer ce service ?')) {
      this.serviceService.deleteService(this.service.id).subscribe({
        next: () => this.router.navigate(['/services']),
        error: (err: any) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}