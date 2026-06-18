import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CongeService } from '../../../../core/services/conge.service';
import { UserService } from '../../../users/services/user.service';
import { AuthService }  from '../../../../core/services/auth.service';
import {
  DemandeCongeDto, StatutDemande, TypeConge, TypeCongeExceptionnel,
  TypeCongeLabels, StatutDemandeLabels, TypeCongeExceptionnelLabels,
  RefuserCongeRequest, ValiderCongeRequest,
  DemanderCongeCommand
} from '../../../../core/models/conge.models';

@Component({
  selector: 'app-conge-manager',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './conge-manager.component.html',
  styleUrls: ['./conge-manager.component.css']
})
export class CongeManagerComponent implements OnInit {

  demandes:         DemandeCongeDto[] = [];
  demandesFiltrees: DemandeCongeDto[] = [];
  loading           = false;
  managerId         = '';

  showRefusModal   = false;
  demandeSelectee: DemandeCongeDto | null = null;
  commentaireRefus = '';

  showDetailModal  = false;
  showMyCongeModal = false;
  showMyDemandeModal = false;
  demandeDetail:   DemandeCongeDto | null = null;

  StatutDemande      = StatutDemande;
  TypeConge          = TypeConge;
  typeCongeLabels    = TypeCongeLabels;
  statutLabels       = StatutDemandeLabels;
  exceptionnelLabels = TypeCongeExceptionnelLabels;

  filtreStatut: StatutDemande | '' = StatutDemande.EnAttente;
  filtreSearch  = '';

  // ── Nouvelle demande (manager en tant qu'employé) ──────────────────────────
  typesConge = [
    { value: TypeConge.Annuel,       label: TypeCongeLabels[TypeConge.Annuel] },
    { value: TypeConge.Exceptionnel, label: TypeCongeLabels[TypeConge.Exceptionnel] },
    { value: TypeConge.Paternite,    label: TypeCongeLabels[TypeConge.Paternite] },
    { value: TypeConge.Maternite,    label: TypeCongeLabels[TypeConge.Maternite] }
  ];

  typesExceptionnels = Object.entries(TypeCongeExceptionnelLabels).map(([k, v]) => ({
    value: +k as TypeCongeExceptionnel, label: v
  }));

  myForm: DemanderCongeCommand = {
    employeId:        '',
    typeConge:        TypeConge.Annuel,
    dateDebut:        '',
    dateFin:          '',
    motif:            null,
    typeExceptionnel: null
  };
  // ──────────────────────────────────────────────────────────────────────────

  get myEmployeIdAsString(): string {
    return this.managerId;
  }

  get nbEnAttente(): number { return this.demandes.filter(d => d.statut === StatutDemande.EnAttente).length; }
  get nbValidees():  number { return this.demandes.filter(d => d.statut === StatutDemande.Validee).length; }
  get nbRefusees():  number { return this.demandes.filter(d => d.statut === StatutDemande.Refusee).length; }

  toast = { show: false, message: '', type: 'success' };

  constructor(
    private svc:     CongeService,
    private userSvc: UserService,
    private authSvc: AuthService,
    private cdr:     ChangeDetectorRef
  ) {}

  ngOnInit(): void {
   this.userSvc.getUserByAuthId(this.authSvc.getAuthUserId()).subscribe({
      next: (user) => {
        this.managerId = user.guid;
        console.log('✅ managerId GUID :', this.managerId);
        this.loadDemandes();
      },
      error: () => this.showToast('Impossible de récupérer le profil.', 'error')
    });
  }

  get myEmployeId(): string {
    return this.managerId;
  }

  loadDemandes(): void {
    this.loading = true;
    this.cdr.detectChanges();
    this.svc.getDemandesByManager(this.managerId).subscribe({
      next: (data) => {
        this.demandes = data;
        this.appliquerFiltres();
        this.loading  = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.cdr.detectChanges();
        this.showToast('Erreur lors du chargement.', 'error');
      }
    });
  }

  appliquerFiltres(): void {
    let result = [...this.demandes];
    if (this.filtreStatut !== '') {
      result = result.filter(d => d.statut === +this.filtreStatut);
    }
    if (this.filtreSearch.trim()) {
      const q = this.filtreSearch.trim().toLowerCase();
      result = result.filter(d =>
        d.employeId.toLowerCase().includes(q) ||
        (d.motif && d.motif.toLowerCase().includes(q))
      );
    }
    this.demandesFiltrees = result;
  }

  onFiltreChange(): void { this.appliquerFiltres(); }

  valider(d: DemandeCongeDto): void {
    if (!confirm(`Valider le congé de ${d.nombreJours} jours ?`)) return;
    const req: ValiderCongeRequest = { managerId: this.managerId, commentaire: null };
    this.svc.validerConge(d.id, req).subscribe({
      next: () => { this.loadDemandes(); this.showToast('Congé validé !', 'success'); },
      error: (err) => this.showToast(err?.error?.message || 'Impossible de valider.', 'error')
    });
  }

  ouvrirRefus(d: DemandeCongeDto): void {
    this.demandeSelectee  = d;
    this.commentaireRefus = '';
    this.showRefusModal   = true;
  }

  confirmerRefus(): void {
    if (!this.demandeSelectee) return;
    if (!this.commentaireRefus.trim()) {
      this.showToast('Le motif de refus est obligatoire.', 'error');
      return;
    }
    const req: RefuserCongeRequest = { managerId: this.managerId, commentaire: this.commentaireRefus };
    this.svc.refuserConge(this.demandeSelectee.id, req).subscribe({
      next: () => { this.showRefusModal = false; this.loadDemandes(); this.showToast('Demande refusée.', 'success'); },
      error: () => this.showToast('Impossible de refuser.', 'error')
    });
  }

  voirDetail(d: DemandeCongeDto): void {
    this.demandeDetail   = d;
    this.showDetailModal = true;
  }

  getStatutClass(statut: StatutDemande): string {
    const map: Record<StatutDemande, string> = {
      [StatutDemande.EnAttente]: 'badge-pending',
      [StatutDemande.Validee]:   'badge-valid',
      [StatutDemande.Refusee]:   'badge-refused',
      [StatutDemande.Annulee]:   'badge-cancel'
    };
    return map[statut] || '';
  }

  // ── Nouvelle demande (manager en tant qu'employé) ──────────────────────────
  openMyDemande(): void {
    this.myForm = {
      employeId:        this.managerId,
      typeConge:        TypeConge.Annuel,
      dateDebut:        '',
      dateFin:          '',
      motif:            null,
      typeExceptionnel: null
    };
    this.showMyDemandeModal = true;
  }

  onMyTypeChange(): void {
    if (this.myForm.typeConge !== TypeConge.Exceptionnel)
      this.myForm.typeExceptionnel = null;
  }

  submitMyDemande(): void {
    const cmd: DemanderCongeCommand = {
      ...this.myForm,
      dateDebut: this.myForm.dateDebut + 'T00:00:00Z',
      dateFin:   this.myForm.dateFin   ? this.myForm.dateFin + 'T00:00:00Z' : null,
      motif:     this.myForm.motif     || null
    };
    this.svc.demanderConge(cmd).subscribe({
      next: () => {
        this.showMyDemandeModal = false;
        this.showToast('Demande envoyée avec succès !', 'success');
      },
      error: (err) => {
        this.showToast(err?.error?.message || 'Erreur lors de l\'envoi.', 'error');
      }
    });
  }
  // ──────────────────────────────────────────────────────────────────────────

  showToast(message: string, type: 'success' | 'error'): void {
    this.toast = { show: true, message, type };
    setTimeout(() => this.toast.show = false, 4000);
  }
}