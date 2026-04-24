// src/app/features/reclamation/employee/reclamation-employee.component.ts

import { Component, OnInit,ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as signalR from '@microsoft/signalr';
import { ReclamationService } from '../../../core/services/reclamation.service';
import { PropositionService } from '../../../core/services/proposition.service';
import {
  Reclamation, Proposition, ReclamationDetail, PropositionDetail,
  PaginatedResult, ReclamationType, Priority, CreateReclamationPayload,
  CreatePropositionPayload, SatisfactionPayload
} from '../../../core/models/reclamation.model';

type MainTab   = 'reclamations' | 'propositions';
type SubView   = 'list' | 'new' | 'detail';

@Component({
  selector: 'app-reclamation-employee',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './reclamation-employee.component.html',
  styleUrls: ['./reclamation-employee.component.css']
})
export class ReclamationEmployeeComponent implements OnInit {
 
  // ── State ────────────────────────────────────────
  mainTab: MainTab = 'reclamations';
  subView: SubView = 'list';
 
  // ── Réclamations ─────────────────────────────────
  reclamations: Reclamation[] = [];
  recTotal    = 0;
  recPage     = 1;
  selectedRec: ReclamationDetail | null = null;
  private hubConnection!: signalR.HubConnection;
  newRec: CreateReclamationPayload = { titre: '', description: '', type: 'Administrative' };
  reclamationTypes: ReclamationType[] = [
    'ServiceQualite', 'RessourcesHumaines', 'Technique', 'Administrative', 'Autre'
  ];
 
  // ── Propositions ─────────────────────────────────
  propositions: Proposition[] = [];
  propTotal   = 0;
  propPage    = 1;
  selectedProp: PropositionDetail | null = null;
 
  newProp: CreatePropositionPayload = { titre: '', description: '', beneficeAttendu: '' };
 
  // ── Satisfaction ─────────────────────────────────
  satForm: SatisfactionPayload = { note: 5, commentaire: '' };
  showSatForm  = false;
  satTargetId  = 0;
 
  // ── UI ───────────────────────────────────────────
  loading   = false;
  submitting = false;
  toastMsg  = '';
  toastType: 'success' | 'error' = 'success';
 
  constructor(
    private reclamationSvc: ReclamationService,
    private propositionSvc: PropositionService,
    private cdr: ChangeDetectorRef
  ) {}
 
  ngOnInit(): void {
     this.loadReclamations();
     this.startSignalR()
  }

startSignalR(): void {
  this.hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/reclamation', {
      accessTokenFactory: () => localStorage.getItem('token') ?? ''
    })
    .withAutomaticReconnect()
    .build();

  this.hubConnection.start().then(() => {
    // ❌ Supprimez : const userId = this.authService.getUserId();
    
    // ✅ Lire l'userId depuis le token JWT stocké
    const token = localStorage.getItem('token') ?? '';
    const userId = token ? JSON.parse(atob(token.split('.')[1]))?.sub ?? '' : '';
    
    if (userId) {
      this.hubConnection.invoke('JoinUserGroup', userId);
    }
  });
  // Écouter les notifications
this.hubConnection.on('ReclamationNotification', (notif) => {
  const type: 'success' | 'error' =
    notif.type === 'warning' ? 'error' : 'success';
  this.showToast(`${notif.titre} — ${notif.message}`, type);

  this.loadReclamations(); // employee
  this.cdr.detectChanges();
});
}
 
  // ── Tab switching ─────────────────────────────────
  setTab(tab: MainTab): void {
    this.mainTab = tab;
    this.subView = 'list';
    this.selectedRec  = null;
    this.selectedProp = null;
    if (tab === 'reclamations') this.loadReclamations();
    else                        this.loadPropositions();
  }
   ngOnDestroy(): void {
    this.hubConnection?.stop();  // ← nettoyage
  }

  // ── Réclamations ─────────────────────────────────
  loadReclamations(): void {
    this.loading = true;
    this.reclamationSvc.getMesDemandes(this.recPage).subscribe({
      next: (res: PaginatedResult<Reclamation>) => {
        this.reclamations = res.items;
        this.recTotal     = res.totalCount;
      },
      error:    () => { this.showToast('Erreur de chargement', 'error'); },
      complete: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }
 
  openRec(id: number): void {
    this.loading = true;
    this.reclamationSvc.getById(id).subscribe({
      next:     (r: ReclamationDetail) => { this.selectedRec = r; this.subView = 'detail'; },
      error:    () => { this.loading = false; this.cdr.detectChanges(); },
      complete: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }
 
  submitRec(): void {
    if (!this.newRec.titre.trim() || !this.newRec.description.trim()) return;
    this.submitting = true;
    this.reclamationSvc.soumettre(this.newRec).subscribe({
      next: () => {
        this.submitting = false;
        this.newRec     = { titre: '', description: '', type: 'Administrative' };
        this.subView    = 'list';
        this.showToast('Réclamation soumise avec succès');
        this.loadReclamations();
      },
      error: () => { this.submitting = false; this.showToast('Erreur lors de la soumission', 'error'); }
    });
  }
 
  // ── Propositions ─────────────────────────────────
  loadPropositions(): void {
    this.loading = true;
    this.propositionSvc.getMesDemandes(this.propPage).subscribe({
      next: (res: PaginatedResult<Proposition>) => {
        this.propositions = res.items;
        this.propTotal    = res.totalCount;
      },
      error:    () => { this.showToast('Erreur de chargement', 'error'); },
      complete: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }
 
  openProp(id: number): void {
    this.loading = true;
    this.propositionSvc.getById(id).subscribe({
      next:     (p: PropositionDetail) => { this.selectedProp = p; this.subView = 'detail'; },
      error:    () => { this.loading = false; this.cdr.detectChanges(); },
      complete: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }
 
  submitProp(): void {
    if (!this.newProp.titre.trim() || !this.newProp.description.trim()) return;
    this.submitting = true;
    this.propositionSvc.soumettre(this.newProp).subscribe({
      next: () => {
        this.submitting = false;
        this.newProp    = { titre: '', description: '', beneficeAttendu: '' };
        this.subView    = 'list';
        this.showToast('Proposition soumise avec succès');
        this.loadPropositions();
      },
      error: () => { this.submitting = false; this.showToast('Erreur lors de la soumission', 'error'); }
    });
  }
 
  // ── Satisfaction ─────────────────────────────────
  openSatisfaction(id: number): void {
    this.satTargetId = id;
    this.satForm     = { note: 5, commentaire: '' };
    this.showSatForm = true;
  }
 
  submitSatisfaction(): void {
    const svc = this.mainTab === 'reclamations' ? this.reclamationSvc : this.propositionSvc;
    svc.noteSatisfaction(this.satTargetId, this.satForm).subscribe({
      next: () => {
        this.showSatForm = false;
        this.showToast('Satisfaction enregistrée');
        if (this.mainTab === 'reclamations') this.loadReclamations();
        else this.loadPropositions();
      },
      error: () => this.showToast('Erreur lors de l\'enregistrement', 'error')
    });
  }
 
  // ── Helpers UI ────────────────────────────────────
  goBack(): void {
    this.subView      = 'list';
    this.selectedRec  = null;
    this.selectedProp = null;
  }
 
  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      Soumise: 'rp-status-soumise', EnCours: 'rp-status-encours',
      Traitee: 'rp-status-traitee', Rejetee: 'rp-status-rejetee',
      Cloturee: 'rp-status-cloturee', EnEvaluation: 'rp-status-encours',
      Approuvee: 'rp-status-traitee', Implementee: 'rp-status-implementee'
    };
    return map[status] ?? 'rp-status-soumise';
  }
 
  getPriorityClass(p: string): string {
    const map: Record<string, string> = {
      Basse: 'rp-prio-basse', Normale: 'rp-prio-normale',
      Haute: 'rp-prio-haute', Critique: 'rp-prio-critique'
    };
    return map[p] ?? 'rp-prio-normale';
  }
 
  canRate(item: Reclamation | Proposition): boolean {
    const ok = ['Traitee', 'Cloturee', 'Approuvee', 'Rejetee', 'Implementee'];
    return ok.includes(item.status) && !item.satisfactionNote;
  }
 
  getTypeLabel(type: string): string {
    const map: Record<string, string> = {
      ServiceQualite: 'Qualité', RessourcesHumaines: 'RH',
      Technique: 'Technique', Administrative: 'Admin', Autre: 'Autre'
    };
    return map[type] ?? type;
  }
 
  get recPages(): number[] {
    return Array.from({ length: Math.ceil(this.recTotal / 10) }, (_, i) => i + 1);
  }
  get propPages(): number[] {
    return Array.from({ length: Math.ceil(this.propTotal / 10) }, (_, i) => i + 1);
  }
 
  changePage(p: number): void {
    if (this.mainTab === 'reclamations') { this.recPage = p;  this.loadReclamations(); }
    else                                 { this.propPage = p; this.loadPropositions(); }
  }
 
  private showToast(msg: string, type: 'success' | 'error' = 'success'): void {
    this.toastMsg  = msg;
    this.toastType = type;
    setTimeout(() => this.toastMsg = '', 3500);
  }
  
 
  // ── Getters pour stats rapides ────────────────────
  get recEnCours():  number { return this.reclamations.filter(r => r.status === 'EnCours').length; }
  get recTraitees(): number { return this.reclamations.filter(r => r.status === 'Traitee' || r.status === 'Cloturee').length; }
  get propApprouvees(): number { return this.propositions.filter(p => p.status === 'Approuvee' || p.status === 'Implementee').length; }
}