import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

export type KyntusStatusPreset = 'parrainage' | 'documentation' | 'prime' | 'generic';

/** Tonalités sémantiques du design system (badges .ky-badge--*) */
type KyntusStatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

const PARRAINAGE_TONES: Record<string, KyntusStatusTone> = {
  SUBMITTED: 'warning',
  PROCESSED: 'info',
  APPROVED: 'success',
  REJECTED: 'danger',
  REWARDED: 'info',
};

const PARRAINAGE_LABELS: Record<string, string> = {
  SUBMITTED: 'En attente',
  PROCESSED: 'Consulté',
  APPROVED: 'Validé',
  REJECTED: 'Rejeté',
  REWARDED: 'Prime versée',
};

const DOCUMENTATION_TONES: Record<string, KyntusStatusTone> = {
  Generated: 'success',
  Approved: 'info',
  Pending: 'warning',
  Rejected: 'danger',
  Cancelled: 'neutral',
};

const DOCUMENTATION_LABELS: Record<string, string> = {
  Generated: 'Document généré',
  Approved: 'Approuvé',
  Pending: 'En attente',
  Rejected: 'Rejeté',
  Cancelled: 'Annulé',
};

const PRIME_TONES: Record<string, KyntusStatusTone> = {
  PendingReview: 'warning',
  Pending: 'warning',
  Submitted: 'info',
  InReview: 'info',
  Approved: 'success',
  'RH Approved': 'success',
  LineRejected: 'danger',
  Rejected: 'danger',
  Paid: 'info',
  Complete: 'success',
  Draft: 'neutral',
};

const PRIME_LABELS: Record<string, string> = {
  PendingReview: 'En attente',
  Pending: 'En attente',
  Submitted: 'Soumise',
  InReview: 'En revue',
  Approved: 'Validée',
  'RH Approved': 'Validée RH',
  LineRejected: 'Rejetée',
  Rejected: 'Rejetée',
  Paid: 'Versée',
  Complete: 'Complète',
  Draft: 'Brouillon',
};

@Component({
  selector: 'app-kyntus-status-badge',
  standalone: true,
  template: `<span [class]="classes">{{ displayLabel }}</span>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusStatusBadgeComponent {
  @Input({ required: true }) status!: string;
  @Input() preset: KyntusStatusPreset = 'generic';
  @Input() label?: string;

  get displayLabel(): string {
    if (this.label) return this.label;
    if (this.preset === 'parrainage') return PARRAINAGE_LABELS[this.status] ?? this.status;
    if (this.preset === 'documentation') return DOCUMENTATION_LABELS[this.status] ?? this.status;
    if (this.preset === 'prime') return PRIME_LABELS[this.status] ?? this.status;
    return this.status;
  }

  get classes(): string {
    let tone: KyntusStatusTone = 'neutral';
    if (this.preset === 'parrainage') tone = PARRAINAGE_TONES[this.status] ?? tone;
    if (this.preset === 'documentation') tone = DOCUMENTATION_TONES[this.status] ?? tone;
    if (this.preset === 'prime') tone = PRIME_TONES[this.status] ?? tone;
    return `ky-badge ky-badge--${tone} whitespace-nowrap`;
  }
}
