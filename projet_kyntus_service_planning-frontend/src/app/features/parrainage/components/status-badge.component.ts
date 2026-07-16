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
    return cn(REFERRAL_STATUS_STYLES[this.status]);
  }
}
