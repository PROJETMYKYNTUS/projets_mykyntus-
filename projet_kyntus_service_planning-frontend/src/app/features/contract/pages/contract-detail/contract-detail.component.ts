// features/contract/pages/contract-detail/contract-detail.component.ts

import { Component, OnInit, ViewEncapsulation, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ContractService, ContractResponse } from '../../services/contract.service';

@Component({
  selector: 'app-contract-detail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './contract-detail.component.html',
  styleUrls: ['./contract-detail.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class ContractDetailComponent implements OnInit {

  contract: ContractResponse | null = null;
  loading = false;   // ← false par défaut

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private contractService: ContractService,
    private cdr: ChangeDetectorRef   // ← ajouté
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadContract(+id);
    } else {
      this.router.navigate(['/contracts']);
    }
  }

  loadContract(id: number): void {
    this.loading = true;
    this.cdr.detectChanges();

    this.contractService.getById(id).subscribe({
      next: c => {
        this.contract = c;
        this.loading  = false;
        this.cdr.detectChanges();
      },
      error: err => {
        console.error('Erreur chargement détail contrat:', err);
        this.loading = false;
        this.cdr.detectChanges();
        this.router.navigate(['/contracts']);
      }
    });
  }

  goBack():   void { this.router.navigate(['/contracts']); }
  goToEdit(): void { this.router.navigate(['/contracts', this.contract?.id, 'edit']); }

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

  daysPercent(): number {
    const c = this.contract!;
    if (!c.endDate || c.joursRestants == null) return 0;
    const total = (new Date(c.endDate).getTime() - new Date(c.startDate).getTime()) / 86400000;
    return Math.min(100, Math.max(0, (c.joursRestants / total) * 100));
  }

  probationPercent(): number {
    const c = this.contract!;
    if (!c.probationEndDate || c.joursRestantsPeriodeEssai == null) return 0;
    const total = (new Date(c.probationEndDate).getTime() - new Date(c.startDate).getTime()) / 86400000;
    return Math.min(100, Math.max(0, (c.joursRestantsPeriodeEssai / total) * 100));
  }

  daysBarClass(d: number): string {
    return d <= 15 ? 'fill-danger' : d <= 30 ? 'fill-warn' : 'fill-ok';
  }

  daysTextClass(d: number): string {
    return d <= 15 ? 'txt-danger' : d <= 30 ? 'txt-warn' : 'txt-ok';
  }
}