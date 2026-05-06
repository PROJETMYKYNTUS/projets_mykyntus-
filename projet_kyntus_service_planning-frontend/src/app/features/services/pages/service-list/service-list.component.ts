import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ServiceService } from '../../services/service';
import { Service } from '../../services-module';

@Component({
  selector: 'app-service-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './service-list.component.html',
  styleUrls: ['./service-list.component.css']
})
export class ServiceListComponent implements OnInit {
  services: Service[] = [];
  loading = false;
  error: string | null = null;

  constructor(
    private serviceService: ServiceService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadServices();
  }

  loadServices(): void {
    this.loading = true;
    this.error = null;
    this.serviceService.getAllServices().subscribe({
      next: (services: Service[]) => {
        this.services = services;
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

  goToDashboard(): void { this.router.navigate(['/dashboard']); }
  viewService(id: number): void { this.router.navigate(['/services', id]); }
  editService(id: number): void { this.router.navigate(['/services', 'edit', id]); }
  createService(): void { this.router.navigate(['/services', 'create']); }

  deleteService(id: number): void {
    if (confirm('Supprimer ce service ?')) {
      this.serviceService.deleteService(id).subscribe({
        next: () => this.loadServices(),
        error: (err: any) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}