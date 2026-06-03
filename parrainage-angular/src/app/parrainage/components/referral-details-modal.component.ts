import { ChangeDetectionStrategy, Component, Input, Output, EventEmitter } from '@angular/core';
import { X } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { StatusBadgeComponent } from './status-badge.component';
import type { Referral } from '../models/referral.model';

@Component({
  selector: 'app-referral-details-modal',
  standalone: true,
  imports: [LucideIconComponent, StatusBadgeComponent],
  template: `
    @if (open && referral) {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <button type="button" class="absolute inset-0 bg-navy-950/80 backdrop-blur-sm" aria-label="Fermer" (click)="close.emit()"></button>
        <div class="relative card-navy max-w-2xl w-full max-h-[90vh] overflow-y-auto shadow-2xl border border-navy-800">
          <div class="sticky top-0 flex items-start justify-between gap-4 p-4 border-b border-navy-800 bg-navy-900">
            <h3 class="text-lg font-semibold text-slate-50">Détails du parrainage</h3>
            <button type="button" class="rounded-lg p-1.5 text-slate-500 hover:text-slate-200 hover:bg-navy-800" (click)="close.emit()" aria-label="Fermer">
              <app-lucide-icon [icon]="xIcon" className="h-5 w-5" />
            </button>
          </div>
          <div class="p-4 md:p-6 space-y-6">
            <div class="flex flex-wrap items-center gap-3">
              <span class="text-xs text-slate-500 font-mono">{{ referral.id }}</span>
              <app-status-badge [status]="referral.status" />
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div class="space-y-2">
                <h4 class="text-xs font-bold text-slate-400 uppercase tracking-wider">Candidat</h4>
                <div class="space-y-1 text-sm">
                  <p><span class="text-slate-500">Nom:</span> <span class="text-slate-200">{{ referral.candidateName }}</span></p>
                  <p><span class="text-slate-500">E-mail :</span> <span class="text-slate-200 break-all">{{ referral.candidateEmail }}</span></p>
                  <p><span class="text-slate-500">Tél:</span> <span class="text-slate-200">{{ referral.candidatePhone }}</span></p>
                  <p><span class="text-slate-500">Poste:</span> <span class="text-slate-200">{{ referral.position }}</span></p>
                </div>
              </div>
              <div class="space-y-2">
                <h4 class="text-xs font-bold text-slate-400 uppercase tracking-wider">Parrain</h4>
                <div class="space-y-1 text-sm">
                  <p><span class="text-slate-500">Nom:</span> <span class="text-slate-200">{{ referral.referrerName }}</span></p>
                  <p><span class="text-slate-500">Projet:</span> <span class="text-slate-200">{{ referral.projectName }}</span></p>
                  <p><span class="text-slate-500">Équipe:</span> <span class="text-slate-200">{{ referral.teamId }}</span></p>
                  @if (referral.rewardAmount > 0) {
                    <p><span class="text-slate-500">Prime:</span> <span class="text-slate-200">{{ referral.rewardAmount }} DH</span></p>
                  }
                </div>
              </div>
            </div>
            <div class="text-sm text-slate-500">Date de soumission: {{ submissionDate }}</div>
            @if (showCommentField) {
              <div class="space-y-2">
                <label class="block text-xs font-bold text-slate-400 uppercase tracking-wider">Commentaire (non enregistré)</label>
                <textarea
                  [value]="comment"
                  (input)="comment = $any($event.target).value"
                  placeholder="Ajouter un commentaire…"
                  class="w-full min-h-[80px] rounded-lg border border-navy-800 bg-navy-900 px-4 py-2.5 text-sm text-slate-100 placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50 transition-colors"
                  rows="3"
                ></textarea>
              </div>
            }
          </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReferralDetailsModalComponent {
  @Input() referral: Referral | null = null;
  @Input() open = false;
  @Input() showCommentField = false;
  @Output() close = new EventEmitter<void>();

  comment = '';
  readonly xIcon = X;

  get submissionDate(): string {
    if (!this.referral) return '';
    return new Date(this.referral.createdAt).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' });
  }
}
