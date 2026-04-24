// src/app/features/reclamation/admin/reclamation-admin.component.ts

import { Component, OnInit,ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as signalR from '@microsoft/signalr';
import { ReclamationService } from '../../../core/services/reclamation.service';
import { PropositionService } from '../../../core/services/proposition.service';
import {
  Reclamation, Proposition, ReclamationDetail, PropositionDetail,
  PaginatedResult, SatisfactionReport, Priority,
  UpdateStatusPayload, AssignPayload, PrioriserPayload
} from '../../../core/models/reclamation.model';

type MainTab   = 'reclamations' | 'propositions';
type AdminView = 'list' | 'detail' | 'reporting' | 'historique';

@Component({
  selector: 'app-reclamation-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './reclamation-admin.component.html',
  styleUrls: ['./reclamation-admin.component.css']
})
export class ReclamationAdminComponent implements OnInit {
 
  // ── State ────────────────────────────────────────────
  mainTab:   MainTab   = 'reclamations';
  adminView: AdminView = 'list';
 
  // ── Filters ──────────────────────────────────────────
  filterStatus   = '';
  filterPriorite = '';
   private hubConnection!: signalR.HubConnection;
  // ── Réclamations data ─────────────────────────────────
  reclamations: Reclamation[]    = [];
  recTotal     = 0; recPage = 1;
  selectedRec: ReclamationDetail | null = null;
 
  // ── Propositions data ─────────────────────────────────
  propositions: Proposition[]    = [];
  propTotal    = 0; propPage = 1;
  selectedProp: PropositionDetail | null = null;
 
  // ── Reporting ─────────────────────────────────────────
  report: SatisfactionReport | null = null;
  reportFrom = ''; reportTo = '';
 
  // ── Historique ────────────────────────────────────────
  historique:  (ReclamationDetail | PropositionDetail)[] = [];
  histTotal    = 0; histPage = 1;
 
  // ── Action panels ─────────────────────────────────────
  activePanel: 'none' | 'traiter' | 'assigner' | 'prioriser' = 'none';
 
  traiterForm: UpdateStatusPayload = { status: 'EnCours', commentaire: '' };
  assignerForm: AssignPayload      = { assigneeId: '', assigneeNom: '' };
  prioriserForm: PrioriserPayload  = { priorite: 'Normale' };
 
  priorities: Priority[] = ['Basse', 'Normale', 'Haute', 'Critique'];
 
  recStatuts  = ['EnCours', 'Traitee', 'Rejetee', 'Cloturee'];
  propStatuts = ['EnEvaluation', 'Approuvee', 'Rejetee', 'EnCours', 'Implementee'];
 
  // ── UI ───────────────────────────────────────────────
  loading    = false;
  submitting = false;
  toastMsg   = '';
  toastType: 'success' | 'error' = 'success';
 
  constructor(
    private reclamationSvc: ReclamationService,
    private propositionSvc: PropositionService,
    private cdr: ChangeDetectorRef
  ) {}
 
  ngOnInit(): void { 
    this.startSignalR(); 
    this.loadList(); 
  }
  ngOnDestroy(): void {
  this.hubConnection?.stop();
}
startSignalR(): void {
  this.hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/reclamation', {
      accessTokenFactory: () => localStorage.getItem('token') ?? ''
    })
    .withAutomaticReconnect()
    .build();

  this.hubConnection.start().then(() => {
    this.hubConnection.invoke('JoinManagerGroup');  // ← ICI
  });

this.hubConnection.on('ReclamationNotification', (notif) => {
  const type: 'success' | 'error' =
    notif.type === 'warning' ? 'error' : 'success';
  this.showToast(`${notif.titre} — ${notif.message}`, type);
  this.loadList();       // admin

  this.cdr.detectChanges();
});
}
 
  // ── Tab ──────────────────────────────────────────────
  setTab(tab: MainTab): void {
    this.mainTab   = tab;
    this.adminView = 'list';
    this.selectedRec = null; this.selectedProp = null;
    this.activePanel = 'none';
    this.filterStatus = ''; this.filterPriorite = '';
    this.loadList();
  }
 
  setView(v: AdminView): void {
    this.adminView = v;
    this.activePanel = 'none';
    if (v === 'list')       this.loadList();
    if (v === 'reporting')  this.loadReport();
    if (v === 'historique') this.loadHistorique();
  }
 
  // ── Load list ────────────────────────────────────────
  loadList(): void {
    this.loading = true;
    if (this.mainTab === 'reclamations') {
      this.reclamationSvc.getAll(this.recPage, 20, this.filterStatus || undefined, this.filterPriorite || undefined)
        .subscribe({
          next: (r: PaginatedResult<Reclamation>) => {
            this.reclamations = r.items; this.recTotal = r.totalCount;
          },
          error:    () => { this.showToast('Erreur de chargement', 'error'); },
          complete: () => { this.loading = false; this.cdr.detectChanges(); }
        });
    } else {
      this.propositionSvc.getAll(this.propPage, 20, this.filterStatus || undefined)
        .subscribe({
          next: (r: PaginatedResult<Proposition>) => {
            this.propositions = r.items; this.propTotal = r.totalCount;
          },
          error:    () => { this.showToast('Erreur de chargement', 'error'); },
          complete: () => { this.loading = false; this.cdr.detectChanges(); }
        });
    }
  }
 
  applyFilters(): void { this.recPage = 1; this.propPage = 1; this.loadList(); }
 
  // ── Detail ───────────────────────────────────────────
  openDetail(id: number): void {
    this.loading = true; this.activePanel = 'none';
    if (this.mainTab === 'reclamations') {
      this.reclamationSvc.getById(id).subscribe({
        next:     (r) => { this.selectedRec = r; this.adminView = 'detail'; },
        error:    () => { this.loading = false; this.cdr.detectChanges(); },
        complete: () => { this.loading = false; this.cdr.detectChanges(); }
      });
    } else {
      this.propositionSvc.getById(id).subscribe({
        next:     (p) => { this.selectedProp = p; this.adminView = 'detail'; },
        error:    () => { this.loading = false; this.cdr.detectChanges(); },
        complete: () => { this.loading = false; this.cdr.detectChanges(); }
      });
    }
  }
 
  // ── Actions ──────────────────────────────────────────
  submitTraiter(): void {
    if (!this.selectedRec && !this.selectedProp) return;
    const id = (this.selectedRec ?? this.selectedProp)!.id;
    this.submitting = true;
    const obs = this.mainTab === 'reclamations'
      ? this.reclamationSvc.traiter(id, this.traiterForm)
      : this.propositionSvc.evaluer(id, this.traiterForm);
    obs.subscribe({
      next: () => {
        this.submitting = false; this.activePanel = 'none';
        this.showToast('Statut mis à jour');
        this.openDetail(id);
      },
      error: () => { this.submitting = false; this.showToast('Erreur', 'error'); }
    });
  }
 
  submitAssigner(): void {
    if (!this.assignerForm.assigneeId.trim() || !this.assignerForm.assigneeNom.trim()) return;
    const id = (this.selectedRec ?? this.selectedProp)!.id;
    this.submitting = true;
    const obs = this.mainTab === 'reclamations'
      ? this.reclamationSvc.assigner(id, this.assignerForm)
      : this.propositionSvc.assigner(id, this.assignerForm);
    obs.subscribe({
      next: () => {
        this.submitting = false; this.activePanel = 'none';
        this.showToast('Assigné avec succès');
        this.openDetail(id);
      },
      error: () => { this.submitting = false; this.showToast('Erreur', 'error'); }
    });
  }
 
  submitPrioriser(): void {
    const id = (this.selectedRec ?? this.selectedProp)!.id;
    this.submitting = true;
    const obs = this.mainTab === 'reclamations'
      ? this.reclamationSvc.prioriser(id, this.prioriserForm)
      : this.propositionSvc.prioriser(id, this.prioriserForm);
    obs.subscribe({
      next: () => {
        this.submitting = false; this.activePanel = 'none';
        this.showToast('Priorité mise à jour');
        this.openDetail(id);
      },
      error: () => { this.submitting = false; this.showToast('Erreur', 'error'); }
    });
  }
 
  // ── Reporting ─────────────────────────────────────────
  loadReport(): void {
    this.loading = true;
    if (this.mainTab === 'reclamations') {
      this.reclamationSvc.getReporting(this.reportFrom || undefined, this.reportTo || undefined).subscribe({
        next:     (r) => { this.report = r; },
        error:    () => { this.showToast('Erreur reporting', 'error'); },
        complete: () => { this.loading = false; this.cdr.detectChanges(); }
      });
    } else {
      this.propositionSvc.getReporting(this.reportFrom || undefined, this.reportTo || undefined).subscribe({
        next:     (r) => { this.report = r; },
        error:    () => { this.showToast('Erreur reporting', 'error'); },
        complete: () => { this.loading = false; this.cdr.detectChanges(); }
      });
    }
  }
 
  getBarWidth(count: number): string {
    if (!this.report || this.report.totalDemandes === 0) return '0%';
    return Math.round((count / this.report.totalDemandes) * 100) + '%';
  }
 
  getStatutEntries(): { key: string; val: number }[] {
    return Object.entries(this.report?.parStatut ?? {}).map(([key, val]) => ({ key, val }));
  }
  getPrioriteEntries(): { key: string; val: number }[] {
    return Object.entries(this.report?.parPriorite ?? {}).map(([key, val]) => ({ key, val }));
  }
  getNoteEntries(): { key: number; val: number }[] {
    return Object.entries(this.report?.repartitionNotes ?? {})
      .map(([key, val]) => ({ key: +key, val }))
      .sort((a, b) => b.key - a.key);
  }
 
  // ── Historique ────────────────────────────────────────
  loadHistorique(): void {
    this.loading = true;
    if (this.mainTab === 'reclamations') {
      this.reclamationSvc.getHistorique(undefined, this.histPage).subscribe({
        next:     (r) => { this.historique = r.items; this.histTotal = r.totalCount; },
        error:    () => { this.showToast('Erreur historique', 'error'); },
        complete: () => { this.loading = false; this.cdr.detectChanges(); }
      });
    } else {
      this.propositionSvc.getHistorique(undefined, this.histPage).subscribe({
        next:     (r) => { this.historique = r.items; this.histTotal = r.totalCount; },
        error:    () => { this.showToast('Erreur historique', 'error'); },
        complete: () => { this.loading = false; this.cdr.detectChanges(); }
      });
    }
  }
 
  // ── Helpers ──────────────────────────────────────────
  goBack(): void {
    this.adminView = 'list'; this.selectedRec = null;
    this.selectedProp = null; this.activePanel = 'none';
    this.loadList();
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
 
  getTypeLabel(type: string): string {
    const map: Record<string, string> = {
      ServiceQualite: 'Qualité', RessourcesHumaines: 'RH',
      Technique: 'Technique', Administrative: 'Admin', Autre: 'Autre'
    };
    return map[type] ?? type;
  }
 
  get currentStatuts(): string[] {
    return this.mainTab === 'reclamations' ? this.recStatuts : this.propStatuts;
  }
 
  get recPages():  number[] { return Array.from({ length: Math.ceil(this.recTotal / 20) },  (_, i) => i + 1); }
  get propPages(): number[] { return Array.from({ length: Math.ceil(this.propTotal / 20) }, (_, i) => i + 1); }
 
  changePage(p: number): void {
    if (this.mainTab === 'reclamations') this.recPage  = p;
    else                                 this.propPage = p;
    this.loadList();
  }
 
  private showToast(msg: string, type: 'success' | 'error' = 'success'): void {
    this.toastMsg = msg; this.toastType = type;
    setTimeout(() => this.toastMsg = '', 3500);
  }
}