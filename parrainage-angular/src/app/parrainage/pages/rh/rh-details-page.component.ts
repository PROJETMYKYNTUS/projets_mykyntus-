import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AlertTriangle, Loader2, X as XIcon } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { CvPreviewPanelComponent } from '../../components/cv-preview-panel.component';
import { ReferralService } from '../../services/referral.service';
import { ParrainageStoreService } from '../../services/parrainage-store.service';
import { ParrainageNavService } from '../../state/parrainage-nav.service';
import { ParrainageRoleService } from '../../state/parrainage-role.service';
import type { ReferralStatus } from '../../models/referral.model';

const STATUS_STYLES: Record<ReferralStatus, string> = {
  SUBMITTED: 'bg-blue-500/15 text-blue-300 border-blue-500/40',
  PROCESSED: 'bg-cyan-500/15 text-cyan-300 border-cyan-500/40',
  APPROVED: 'bg-emerald-500/15 text-emerald-300 border-emerald-500/40',
  REJECTED: 'bg-red-500/15 text-red-300 border-red-500/40',
  REWARDED: 'bg-purple-500/15 text-purple-200 border-purple-500/40',
};
const STATUS_LABELS: Record<ReferralStatus, string> = {
  SUBMITTED: 'En attente',
  PROCESSED: 'Dossier traité',
  APPROVED: 'Validé',
  REJECTED: 'Rejeté',
  REWARDED: 'Prime versée',
};

type ToastType = 'success' | 'error' | 'info';

@Component({
  selector: 'app-rh-details-page',
  standalone: true,
  imports: [FormsModule, LucideIconComponent, CvPreviewPanelComponent],
  template: `
    <section class="flex-1 min-w-0 space-y-6">
      @if (toast().show) {
        <div [class]="'fixed top-4 left-1/2 -translate-x-1/2 z-[60] card-navy px-4 py-3 border-l-4 ' + toastBorder()">
          <div class="text-sm font-medium">{{ toast().message }}</div>
        </div>
      }

      <div class="space-y-2">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 class="text-2xl font-semibold text-slate-50">Décision RH</h1>
            <p class="text-sm text-slate-500 mt-1">Validation uniquement depuis l'écran de détail.</p>
          </div>
          <div class="flex items-center gap-3">
            <span [class]="'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ' + statusStyles[referral()?.status ?? 'SUBMITTED']">{{ statusLabels[referral()?.status ?? 'SUBMITTED'] }}</span>
            <button type="button" (click)="nav.setView('rh-management')" class="text-sm text-soft-blue hover:underline font-medium">
              ← Retour
            </button>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="card-navy p-10 text-center text-slate-500 text-sm">Chargement du dossier…</div>
      } @else if (!referral()) {
        <div class="card-navy p-10 text-center text-slate-400 text-sm">
          Parrainage introuvable.
        </div>
      } @else {
        @if (referral(); as ref) {
          <div class="space-y-6">
            @if (unauthorized) {
              <div class="card-navy p-5 border-red-500/30 text-red-200">
                Accès refusé. Seule la RH peut valider des dossiers.
              </div>
            }

            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div class="card-navy p-5 md:p-6 space-y-4">
                <div class="flex items-start justify-between gap-3">
                  <h2 class="text-sm font-semibold text-slate-200">Candidat</h2>
                  <span class="text-xs text-slate-500 font-mono">{{ ref.id }}</span>
                </div>
                <div class="space-y-2 text-sm">
                  <div class="flex justify-between gap-4">
                    <span class="text-slate-500">Nom</span>
                    <span class="text-slate-200 text-right font-medium">{{ ref.candidateName }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-slate-500">E-mail</span>
                    <span class="text-slate-200 text-right break-all">{{ ref.candidateEmail }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-slate-500">Téléphone</span>
                    <span class="text-slate-200 text-right break-all">{{ ref.candidatePhone }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-slate-500">Poste</span>
                    <span class="text-slate-200 text-right font-medium">{{ ref.position }}</span>
                  </div>
                </div>
                @if (ref.notes) {
                  <div class="pt-2 border-t border-navy-800">
                    <p class="text-xs text-slate-500 mb-1">Notes du parrain</p>
                    <p class="text-sm text-slate-300 whitespace-pre-wrap">{{ ref.notes }}</p>
                  </div>
                }
              </div>

              <div class="card-navy p-5 md:p-6 space-y-4">
                <h2 class="text-sm font-semibold text-slate-200">Parrain</h2>
                <div class="space-y-2 text-sm">
                  <div class="flex justify-between gap-4">
                    <span class="text-slate-500">Nom</span>
                    <span class="text-slate-200 text-right font-medium">{{ ref.referrerName }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-slate-500">Équipe</span>
                    <span class="text-slate-200 text-right">{{ ref.teamId }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-slate-500">Projet</span>
                    <span class="text-slate-200 text-right">{{ ref.projectName }}</span>
                  </div>
                </div>
              </div>
            </div>

            <div class="grid grid-cols-1 xl:grid-cols-3 gap-6">
              <div class="card-navy p-5 md:p-6 xl:col-span-2">
                <app-cv-preview-panel [cvUrl]="ref.cvUrl" />
              </div>

              <div class="card-navy p-5 md:p-6 space-y-4">
                <h2 class="text-sm font-semibold text-slate-200">Chronologie</h2>
                <div class="space-y-3">
                  <div class="text-sm">
                    <div class="text-xs uppercase tracking-wide text-slate-500">Date de soumission</div>
                    <div class="text-slate-200 mt-1 font-medium">
                      {{ fr(ref.createdAt) }}
                    </div>
                  </div>
                  <div class="space-y-2">
                    @if (statusHistory().length === 0) {
                      <p class="text-xs text-slate-500">Pas encore d'historique RH.</p>
                    } @else {
                      @for (h of statusHistory(); track h.id) {
                        <div class="flex items-start justify-between gap-4">
                          <div class="min-w-0">
                            <div class="flex items-center gap-2">
                              <span [class]="'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ' + statusStyles[historyStatus(h.action)]">{{ statusLabels[historyStatus(h.action)] }}</span>
                              <span class="text-sm font-medium text-slate-200">
                                {{ h.action === 'APPROVED' ? 'Validé' : h.action === 'PROCESSED' ? 'Dossier traité' : h.action === 'REJECTED' ? 'Rejeté' : 'Prime versée' }}
                              </span>
                            </div>
                            @if (h.comment) {
                              <p class="text-xs text-slate-500 mt-1">Commentaire : {{ h.comment }}</p>
                            }
                            @if (h.rewardAmount !== undefined && h.rewardAmount !== null) {
                              <p class="text-xs text-purple-200 mt-1">Montant engagé : {{ h.rewardAmount }} DH</p>
                            }
                          </div>
                          <div class="text-right text-xs text-slate-500 whitespace-nowrap">
                            {{ fr(h.createdAt) }}
                          </div>
                        </div>
                      }
                    }
                  </div>
                </div>
              </div>
            </div>

            @if (ref.status === 'PROCESSED') {
              <div class="card-navy p-4 border border-cyan-500/20 bg-cyan-500/5 text-sm text-cyan-100">
                Candidature traitée par la RH — en attente de l'entrée effective du candidat dans la société.
                Validez le dossier une fois la date d'entrée connue.
              </div>
            }

            @if (ref.status === 'APPROVED' || ref.status === 'REWARDED') {
              <div class="card-navy p-4 border border-emerald-500/20 bg-emerald-500/5 text-sm text-emerald-100">
                @if (ref.status === 'APPROVED') {
                  Période d'ancienneté jusqu'au {{ formatDate(ref.eligibleForPaymentAt) }} —
                  montant engagé {{ ref.rewardAmount }} DH.
                  @if (ref.paymentStatus === 'READY') {
                    <span class="block mt-1 text-amber-200">Dossier éligible — en attente du service comptabilité.</span>
                  }
                } @else {
                  Prime versée le {{ formatDate(ref.paidAt) }} ({{ ref.rewardAmount }} DH).
                }
              </div>
            }

            <div class="card-navy p-5 md:p-6 space-y-4">
              <div class="flex items-center justify-between gap-3">
                <h2 class="text-sm font-semibold text-slate-200">Décision</h2>
                @if (ref.status === 'REJECTED') {
                  <span class="text-xs text-red-300 inline-flex items-center gap-2">
                    <app-lucide-icon [icon]="alertIcon" className="h-3.5 w-3.5" />
                    Rejeté
                  </span>
                }
              </div>

              <div class="flex flex-wrap gap-3">
                @if (canProcess()) {
                  <button
                    type="button"
                    (click)="handleProcessClick()"
                    [disabled]="busy() || unauthorized"
                    class="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500 disabled:opacity-50"
                  >
                    Marquer comme traité
                  </button>
                }
                @if (canApprove()) {
                  <button
                    type="button"
                    (click)="handleApproveClick()"
                    [disabled]="busy() || unauthorized"
                    class="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
                  >
                    Valider l'entrée
                  </button>
                }
                <button
                  type="button"
                  (click)="handleRejectClick()"
                  [disabled]="!canReject() || busy() || unauthorized"
                  class="rounded-lg border border-red-500/50 px-4 py-2 text-sm font-medium text-red-300 hover:bg-red-500/10 disabled:opacity-50 disabled:hover:bg-transparent"
                >
                  Rejeter
                </button>
              </div>

              @if (mode() === 'process' && canProcess()) {
                <div class="space-y-4">
                  <p class="text-xs text-slate-400">
                    Confirmez que la candidature a été examinée (CV, entretiens). Le candidat n'a pas encore rejoint la société.
                  </p>
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-slate-500 mb-1.5">
                      Commentaire (facultatif)
                    </label>
                    <textarea
                      class="w-full min-h-[70px] rounded-lg border border-navy-800 bg-navy-950/40 px-3 py-2 text-sm text-slate-100"
                      [(ngModel)]="processComment"
                      placeholder="Notes internes RH…"
                    ></textarea>
                  </div>
                  <button
                    type="button"
                    (click)="confirmOpen.set('process')"
                    [disabled]="busy() || unauthorized"
                    class="rounded-lg bg-cyan-600 px-4 py-2 text-sm font-medium text-white hover:bg-cyan-500 disabled:opacity-50"
                  >
                    Confirmer le traitement
                  </button>
                </div>
              }

              @if (mode() === 'approve' && canApprove()) {
                <div class="space-y-4">
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-slate-500 mb-1.5">
                      Date d'entrée du candidat
                    </label>
                    <input
                      type="date"
                      class="w-full rounded-lg border border-navy-800 bg-navy-950/40 px-3 py-2 text-sm text-slate-100 focus:outline-none focus:ring-1 focus:ring-soft-blue"
                      [(ngModel)]="candidateStartDate"
                    />
                  </div>
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-slate-500 mb-1.5">
                      Montant engagé (DH)
                    </label>
                    <input
                      type="text"
                      inputmode="decimal"
                      class="w-full rounded-lg border border-navy-800 bg-navy-950/40 px-3 py-2 text-sm text-slate-100 focus:outline-none focus:ring-1 focus:ring-soft-blue"
                      [(ngModel)]="rewardAmount"
                      [placeholder]="String(suggestedReward())"
                    />
                    <p class="text-xs text-slate-500 mt-2">
                      Montant suggéré : {{ suggestedReward() }} DH (selon les règles)
                    </p>
                  </div>
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-slate-500 mb-1.5">
                      Commentaire (facultatif)
                    </label>
                    <textarea
                      class="w-full min-h-[70px] rounded-lg border border-navy-800 bg-navy-950/40 px-3 py-2 text-sm text-slate-100"
                      [(ngModel)]="approveComment"
                      placeholder="Notes internes RH…"
                    ></textarea>
                  </div>

                  <button
                    type="button"
                    (click)="confirmOpen.set('approve')"
                    [disabled]="busy() || unauthorized"
                    class="rounded-lg bg-soft-blue px-4 py-2 text-sm font-medium text-white hover:bg-blue-600 disabled:opacity-50"
                  >
                    Valider le dossier
                  </button>
                </div>
              }

              @if (mode() === 'reject' && canReject()) {
                <div class="space-y-4">
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-slate-500 mb-1.5">
                      Commentaire (facultatif)
                    </label>
                    <textarea
                      class="w-full min-h-[90px] rounded-lg border border-navy-800 bg-navy-950/40 px-3 py-2 text-sm text-slate-100 focus:outline-none focus:ring-1 focus:ring-soft-blue"
                      [(ngModel)]="rejectComment"
                      placeholder="Motif du refus…"
                    ></textarea>
                  </div>
                  <button
                    type="button"
                    (click)="confirmOpen.set('reject')"
                    [disabled]="busy() || unauthorized"
                    class="rounded-lg border border-red-500/50 px-4 py-2 text-sm font-medium text-red-300 hover:bg-red-500/10 disabled:opacity-50"
                  >
                    Rejeter
                  </button>
                </div>
              }

              @if (ref.status === 'REWARDED' || ref.status === 'REJECTED' || ref.status === 'APPROVED') {
                <p class="text-xs text-slate-500">
                  Décision finale enregistrée. Les actions sont désactivées.
                </p>
              }
            </div>
          </div>
        }
      }
    </section>

    @if (confirmOpen() === 'process') {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <button type="button" class="absolute inset-0 bg-navy-950/80 backdrop-blur-sm" aria-label="Fermer" (click)="confirmOpen.set(null)"></button>
        <div class="relative card-navy max-w-md w-full p-6 shadow-2xl border border-navy-800">
          <div class="flex items-start justify-between gap-4">
            <h3 class="text-lg font-semibold text-slate-50">Marquer comme traité</h3>
            <button type="button" class="rounded-lg p-1 text-slate-500 hover:text-slate-200 hover:bg-navy-800" (click)="confirmOpen.set(null)" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <p class="mt-3 text-sm text-slate-400 leading-relaxed">
            Candidat : {{ referral()?.candidateName ?? '' }}.
            Le dossier passera en « Dossier traité » en attente de l'entrée du candidat.
          </p>
          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button type="button" (click)="confirmOpen.set(null)" class="rounded-lg border border-navy-800 px-4 py-2 text-sm text-slate-300 hover:bg-navy-800/80" [disabled]="busy()">
              Annuler
            </button>
            <button type="button" (click)="handleConfirm()" class="rounded-lg px-4 py-2 text-sm font-medium bg-cyan-600 hover:bg-cyan-500 text-white" [disabled]="busy()">
              @if (busy()) {
                <span class="inline-flex items-center gap-2">
                  <app-lucide-icon [icon]="loaderIcon" className="h-4 w-4 animate-spin" />
                  Traitement…
                </span>
              } @else {
                Confirmer
              }
            </button>
          </div>
        </div>
      </div>
    }

    @if (confirmOpen() === 'approve') {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <button type="button" class="absolute inset-0 bg-navy-950/80 backdrop-blur-sm" aria-label="Fermer" (click)="confirmOpen.set(null)"></button>
        <div class="relative card-navy max-w-md w-full p-6 shadow-2xl border border-navy-800">
          <div class="flex items-start justify-between gap-4">
            <h3 class="text-lg font-semibold text-slate-50">Confirmer la validation</h3>
            <button type="button" class="rounded-lg p-1 text-slate-500 hover:text-slate-200 hover:bg-navy-800" (click)="confirmOpen.set(null)" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <p class="mt-3 text-sm text-slate-400 leading-relaxed">
            Candidat : {{ referral()?.candidateName ?? '' }}.
            Montant engagé {{ rewardAmount || '—' }} DH — entrée le {{ candidateStartDate || '—' }}.
            Le versement interviendra après la période minimum.
          </p>
          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button type="button" (click)="confirmOpen.set(null)" class="rounded-lg border border-navy-800 px-4 py-2 text-sm text-slate-300 hover:bg-navy-800/80" [disabled]="busy()">
              Annuler
            </button>
            <button type="button" (click)="handleConfirm()" class="rounded-lg px-4 py-2 text-sm font-medium bg-soft-blue hover:bg-blue-600 text-white" [disabled]="busy()">
              @if (busy()) {
                <span class="inline-flex items-center gap-2">
                  <app-lucide-icon [icon]="loaderIcon" className="h-4 w-4 animate-spin" />
                  Traitement…
                </span>
              } @else {
                Confirmer
              }
            </button>
          </div>
        </div>
      </div>
    }

    @if (confirmOpen() === 'reject') {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <button type="button" class="absolute inset-0 bg-navy-950/80 backdrop-blur-sm" aria-label="Fermer" (click)="confirmOpen.set(null)"></button>
        <div class="relative card-navy max-w-md w-full p-6 shadow-2xl border border-navy-800">
          <div class="flex items-start justify-between gap-4">
            <h3 class="text-lg font-semibold text-slate-50">Confirmer le refus</h3>
            <button type="button" class="rounded-lg p-1 text-slate-500 hover:text-slate-200 hover:bg-navy-800" (click)="confirmOpen.set(null)" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <p class="mt-3 text-sm text-slate-400 leading-relaxed">Candidat : {{ referral()?.candidateName ?? '' }}. Le statut passera à « Rejeté ».</p>
          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button type="button" (click)="confirmOpen.set(null)" class="rounded-lg border border-navy-800 px-4 py-2 text-sm text-slate-300 hover:bg-navy-800/80" [disabled]="busy()">
              Annuler
            </button>
            <button type="button" (click)="handleConfirm()" class="rounded-lg px-4 py-2 text-sm font-medium bg-red-600 hover:bg-red-500 text-white" [disabled]="busy()">
              @if (busy()) {
                <span class="inline-flex items-center gap-2">
                  <app-lucide-icon [icon]="loaderIcon" className="h-4 w-4 animate-spin" />
                  Traitement…
                </span>
              } @else {
                Confirmer
              }
            </button>
          </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RhDetailsPageComponent {
  private readonly referralService = inject(ReferralService);
  private readonly store = inject(ParrainageStoreService);
  readonly nav = inject(ParrainageNavService);
  private readonly role = inject(ParrainageRoleService);

  readonly String = String;

  readonly statusStyles = STATUS_STYLES;
  readonly statusLabels = STATUS_LABELS;
  readonly alertIcon = AlertTriangle;
  readonly loaderIcon = Loader2;
  readonly xIcon = XIcon;

  readonly loading = computed(() => this.store.loading());
  readonly referral = computed(() => {
    const rid = this.nav.selectedReferralId();
    if (!rid) return null;
    return this.store.referrals().find((r) => r.id === rid) ?? null;
  });
  readonly history = computed(() => {
    const rid = this.nav.selectedReferralId();
    if (!rid) return [];
    return this.store.history().filter((h) => h.referralId === rid);
  });
  readonly mode = signal<'none' | 'process' | 'approve' | 'reject'>('none');
  candidateStartDate = new Date().toISOString().slice(0, 10);
  rewardAmount = '';
  approveComment = '';
  processComment = '';
  rejectComment = '';
  readonly busy = signal(false);
  readonly confirmOpen = signal<null | 'process' | 'approve' | 'reject'>(null);
  readonly toast = signal<{ show: boolean; type: ToastType; message: string }>({ show: false, type: 'success', message: '' });

  get id(): string {
    return this.nav.selectedReferralId() ?? '';
  }

  get unauthorized(): boolean {
    return this.role.user().role !== 'RH';
  }

  readonly canProcess = computed(() => this.referral()?.status === 'SUBMITTED');
  readonly canApprove = computed(() => this.referral()?.status === 'PROCESSED');
  readonly canReject = computed(() => {
    const r = this.referral();
    return r ? r.status === 'SUBMITTED' || r.status === 'PROCESSED' : false;
  });

  readonly statusHistory = computed(() =>
    this.history()
      .slice()
      .sort((a, b) => a.createdAt.getTime() - b.createdAt.getTime())
      .filter((h) => h.action !== 'SUBMITTED'),
  );

  readonly suggestedReward = computed(() => {
    if (!this.id) return 500;
    return this.referralService.getSuggestedReward(this.id);
  });

  toastBorder(): string {
    const t = this.toast().type;
    return t === 'success' ? 'border-emerald-500' : t === 'error' ? 'border-red-500' : 'border-soft-blue';
  }

  historyStatus(action: string): ReferralStatus {
    if (action === 'PROCESSED') return 'PROCESSED';
    return action === 'APPROVED' ? 'APPROVED' : action === 'REJECTED' ? 'REJECTED' : 'REWARDED';
  }

  private actor() {
    const u = this.role.user();
    return { id: u.id ?? 'rh-1', label: u.name ?? 'RH' };
  }

  private showToast(type: ToastType, message: string): void {
    this.toast.set({ show: true, type, message });
    setTimeout(() => this.toast.update((t) => ({ ...t, show: false })), 3200);
  }

  handleProcessClick(): void {
    this.processComment = '';
    this.mode.set('process');
  }

  handleApproveClick(): void {
    if (!this.referral()) return;
    this.rewardAmount = String(this.suggestedReward());
    this.rejectComment = '';
    this.mode.set('approve');
  }

  handleRejectClick(): void {
    this.rejectComment = '';
    this.rewardAmount = '';
    this.mode.set('reject');
  }

  async handleConfirm(): Promise<void> {
    const id = this.id;
    if (!id || !this.referral() || this.busy()) return;
    this.busy.set(true);
    try {
      if (this.confirmOpen() === 'process') {
        const updated = await this.referralService.processReferral(
          id,
          this.processComment || undefined,
          this.actor(),
        );
        if (!updated) throw new Error('Échec du marquage.');
        this.showToast('success', 'Dossier marqué comme traité.');
        this.mode.set('none');
        this.confirmOpen.set(null);
        return;
      }
      if (this.confirmOpen() === 'approve') {
        const amount = Number(this.rewardAmount.replace(',', '.'));
        if (!Number.isFinite(amount) || amount <= 0) {
          this.showToast('error', 'Montant engagé invalide.');
          this.busy.set(false);
          this.confirmOpen.set(null);
          return;
        }
        if (!this.candidateStartDate) {
          this.showToast('error', 'Date d\'entrée requise.');
          this.busy.set(false);
          this.confirmOpen.set(null);
          return;
        }
        const updated = await this.referralService.approveReferral(
          id,
          {
            candidateStartDate: this.candidateStartDate,
            rewardAmount: amount,
            comment: this.approveComment || undefined,
          },
          this.actor(),
        );
        if (!updated) throw new Error('Échec de la validation.');
        this.showToast('success', 'Dossier validé — période d\'ancienneté en cours.');
        this.mode.set('none');
        this.confirmOpen.set(null);
        return;
      }
      if (this.confirmOpen() === 'reject') {
        await this.referralService.updateStatus(id, 'REJECTED', this.actor(), this.rejectComment || undefined);
        this.showToast('success', 'Décision enregistrée (rejet).');
        this.mode.set('none');
        this.confirmOpen.set(null);
      }
    } catch {
      this.showToast('error', 'Action impossible. Réessayez.');
      this.confirmOpen.set(null);
    } finally {
      this.busy.set(false);
    }
  }

  fr(d: Date): string {
    return new Date(d).toLocaleDateString('fr-FR');
  }

  formatDate(d?: Date): string {
    if (!d) return '—';
    return this.fr(d);
  }
}
