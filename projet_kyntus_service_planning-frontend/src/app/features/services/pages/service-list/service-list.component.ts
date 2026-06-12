import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServiceService } from '../../services/service';
import { Service } from '../../services-module';
import { NavigationActionsService } from '../../../../core/navigation/navigation-actions.service';

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
    private navActions: NavigationActionsService,
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

  goToOrganisationRh(): void {
    void this.navActions.openOrganisationRh('poles');
  }
}
