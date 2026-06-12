import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SubServiceService } from '../../services/sub-service.service';
import { SubService } from '../../sub-services-module';
import { NavigationActionsService } from '../../../../core/navigation/navigation-actions.service';

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
    private navActions: NavigationActionsService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadSubServices();
  }

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

  goToOrganisationRh(): void {
    void this.navActions.openOrganisationRh('cellules');
  }
}
