import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { SubServiceService } from '../../services/sub-service.service';
import { SubService } from '../../sub-services-module';

@Component({
  selector: 'app-sub-service-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sub-service-list.component.html',
  styleUrls: ['./sub-service-list.component.css']
})
export class SubServiceListComponent implements OnInit {
  subServices: SubService[] = [];
  loading = false;
  error: string | null = null;

  constructor(
    private subServiceService: SubServiceService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadSubServices();
  }
  // Dans sub-service-list.component.ts — ajouter ces 2 méthodes

getTotalEmployees(): number {
  return this.subServices.reduce((sum, s) => sum + (s.employeesCount ?? 0), 0);
}

getUniqueServices(): number {
  return new Set(this.subServices.map(s => s.serviceName)).size;
}

  loadSubServices(): void {
    this.loading = true;
    this.error = null;
    this.subServiceService.getAllSubServices().subscribe({
      next: (subServices: SubService[]) => {
        this.subServices = subServices;
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
  viewSubService(id: number): void { this.router.navigate(['/sub-services', id]); }
  editSubService(id: number): void { this.router.navigate(['/sub-services', 'edit', id]); }
  createSubService(): void { this.router.navigate(['/sub-services', 'create']); }

  deleteSubService(id: number): void {
    if (confirm('Supprimer ce sous-service ?')) {
      this.subServiceService.deleteSubService(id).subscribe({
        next: () => this.loadSubServices(),
        error: (err: any) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}