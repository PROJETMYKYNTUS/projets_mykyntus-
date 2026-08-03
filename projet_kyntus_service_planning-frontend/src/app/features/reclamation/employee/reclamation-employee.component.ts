import { Component, OnInit, OnDestroy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { KyntusToastService } from '../../../shared/components/ui/kyntus-toast.service';
import { KyntusFormDraftService } from '../../../core/drafts/kyntus-form-draft.service';
import { KyntusObjectDraftBinder } from '../../../core/drafts/kyntus-object-draft.binder';
import { Subscription } from 'rxjs';
import { ReclamationService } from '../../../core/services/reclamation.service';
import { PropositionService } from '../../../core/services/proposition.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  Reclamation, Proposition, ReclamationDetail, PropositionDetail,
  PaginatedResult, ReclamationType, CreateReclamationPayload,
  CreatePropositionPayload, SatisfactionPayload
} from '../../../core/models/reclamation.model';

type MainTab = 'reclamations' | 'propositions';
type SubView = 'list' | 'new' | 'detail';

@Component({
  selector: 'app-reclamation-employee',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, KyntusPageHeaderComponent],
  templateUrl: './reclamation-employee.component.html',
  styleUrls: ['./reclamation-employee.component.css']
})
export class ReclamationEmployeeComponent implements OnInit, OnDestroy {

  private readonly toastSvc = inject(KyntusToastService);
  private readonly formDrafts = inject(KyntusFormDraftService);
  private draftBinder?: KyntusObjectDraftBinder<{
    mainTab: MainTab;
    subView: SubView;
    newRec: CreateReclamationPayload;
    newProp: CreatePropositionPayload;
  }>;

  // ── State ────────────────────────────────────────
  mainTab: MainTab = 'reclamations';
  subView: SubView = 'list';

  // ── Réclamations ─────────────────────────────────
  reclamations: Reclamation[] = [];
  recTotal = 0;
  recPage  = 1;
  selectedRec: ReclamationDetail | null = null;

  newRec: CreateReclamationPayload = { titre: '', description: '', type: 'Administrative' };
  reclamationTypes: ReclamationType[] = [
    'ServiceQualite', 'RessourcesHumaines', 'Technique', 'Administrative', 'Autre'
  ];

  // ── Propositions ─────────────────────────────────
  propositions: Proposition[] = [];
  propTotal = 0;
  propPage  = 1;
  selectedProp: PropositionDetail | null = null;

  newProp: CreatePropositionPayload = { titre: '', description: '', beneficeAttendu: '' };

  // ── Satisfaction ─────────────────────────────────
  satForm: SatisfactionPayload = { note: 5, commentaire: '' };
  showSatForm = false;
  satTargetId = 0;

  // ── UI ───────────────────────────────────────────
  loading    = false;
  submitting = false;

  // ── Subscription ─────────────────────────────────
  private notifSub!: Subscription;

  constructor(
    private reclamationSvc:    ReclamationService,
    private propositionSvc:    PropositionService,
    private notificationService: NotificationService, // ✅ service partagé
    private cdr:               ChangeDetectorRef
  ) {}

  // ─────────────────────────────────────────────────
  ngOnInit(): void {
    this.draftBinder = new KyntusObjectDraftBinder(
      this.formDrafts,
      'reclamation-employee-forms',
      () => ({
        mainTab: this.mainTab,
        subView: this.subView,
        newRec: { ...this.newRec },
        newProp: { ...this.newProp },
      }),
      (s) => {
        if (s.newRec) this.newRec = { ...this.newRec, ...s.newRec };
        if (s.newProp) this.newProp = { ...this.newProp, ...s.newProp };
        if (s.mainTab) this.mainTab = s.mainTab;
        if (s.subView === 'new' && (s.newRec?.titre || s.newProp?.titre)) {
          this.subView = 'new';
        }
      },
    );
    this.draftBinder.start();
    this.loadReclamations();
    this.listenNotifications();
  }

  ngOnDestroy(): void {
    this.draftBinder?.destroy();
    // ✅ Juste unsubscribe — pas de hub.stop() (le hub est géré par NotificationService)
    this.notifSub?.unsubscribe();
  }

  touchDraft(): void {
    this.draftBinder?.touch();
  }

  // ── Écoute les notifications via le service partagé ──
private listenNotifications(): void {
  this.notifSub = this.notificationService.notifications$.subscribe(notifs => {
    // ✅ Écouter réclamations ET propositions
    const latest = notifs.find(
      n => (n.type === 'reclamation' || n.type === 'proposition') && !n.read
    );
    if (latest) {
      const type: 'success' | 'error' =
        latest.message.toLowerCase().includes('rejet') ? 'error' : 'success';
      this.showToast(latest.message, type);

      // ✅ Recharger la bonne liste selon le type
      if (latest.type === 'proposition') {
        this.loadPropositions();
      } else {
        this.loadReclamations();
      }
      this.cdr.detectChanges();
    }
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
        this.draftBinder?.clear();
        this.showToast('Réclamation soumise avec succès');
        this.loadReclamations();
      },
      error: () => {
        this.submitting = false;
        this.showToast('Erreur lors de la soumission', 'error');
      }
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
        this.draftBinder?.clear();
        this.showToast('Proposition soumise avec succès');
        this.loadPropositions();
      },
      error: () => {
        this.submitting = false;
        this.showToast('Erreur lors de la soumission', 'error');
      }
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
      Soumise:      'rp-status-soumise',
      EnCours:      'rp-status-encours',
      Traitee:      'rp-status-traitee',
      Rejetee:      'rp-status-rejetee',
      Cloturee:     'rp-status-cloturee',
      EnEvaluation: 'rp-status-encours',
      Approuvee:    'rp-status-traitee',
      Implementee:  'rp-status-implementee'
    };
    return map[status] ?? 'rp-status-soumise';
  }

  getPriorityClass(p: string): string {
    const map: Record<string, string> = {
      Basse:    'rp-prio-basse',
      Normale:  'rp-prio-normale',
      Haute:    'rp-prio-haute',
      Critique: 'rp-prio-critique'
    };
    return map[p] ?? 'rp-prio-normale';
  }

  canRate(item: Reclamation | Proposition): boolean {
    const ok = ['Traitee', 'Cloturee', 'Approuvee', 'Rejetee', 'Implementee'];
    return ok.includes(item.status) && !item.satisfactionNote;
  }

  getTypeLabel(type: string): string {
    const map: Record<string, string> = {
      ServiceQualite:    'Qualité',
      RessourcesHumaines: 'RH',
      Technique:         'Technique',
      Administrative:    'Admin',
      Autre:             'Autre'
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
    if (type === 'error') this.toastSvc.error(msg);
    else this.toastSvc.success(msg);
  }

  // ── Getters stats ─────────────────────────────────
  get recEnCours():     number { return this.reclamations.filter(r => r.status === 'EnCours').length; }
  get recTraitees():    number { return this.reclamations.filter(r => r.status === 'Traitee' || r.status === 'Cloturee').length; }
  get propApprouvees(): number { return this.propositions.filter(p => p.status === 'Approuvee' || p.status === 'Implementee').length; }
}