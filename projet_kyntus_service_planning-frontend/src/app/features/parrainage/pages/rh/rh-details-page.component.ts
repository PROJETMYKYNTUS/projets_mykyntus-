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
            <h1 class="text-2xl font-semibold text-primary">Décision RH</h1>
            <p class="text-sm text-muted mt-1">Validation uniquement depuis l'écran de détail.</p>
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
        <div class="card-navy p-10 text-center text-muted text-sm">Chargement du dossier…</div>
      } @else if (!referral()) {
        <div class="card-navy p-10 text-center text-muted text-sm">
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

            @if (ruleLabel()) {
              <div class="card-navy p-4 border border-cyan-500/20 bg-cyan-500/5 text-sm text-cyan-100">
                Règle appliquée : {{ ruleLabel() }}
              </div>
            }

            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div class="card-navy p-5 md:p-6 space-y-4">
                <div class="flex items-start justify-between gap-3">
                  <h2 class="text-sm font-semibold text-primary">Candidat</h2>
                  <span class="text-xs text-muted font-mono">{{ ref.id }}</span>
                </div>
                <div class="space-y-2 text-sm">
                  <div class="flex justify-between gap-4">
                    <span class="text-muted">Nom</span>
                    <span class="text-primary text-right font-medium">{{ ref.candidateName }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-muted">E-mail</span>
                    <span class="text-primary text-right break-all">{{ ref.candidateEmail }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-muted">Téléphone</span>
                    <span class="text-primary text-right break-all">{{ ref.candidatePhone }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-muted">Poste</span>
                    <span class="text-primary text-right font-medium">{{ ref.position }}</span>
                  </div>
                </div>
                @if (ref.notes) {
                  <div class="pt-2 border-t border-default">
                    <p class="text-xs text-muted mb-1">Notes du parrain</p>
                    <p class="text-sm text-primary whitespace-pre-wrap">{{ ref.notes }}</p>
                  </div>
                }
              </div>

              <div class="card-navy p-5 md:p-6 space-y-4">
                <h2 class="text-sm font-semibold text-primary">Parrain</h2>
                <div class="space-y-2 text-sm">
                  <div class="flex justify-between gap-4">
                    <span class="text-muted">Nom</span>
                    <span class="text-primary text-right font-medium">{{ ref.referrerName }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-muted">Équipe</span>
                    <span class="text-primary text-right">{{ ref.teamId }}</span>
                  </div>
                  <div class="flex justify-between gap-4">
                    <span class="text-muted">Projet</span>
                    <span class="text-primary text-right">{{ ref.projectName }}</span>
                  </div>
                </div>
              </div>
            </div>

            <div class="grid grid-cols-1 xl:grid-cols-3 gap-6">
              <div class="card-navy p-5 md:p-6 xl:col-span-2">
                <app-cv-preview-panel [cvUrl]="ref.cvUrl" />
              </div>

              <div class="card-navy p-5 md:p-6 space-y-4">
                <h2 class="text-sm font-semibold text-primary">Chronologie</h2>
                <div class="space-y-3">
                  <div class="text-sm">
                    <div class="text-xs uppercase tracking-wide text-muted">Date de soumission</div>
                    <div class="text-primary mt-1 font-medium">
                      {{ fr(ref.createdAt) }}
                    </div>
                  </div>
                  <div class="space-y-2">
                    @if (statusHistory().length === 0) {
                      <p class="text-xs text-muted">Pas encore d'historique RH.</p>
                    } @else {
                      @for (h of statusHistory(); track h.id) {
                        <div class="flex items-start justify-between gap-4">
                          <div class="min-w-0">
                            <div class="flex items-center gap-2">
                              <span [class]="'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ' + statusStyles[historyStatus(h.action)]">{{ statusLabels[historyStatus(h.action)] }}</span>
                              <span class="text-sm font-medium text-primary">
                                {{ h.action === 'APPROVED' ? 'Validé' : h.action === 'PROCESSED' ? 'Dossier traité' : h.action === 'REJECTED' ? 'Rejeté' : 'Prime versée' }}
                              </span>
                            </div>
                            @if (h.comment) {
                              <p class="text-xs text-muted mt-1">Commentaire : {{ h.comment }}</p>
                            }
                            @if (h.rewardAmount !== undefined && h.rewardAmount !== null) {
                              <p class="text-xs text-purple-200 mt-1">Montant engagé : {{ h.rewardAmount }} DH</p>
                            }
                          </div>
                          <div class="text-right text-xs text-muted whitespace-nowrap">
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
                  @if (ref.paymentStatus === 'NOT_ELIGIBLE') {
                    <span class="block mt-1 text-primary">En attente de la fin de la période minimum.</span>
                  }
                  @if (ref.paymentStatus === 'AWAITING_RH') {
                    <span class="block mt-1 text-amber-200">
                      Période écoulée — confirmez que le candidat est toujours en poste avant transmission à la comptabilité.
                    </span>
                  }
                  @if (ref.paymentStatus === 'READY') {
                    <span class="block mt-1 text-amber-200">Éligibilité confirmée — en attente du service comptabilité.</span>
                  }
                } @else {
                  Prime versée le {{ formatDate(ref.paidAt) }} ({{ ref.rewardAmount }} DH).
                }
              </div>
            }

            <div class="card-navy p-5 md:p-6 space-y-4">
              <div class="flex items-center justify-between gap-3">
                <h2 class="text-sm font-semibold text-primary">Décision</h2>
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
                @if (canConfirmEligibility()) {
                  <button
                    type="button"
                    (click)="handleConfirmEligibilityClick()"
                    [disabled]="busy() || unauthorized"
                    class="rounded-lg bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-500 disabled:opacity-50"
                  >
                    Confirmer l'éligibilité
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
                  <p class="text-xs text-muted">
                    Confirmez que la candidature a été examinée (CV, entretiens). Le candidat n'a pas encore rejoint la société.
                  </p>
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-muted mb-1.5">
                      Commentaire (facultatif)
                    </label>
                    <textarea
                      class="w-full min-h-[70px] rounded-lg border border-default bg-input/40 px-3 py-2 text-sm text-primary"
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
                    <label class="block text-xs font-medium uppercase tracking-wide text-muted mb-1.5">
                      Date d'entrée du candidat
                    </label>
                    <input
                      type="date"
                      class="w-full rounded-lg border border-default bg-input/40 px-3 py-2 text-sm text-primary focus:outline-none focus:ring-1 focus:ring-soft-blue"
                      [(ngModel)]="candidateStartDate"
                    />
                  </div>
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-muted mb-1.5">
                      Montant engagé (DH)
                    </label>
                    <input
                      type="text"
                      inputmode="decimal"
                      class="w-full rounded-lg border border-default bg-input/40 px-3 py-2 text-sm text-primary focus:outline-none focus:ring-1 focus:ring-soft-blue"
                      [(ngModel)]="rewardAmount"
                      [placeholder]="String(suggestedReward())"
                    />
                    <p class="text-xs text-muted mt-2">
                      Montant suggéré : {{ suggestedReward() }} DH — ancienneté minimale {{ suggestedMinDuration() }} mois
                    </p>
                  </div>
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-muted mb-1.5">
                      Commentaire (facultatif)
                    </label>
                    <textarea
                      class="w-full min-h-[70px] rounded-lg border border-default bg-input/40 px-3 py-2 text-sm text-primary"
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

              @if (mode() === 'confirm-eligibility' && canConfirmEligibility()) {
                <div class="space-y-4">
                  <p class="text-xs text-muted">
                    Vérifiez que le candidat recruté est toujours en poste avant de transmettre le dossier à la comptabilité.
                  </p>
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-muted mb-1.5">
                      Commentaire (facultatif)
                    </label>
                    <textarea
                      class="w-full min-h-[70px] rounded-lg border border-default bg-input/40 px-3 py-2 text-sm text-primary"
                      [(ngModel)]="eligibilityComment"
                      placeholder="Ex. : candidat toujours en poste au {{ formatDate(referral()?.eligibleForPaymentAt) }}…"
                    ></textarea>
                  </div>
                  <button
                    type="button"
                    (click)="confirmOpen.set('confirm-eligibility')"
                    [disabled]="busy() || unauthorized"
                    class="rounded-lg bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-500 disabled:opacity-50"
                  >
                    Confirmer et transmettre à la compta
                  </button>
                </div>
              }

              @if (mode() === 'reject' && canReject()) {
                <div class="space-y-4">
                  <div>
                    <label class="block text-xs font-medium uppercase tracking-wide text-muted mb-1.5">
                      Commentaire (facultatif)
                    </label>
                    <textarea
                      class="w-full min-h-[90px] rounded-lg border border-default bg-input/40 px-3 py-2 text-sm text-primary focus:outline-none focus:ring-1 focus:ring-soft-blue"
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
                <p class="text-xs text-muted">
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
        <button type="button" class="absolute inset-0 bg-app/80 backdrop-blur-sm" aria-label="Fermer" (click)="confirmOpen.set(null)"></button>
        <div class="relative card-navy max-w-md w-full p-6 shadow-2xl border border-default">
          <div class="flex items-start justify-between gap-4">
            <h3 class="text-lg font-semibold text-primary">Marquer comme traité</h3>
            <button type="button" class="rounded-lg p-1 text-muted hover:text-primary hover:bg-input" (click)="confirmOpen.set(null)" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <p class="mt-3 text-sm text-muted leading-relaxed">
            Candidat : {{ referral()?.candidateName ?? '' }}.
            Le dossier passera en « Dossier traité » en attente de l'entrée du candidat.
          </p>
          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button type="button" (click)="confirmOpen.set(null)" class="rounded-lg border border-default px-4 py-2 text-sm text-primary hover:bg-input/80" [disabled]="busy()">
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
        <button type="button" class="absolute inset-0 bg-app/80 backdrop-blur-sm" aria-label="Fermer" (click)="confirmOpen.set(null)"></button>
        <div class="relative card-navy max-w-md w-full p-6 shadow-2xl border border-default">
          <div class="flex items-start justify-between gap-4">
            <h3 class="text-lg font-semibold text-primary">Confirmer la validation</h3>
            <button type="button" class="rounded-lg p-1 text-muted hover:text-primary hover:bg-input" (click)="confirmOpen.set(null)" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <p class="mt-3 text-sm text-muted leading-relaxed">
            Candidat : {{ referral()?.candidateName ?? '' }}.
            Montant engagé {{ rewardAmount || '—' }} DH — entrée le {{ candidateStartDate || '—' }}.
            Le versement interviendra après la période minimum.
          </p>
          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button type="button" (click)="confirmOpen.set(null)" class="rounded-lg border border-default px-4 py-2 text-sm text-primary hover:bg-input/80" [disabled]="busy()">
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

    @if (confirmOpen() === 'confirm-eligibility') {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <button type="button" class="absolute inset-0 bg-app/80 backdrop-blur-sm" aria-label="Fermer" (click)="confirmOpen.set(null)"></button>
        <div class="relative card-navy max-w-md w-full p-6 shadow-2xl border border-default">
          <div class="flex items-start justify-between gap-4">
            <h3 class="text-lg font-semibold text-primary">Confirmer l'éligibilité</h3>
            <button type="button" class="rounded-lg p-1 text-muted hover:text-primary hover:bg-input" (click)="confirmOpen.set(null)" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <p class="mt-3 text-sm text-muted leading-relaxed">
            Candidat : {{ referral()?.candidateName ?? '' }}.
            Vous confirmez que le candidat est toujours en poste et que la prime peut être transmise à la comptabilité ({{ referral()?.rewardAmount ?? 0 }} DH).
          </p>
          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button type="button" (click)="confirmOpen.set(null)" class="rounded-lg border border-default px-4 py-2 text-sm text-primary hover:bg-input/80" [disabled]="busy()">
              Annuler
            </button>
            <button type="button" (click)="handleConfirm()" class="rounded-lg px-4 py-2 text-sm font-medium bg-amber-600 hover:bg-amber-500 text-white" [disabled]="busy()">
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
        <button type="button" class="absolute inset-0 bg-app/80 backdrop-blur-sm" aria-label="Fermer" (click)="confirmOpen.set(null)"></button>
        <div class="relative card-navy max-w-md w-full p-6 shadow-2xl border border-default">
          <div class="flex items-start justify-between gap-4">
            <h3 class="text-lg font-semibold text-primary">Confirmer le refus</h3>
            <button type="button" class="rounded-lg p-1 text-muted hover:text-primary hover:bg-input" (click)="confirmOpen.set(null)" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <p class="mt-3 text-sm text-muted leading-relaxed">Candidat : {{ referral()?.candidateName ?? '' }}. Le statut passera à « Rejeté ».</p>
          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button type="button" (click)="confirmOpen.set(null)" class="rounded-lg border border-default px-4 py-2 text-sm text-primary hover:bg-input/80" [disabled]="busy()">
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
  readonly mode = signal<'none' | 'process' | 'approve' | 'confirm-eligibility' | 'reject'>('none');
  candidateStartDate = new Date().toISOString().slice(0, 10);
  rewardAmount = '';
  approveComment = '';
  processComment = '';
  rejectComment = '';
  eligibilityComment = '';
  readonly busy = signal(false);
  readonly confirmOpen = signal<null | 'process' | 'approve' | 'confirm-eligibility' | 'reject'>(null);
  readonly toast = signal<{ show: boolean; type: ToastType; message: string }>({ show: false, type: 'success', message: '' });

  get id(): string {
    return this.nav.selectedReferralId() ?? '';
  }

  get unauthorized(): boolean {
    return this.role.user().role !== 'RH';
  }

  readonly canProcess = computed(() => this.referral()?.status === 'SUBMITTED');
  readonly canApprove = computed(() => this.referral()?.status === 'PROCESSED');
  readonly canConfirmEligibility = computed(
    () => this.referral()?.status === 'APPROVED' && this.referral()?.paymentStatus === 'AWAITING_RH',
  );
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

  readonly suggestedMinDuration = computed(() => {
    if (!this.id) return 6;
    return this.referralService.getSuggestedMinDuration(this.id);
  });

  readonly ruleLabel = computed(() => {
    if (!this.id) return '';
    return this.referralService.getRuleLabelForReferral(this.id);
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

  handleConfirmEligibilityClick(): void {
    this.eligibilityComment = '';
    this.mode.set('confirm-eligibility');
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
      if (this.confirmOpen() === 'confirm-eligibility') {
        const updated = await this.referralService.confirmPaymentEligibility(
          id,
          this.eligibilityComment || undefined,
          this.actor(),
        );
        if (!updated) throw new Error('Échec de la confirmation.');
        this.showToast('success', 'Éligibilité confirmée — dossier transmis à la comptabilité.');
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
