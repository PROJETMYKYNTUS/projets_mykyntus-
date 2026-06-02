import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import type { ReferralStatus } from '../models/referral.model';
import { cn } from '@/lib/utils';

const STATUS_STYLES: Record<ReferralStatus, string> = {
  SUBMITTED: 'bg-amber-500/10 text-amber-500 border-amber-500/20',
  PROCESSED: 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20',
  APPROVED: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  REJECTED: 'bg-red-500/10 text-red-500 border-red-500/20',
  REWARDED: 'bg-blue-500/10 text-blue-500 border-blue-500/20',
};

const STATUS_LABELS: Record<ReferralStatus, string> = {
  SUBMITTED: 'En attente',
  PROCESSED: 'Dossier traité',
  APPROVED: 'Validé',
  REJECTED: 'Rejeté',
  REWARDED: 'Prime versée',
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `
    <span [class]="classes">{{ STATUS_LABELS[status] }}</span>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: ReferralStatus;
  readonly STATUS_LABELS = STATUS_LABELS;

  get classes(): string {
    return cn(
      'inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-semibold',
      STATUS_STYLES[this.status],
    );
  }
}
