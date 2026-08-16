import { Component, OnInit, OnDestroy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { BodyPortalDirective } from '../../../../shared/directives/body-portal.directive';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';
import { KyntusFormDraftService } from '../../../../core/drafts/kyntus-form-draft.service';
import { KyntusObjectDraftBinder } from '../../../../core/drafts/kyntus-object-draft.binder';
import { CongeService } from '../../../../core/services/conge.service';
import { UserService } from '../../../users/services/user.service';
import { AuthService }  from '../../../../core/services/auth.service';
import {
  DemandeCongeDto, SoldeCongeDto, DemanderCongeCommand,
  TypeConge, TypeCongeExceptionnel, StatutDemande,
  TypeCongeLabels, StatutDemandeLabels, TypeCongeExceptionnelLabels,
  MOIS_LABELS, normalizeStatutDemande
} from '../../../../core/models/conge.models';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';

@Component({
  selector: 'app-conge-employe',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent, BodyPortalDirective],
  templateUrl: './conge-employe.component.html',
  styleUrls: ['./conge-employe.component.css']
})
export class CongeEmployeComponent implements OnInit, OnDestroy {

  private readonly toastSvc = inject(KyntusToastService);
  private readonly confirmService = inject(KyntusConfirmService);
  private readonly formDrafts = inject(KyntusFormDraftService);
  private draftBinder?: KyntusObjectDraftBinder<{
    showModal: boolean;
    form: DemanderCongeCommand;
  }>;

  demandes:  DemandeCongeDto[] = [];
  solde:     SoldeCongeDto | null = null;
  loading    = false;
  showModal  = false;
  employeId  = '';
  moisInterdits: number[] = [9, 10];
  disponibiliteMotif: string | null = null;

  TypeConge             = TypeConge;
  TypeCongeExceptionnel = TypeCongeExceptionnel;
  StatutDemande         = StatutDemande;
  typeCongeLabels       = TypeCongeLabels;
  statutLabels          = StatutDemandeLabels;
  exceptionnelLabels    = TypeCongeExceptionnelLabels;
  moisLabels            = MOIS_LABELS;

  get moisInterditsLabel(): string {
    return this.moisInterdits.map((m) => this.moisLabels[m] ?? String(m)).join(', ');
  }

  typesConge = [
    { value: TypeConge.Annuel,       label: TypeCongeLabels[TypeConge.Annuel] },
    { value: TypeConge.Exceptionnel, label: TypeCongeLabels[TypeConge.Exceptionnel] },
  ];

  typesExceptionnels = Object.entries(TypeCongeExceptionnelLabels).map(([k, v]) => ({
    value: +k as TypeCongeExceptionnel, label: v
  }));

  form: DemanderCongeCommand = {
    employeId: '', typeConge: TypeConge.Annuel,
    dateDebut: '', dateFin: '', motif: null, typeExceptionnel: null
  };
  filtreStatut: StatutDemande | '' = '';

  constructor(
    private svc:     CongeService,
    private userSvc: UserService,
    private authSvc: AuthService,
    private cdr:     ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'conge-employe-request',
      () => ({ showModal: this.showModal, form: { ...this.form } }),
      (s) => {
        if (s.form) this.form = { ...this.form, ...s.form };
        if (s.showModal && (s.form?.dateDebut || s.form?.motif)) {
          this.showModal = true;
        }
      },
    );
    this.draftBinder.start();

    this.userSvc.getCurrentUser().subscribe({
      next: (user) => {
        this.employeId = user.guid;
        this.form.employeId = user.guid;
        this.loadSolde();
        this.loadDemandes();
        this.loadMoisInterdits();
      },
      error: () => this.showToast('Impossible de récupérer le profil.', 'error')
    });
  }

  loadMoisInterdits(): void {
    this.svc.getPeriodesInterdites().subscribe({
      next: (dto) => {
        this.moisInterdits = dto.mois?.length ? dto.mois : [9, 10];
        this.cdr.detectChanges();
      },
      error: () => { /* défaut déjà 9–10 */ }
    });
  }

  checkDisponibilite(): void {
    this.disponibiliteMotif = null;
    this.touchDraft();
    if (!this.employeId || !this.form.dateDebut) return;
    const fin = this.form.dateFin || this.form.dateDebut;
    const debutIso = this.form.dateDebut + 'T00:00:00Z';
    const finIso = fin + 'T00:00:00Z';
    this.svc.getDisponibilite(this.employeId, debutIso, finIso).subscribe({
      next: (r) => {
        this.moisInterdits = r.moisInterdits?.length ? r.moisInterdits : this.moisInterdits;
        this.disponibiliteMotif = r.ok ? null : (r.motif || 'Période indisponible.');
        this.cdr.detectChanges();
      },
      error: () => {}
    });
  }

  ngOnDestroy(): void {
    this.draftBinder?.destroy();
  }

  touchDraft(): void {
    this.draftBinder?.touch();
  }

  async resetDraftForm(): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Effacer la saisie',
      message: 'Effacer la saisie et le brouillon de cette demande ?',
      confirmLabel: 'Effacer',
    });
    if (!ok) return;
    this.draftBinder?.discard();
    this.form = {
      employeId: this.employeId,
      typeConge: TypeConge.Annuel,
      dateDebut: '',
      dateFin: '',
      motif: null,
      typeExceptionnel: null,
    };
    this.disponibiliteMotif = null;
    this.restartDraftBinder();
    this.cdr.detectChanges();
  }

  private restartDraftBinder(): void {
    this.draftBinder?.destroy();
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'conge-employe-request',
      () => ({ showModal: this.showModal, form: { ...this.form } }),
      (s) => {
        if (s.form) this.form = { ...this.form, ...s.form };
        if (s.showModal && (s.form?.dateDebut || s.form?.motif)) {
          this.showModal = true;
        }
      },
    );
    this.draftBinder.start();
  }

  loadSolde(): void {
    this.svc.getSolde(this.employeId).subscribe({
      next: (s) => { this.solde = s; this.cdr.detectChanges(); },
      error: () => this.showToast('Impossible de charger le solde.', 'error')
    });
  }

  loadDemandes(): void {
    this.loading = true;
    this.cdr.detectChanges();
    const statut = this.filtreStatut !== '' ? this.filtreStatut : undefined;
    this.svc.getDemandesByEmploye(this.employeId, statut).subscribe({
      next: (data) => {
        this.demandes = data.map(d => ({ ...d, statut: normalizeStatutDemande(d.statut) }));
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); this.showToast('Erreur chargement.', 'error'); }
    });
  }

  openNouvelleDemande(): void {
    this.form = {
      employeId: this.employeId, typeConge: TypeConge.Annuel,
      dateDebut: '', dateFin: '', motif: null, typeExceptionnel: null
    };
    this.disponibiliteMotif = null;
    this.showModal = true;
    this.touchDraft();
  }

  onTypeCongeChange(): void {
    if (this.form.typeConge !== TypeConge.Exceptionnel) this.form.typeExceptionnel = null;
    this.form.dateFin = '';
    this.touchDraft();
  }

  isExceptionnel(): boolean    { return this.form.typeConge === TypeConge.Exceptionnel; }
  isDateFinManuelle(): boolean { return this.form.typeConge === TypeConge.Annuel; }

  submit(): void {
    if (!this.form.dateDebut) {
      this.showToast('La date de début est obligatoire.', 'error'); return;
    }
    if (this.isDateFinManuelle()) {
      if (!this.form.dateFin) {
        this.showToast('La date de fin est obligatoire.', 'error'); return;
      }
      if (new Date(this.form.dateFin) < new Date(this.form.dateDebut)) {
        this.showToast('La date de fin doit être après la date de début.', 'error'); return;
      }
    }
    if (this.isExceptionnel() && !this.form.typeExceptionnel) {
      this.showToast('Veuillez sélectionner un événement exceptionnel.', 'error'); return;
    }

    const fin = this.form.dateFin || this.form.dateDebut;
    const debutIso = this.form.dateDebut + 'T00:00:00Z';
    const finIso = fin + 'T00:00:00Z';

    this.svc.getDisponibilite(this.employeId, debutIso, finIso).subscribe({
      next: (r) => {
        if (!r.ok) {
          this.disponibiliteMotif = r.motif;
          this.showToast(r.motif || 'Période indisponible.', 'error');
          return;
        }
        this.sendDemande(debutIso, this.form.dateFin ? finIso : null);
      },
      error: () => this.sendDemande(debutIso, this.form.dateFin ? finIso : null)
    });
  }

  private sendDemande(debutIso: string, finIso: string | null): void {
    const cmd: DemanderCongeCommand = {
      employeId:        this.employeId,
      typeConge:        this.form.typeConge,
      dateDebut:        debutIso,
      dateFin:          finIso,
      motif:            this.form.motif || null,
      typeExceptionnel: this.form.typeExceptionnel ?? null
    };

    this.svc.demanderConge(cmd).subscribe({
      next: () => {
        this.showModal = false;
        this.disponibiliteMotif = null;
        this.draftBinder?.clear();
        this.loadDemandes();
        this.loadSolde();
        this.showToast('Demande envoyée !', 'success');
      },
      error: (err) => {
        this.showToast(err?.error?.message || JSON.stringify(err?.error) || 'Erreur.', 'error');
      }
    });
  }

  canAnnuler(d: DemandeCongeDto): boolean {
    const s = normalizeStatutDemande(d.statut);
    return s === StatutDemande.EnAttente || s === StatutDemande.EnAttenteRh;
  }

  async annuler(d: DemandeCongeDto): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Annuler la demande',
      message: 'Annuler cette demande ?',
      confirmLabel: 'Annuler la demande',
      variant: 'danger',
    });
    if (!ok) return;
    this.svc.annulerConge(d.id, this.employeId).subscribe({
      next: () => { this.loadDemandes(); this.loadSolde(); this.showToast('Demande annulée.', 'success'); },
      error: () => this.showToast('Impossible d\'annuler.', 'error')
    });
  }

  getStatutClass(statut: StatutDemande): string {
    const s = normalizeStatutDemande(statut);
    const map: Partial<Record<StatutDemande, string>> = {
      [StatutDemande.EnAttente]:   'badge-pending',
      [StatutDemande.EnAttenteRh]: 'badge-pending',
      [StatutDemande.Validee]:     'badge-valid',
      [StatutDemande.Refusee]:     'badge-refused',
      [StatutDemande.Annulee]:     'badge-cancel'
    };
    return map[s] || '';
  }

  getSoldePercent(): number {
    if (!this.solde || this.solde.soldeInitial === 0) return 0;
    return Math.round((this.solde.soldeRestant / this.solde.soldeInitial) * 100);
  }

  showToast(message: string, type: 'success' | 'error'): void {
    if (type === 'error') this.toastSvc.error(message);
    else this.toastSvc.success(message);
  }
}
