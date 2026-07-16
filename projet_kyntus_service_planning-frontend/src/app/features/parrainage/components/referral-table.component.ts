import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { Router } from '@angular/router';
import { FileText } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import type { Referral } from '../models/referral.model';
import { StatusBadgeComponent } from './status-badge.component';
import { ParrainageNavService } from '../state/parrainage-nav.service';

@Component({
  selector: 'app-referral-table',
  standalone: true,
  imports: [DatePipe, LucideIconComponent, StatusBadgeComponent],
  template: `
    @if (loading) {
      <div class="card-navy p-10 text-center text-muted text-sm">Chargement…</div>
    } @else if (referrals.length === 0) {
      <div class="card-navy p-12 text-center">
        <div class="w-16 h-16 bg-card rounded-full flex items-center justify-center mx-auto mb-4">
          <app-lucide-icon [icon]="fileTextIcon" className="w-8 h-8 text-muted" />
        </div>
        <h4 class="text-primary font-medium">Aucun parrainage</h4>
        <p class="text-muted text-sm mt-1">Aucune donnée à afficher</p>
      </div>
    } @else {
      <div class="card-navy overflow-hidden">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-card/50 border-b border-default">
              @if (scope === 'admin') {
                <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider">ID</th>
              }
              <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider">Parrain</th>
              <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider">Candidat</th>
              @if (scope === 'admin') {
                <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider">Projet</th>
              }
              <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider">Statut</th>
              @if (scope === 'admin') {
                <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider">Prime</th>
              }
              <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider">Date</th>
              @if (showActions) {
                <th class="px-6 py-4 text-[11px] font-bold text-muted uppercase tracking-wider text-right">Actions</th>
              }
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            @for (r of referrals; track r.id) {
              <tr class="hover:bg-input/30 transition-colors">
                @if (scope === 'admin') {
                  <td class="px-6 py-4 text-muted text-xs font-mono">{{ r.id }}</td>
                }
                <td class="px-6 py-4 text-sm font-medium text-primary">{{ r.referrerName }}</td>
                <td class="px-6 py-4 text-sm text-muted">{{ r.candidateName }}</td>
                @if (scope === 'admin') {
                  <td class="px-6 py-4 text-sm text-muted">{{ r.projectName }}</td>
                }
                <td class="px-6 py-4"><app-status-badge [status]="r.status" /></td>
                @if (scope === 'admin') {
                  <td class="px-6 py-4 text-sm text-primary">{{ r.rewardAmount > 0 ? r.rewardAmount + ' DH' : '—' }}</td>
                }
                <td class="px-6 py-4 text-sm text-muted">{{ r.createdAt | date: 'dd/MM/yyyy' }}</td>
                @if (showActions) {
                  <td class="px-6 py-4 text-right">
                    <div class="flex items-center justify-end gap-2">
                      <button type="button" (click)="onDetails(r)" class="p-2 text-muted hover:text-[var(--soft-blue)] hover:bg-[var(--info-bg)] rounded-lg text-sm font-medium">Détails</button>
                      @if (enableValidateProcessed && r.status === 'PROCESSED' && !r.candidateEmployeeId) {
                        <button
                          type="button"
                          (click)="navigateToEmployeeForm(r.id)"
                          class="p-2 text-[var(--success-text)] hover:bg-[var(--success-bg)] rounded-lg text-sm font-medium"
                        >
                          Valider
                        </button>
                      }
                      @if (enableApprove && r.status === 'SUBMITTED') {
                        <button type="button" (click)="approve.emit(r)" class="p-2 text-muted hover:text-[var(--success-text)] text-sm">Valider</button>
                      }
                      @if (enableReject && r.status === 'SUBMITTED') {
                        <button type="button" (click)="reject.emit(r)" class="p-2 text-muted hover:text-[var(--danger-text)] text-sm">Rejeter</button>
                      }
                    </div>
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReferralTableComponent {
  private readonly nav = inject(ParrainageNavService);
  private readonly router = inject(Router);
  readonly fileTextIcon = FileText;

  @Input({ required: true }) referrals: Referral[] = [];
  @Input() loading = false;
  @Input() showActions = true;
  @Input() scope: 'admin' | 'pm' = 'admin';
  @Input() enableApprove = false;
  @Input() enableReject = false;
  @Input() enableValidateProcessed = false;
  @Output() approve = new EventEmitter<Referral>();
  @Output() reject = new EventEmitter<Referral>();

  onDetails(r: Referral): void {
    this.nav.openReferralDetails(r.id);
  }

  navigateToEmployeeForm(referralId: string): void {
    void this.router.navigate(['/users/create'], {
      queryParams: { referralId, fromParrainage: '1' },
    });
  }
}
