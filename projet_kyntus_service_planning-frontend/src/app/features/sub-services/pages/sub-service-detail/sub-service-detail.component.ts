import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { SubServiceService } from '../../services/sub-service.service';
import { SubServiceDetail } from '../../sub-services-module';

@Component({
  selector: 'app-sub-service-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sub-service-detail.Component.html',
  styleUrls: ['./sub-service-detail.component.css']
})
export class SubServiceDetailComponent implements OnInit {
  subService: SubServiceDetail | null = null;
  loading = false;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private subServiceService: SubServiceService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadSubService(id);
  }
  getInitials(emp: any): string {
  const first = emp.firstName?.charAt(0) ?? '';
  const last  = emp.lastName?.charAt(0)  ?? '';
  return (first + last).toUpperCase();
}

  loadSubService(id: number): void {
    this.loading = true;
    this.error = null;
    this.subServiceService.getSubServiceDetails(id).subscribe({
      next: (subService: SubServiceDetail) => {
        this.subService = subService;
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

  goBack(): void { this.router.navigate(['/sub-services']); }
  editSubService(): void { this.router.navigate(['/sub-services', 'edit', this.subService?.id]); }

  deleteSubService(): void {
    if (!this.subService) return;
    if (confirm('Supprimer ce sous-service ?')) {
      this.subServiceService.deleteSubService(this.subService.id).subscribe({
        next: () => this.router.navigate(['/sub-services']),
        error: (err: any) => alert(`Erreur: ${err.error?.message}`)
      });
    }
  }
}