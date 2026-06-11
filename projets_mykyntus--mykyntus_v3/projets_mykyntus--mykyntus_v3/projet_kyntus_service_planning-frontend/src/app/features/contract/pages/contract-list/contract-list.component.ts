// features/contract/pages/contract-list/contract-list.component.ts

import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ContractService, ContractResponse } from '../../services/contract.service';

interface Stats {
  total: number; actifs: number;
  periodeEssai: number; alertes: number; expires: number;
}

@Component({
  selector: 'app-contract-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './contract-list.component.html',
  styleUrls: ['./contract-list.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class ContractListComponent implements OnInit {

  contracts:         ContractResponse[] = [];
  filteredContracts: ContractResponse[] = [];

  loading  = false;
  deleting = false;

  searchTerm   = '';
  filterType   = '';
  filterStatus = '';
  filterAlerts = false;

  showDeleteModal   = false;
  contractToDelete: ContractResponse | null = null;

  stats: Stats = { total: 0, actifs: 0, periodeEssai: 0, alertes: 0, expires: 0 };

  constructor(
    private contractService: ContractService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadContracts();
  }

  // ── Navigation ──
  goToDashboard(): void        { this.router.navigate(['/dashboard']); }   // ← ajouté
  goToCreate(): void           { this.router.navigate(['/contracts/new']); }
  goToEdit(id: number): void   { this.router.navigate(['/contracts', id, 'edit']); }
  goToDetail(id: number): void { this.router.navigate(['/contracts', id]); }

  // ── Load ──
  loadContracts(): void {
    this.loading = true;
    this.cdr.detectChanges();

    this.contractService.getAll().subscribe({
      next: data => {
        this.contracts = data;
        this.applyFilters();
        this.computeStats();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        console.error('Erreur chargement contrats:', err);
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Filtres ──
  applyFilters(): void {
    let r = [...this.contracts];
    if (this.searchTerm.trim())
      r = r.filter(c => c.userFullName.toLowerCase().includes(this.searchTerm.toLowerCase()));
    if (this.filterType)   r = r.filter(c => c.type   === this.filterType);
    if (this.filterStatus) r = r.filter(c => c.status === this.filterStatus);
    if (this.filterAlerts) r = r.filter(c => c.isAlertActive);
    this.filteredContracts = r;
    this.cdr.detectChanges();
  }

  toggleAlertFilter(): void {
    this.filterAlerts = !this.filterAlerts;
    this.applyFilters();
  }

  computeStats(): void {
    this.stats = {
      total:        this.contracts.length,
      actifs:       this.contracts.filter(c => c.status === 'Actif').length,
      periodeEssai: this.contracts.filter(c => c.status === "En période d'essai").length,
      alertes:      this.contracts.filter(c => c.isAlertActive).length,
      expires:      this.contracts.filter(c => c.status === 'Expiré').length,
    };
  }

  // ── Suppression ──
  openDeleteModal(c: ContractResponse): void {
    this.contractToDelete = c;
    this.showDeleteModal  = true;
  }

  closeModal(): void {
    this.showDeleteModal  = false;
    this.contractToDelete = null;
  }

  deleteContract(): void {
    if (!this.contractToDelete) return;
    this.deleting = true;
    this.contractService.delete(this.contractToDelete.id).subscribe({
      next: () => { this.deleting = false; this.closeModal(); this.loadContracts(); },
      error: () => { this.deleting = false; }
    });
  }

  // ── Helpers UI ──
  getInitials(name: string): string {
    return (name ?? '??').split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2);
  }

  typeBadge(type: string): string {
    return ({ CDI: 'b-cdi', CDD: 'b-cdd', Stage: 'b-stage', Interim: 'b-interim' } as any)[type] ?? '';
  }

  statusBadge(s: string): string {
    return ({
      "En période d'essai": 's-essai',
      'Actif':   's-actif',
      'Expiré':  's-expire',
      'Résilié': 's-resilie'
    } as any)[s] ?? '';
  }

  daysPercent(c: ContractResponse): number {
    if (!c.endDate || c.joursRestants == null) return 0;
    const total = (new Date(c.endDate).getTime() - new Date(c.startDate).getTime()) / 86400000;
    return Math.min(100, Math.max(0, (c.joursRestants / total) * 100));
  }

  daysBarClass(d: number): string {
    return d <= 15 ? 'fill-danger' : d <= 30 ? 'fill-warn' : 'fill-ok';
  }

  daysTextClass(d: number): string {
    return d <= 15 ? 'txt-danger' : d <= 30 ? 'txt-warn' : 'txt-ok';
  }
}