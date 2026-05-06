// features/contract/pages/notification-list/notification-list.component.ts

import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ContractService, ContractNotification } from '../../services/contract.service';

type FilterType = 'all' | 'unread' | 'warn' | 'mid';

@Component({
  selector: 'app-notification-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './Notification-list.component.html',
  styleUrls: ['./Notification-list.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class NotificationListComponent implements OnInit {

  notifications:         ContractNotification[] = [];
  filteredNotifications: ContractNotification[] = [];

  loading      = false;   // ← FALSE par défaut
  activeFilter: FilterType = 'all';
  stats = { total: 0, warn: 0, mid: 0 };

  constructor(
    private contractService: ContractService,
    private router: Router,
    private cdr: ChangeDetectorRef  // ← ajouter
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loading = true;
    this.cdr.detectChanges();

    this.contractService.getNotifications().subscribe({
      next: data => {
        this.notifications = data;
        this.computeStats();
        this.applyFilter();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        console.error('Erreur chargement notifications:', err);
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  computeStats(): void {
    this.stats.total = this.notifications.length;
    this.stats.warn  = this.notifications.filter(n => n.type.startsWith('AvantFin')).length;
    this.stats.mid   = this.notifications.filter(n => n.type.startsWith('MiParcours')).length;
  }

  setFilter(f: FilterType): void {
    this.activeFilter = f;
    this.applyFilter();
  }

  applyFilter(): void {
    switch (this.activeFilter) {
      case 'warn': this.filteredNotifications = this.notifications.filter(n => n.type.startsWith('AvantFin'));    break;
      case 'mid':  this.filteredNotifications = this.notifications.filter(n => n.type.startsWith('MiParcours')); break;
      default:     this.filteredNotifications = [...this.notifications];
    }
    this.cdr.detectChanges();
  }

  onCardClick(n: ContractNotification): void {
    this.router.navigate(['/contracts', n.contractId]);
  }

  goBack(): void { this.router.navigate(['/contracts']); }

  getIcon(type: string): string {
    const map: Record<string, string> = {
      MiParcoursCDD: '⏳', MiParcoursPeriodeEssai: '📋',
      AvantFinCDD: '⚠️', AvantFinPeriodeEssai: '⚠️',
      AvantFinStage: '🎓', AvantFinInterim: '🔄',
    };
    return map[type] ?? '🔔';
  }

  getIconClass(type: string): string {
    return type.startsWith('AvantFin') ? 'ic-warn' : 'ic-info';
  }

  getTypeLabel(type: string): string {
    const map: Record<string, string> = {
      MiParcoursCDD: 'Mi-parcours CDD', MiParcoursPeriodeEssai: 'Mi-parcours essai',
      AvantFinCDD: 'Avant fin CDD', AvantFinPeriodeEssai: 'Avant fin essai',
      AvantFinStage: 'Avant fin stage', AvantFinInterim: 'Avant fin intérim',
    };
    return map[type] ?? type;
  }

  getTypeBadgeClass(type: string): string {
    return type.startsWith('AvantFin') ? 'badge-warn' : 'badge-info';
  }
}