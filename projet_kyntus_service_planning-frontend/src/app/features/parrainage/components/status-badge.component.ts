import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import type { ReferralStatus } from '../models/referral.model';
import { cn } from '@/lib/utils';
import { REFERRAL_STATUS_LABELS, REFERRAL_STATUS_STYLES } from '../utils/referral-status.util';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `
    <span [class]="classes">{{ REFERRAL_STATUS_LABELS[status] }}</span>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: ReferralStatus;
  readonly REFERRAL_STATUS_LABELS = REFERRAL_STATUS_LABELS;

  get classes(): string {
    return cn(
      'inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-semibold',
      REFERRAL_STATUS_STYLES[this.status],
    );
  }
}
