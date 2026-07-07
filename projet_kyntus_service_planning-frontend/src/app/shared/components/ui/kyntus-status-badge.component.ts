import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { cn } from '@/lib/utils';

export type KyntusStatusPreset = 'parrainage' | 'documentation' | 'prime' | 'generic';

const PARRAINAGE_STYLES: Record<string, string> = {
  SUBMITTED: 'bg-amber-500/10 text-amber-500 border-amber-500/20',
  PROCESSED: 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20',
  APPROVED: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  REJECTED: 'bg-red-500/10 text-red-500 border-red-500/20',
  REWARDED: 'bg-blue-500/10 text-blue-500 border-blue-500/20',
};

const PARRAINAGE_LABELS: Record<string, string> = {
  SUBMITTED: 'En attente',
  PROCESSED: 'Consulté',
  APPROVED: 'Validé',
  REJECTED: 'Rejeté',
  REWARDED: 'Prime versée',
};

const DOCUMENTATION_STYLES: Record<string, string> = {
  Generated: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  Approved: 'bg-blue-500/10 text-blue-500 border-blue-500/20',
  Pending: 'bg-amber-500/10 text-amber-500 border-amber-500/20',
  Rejected: 'bg-red-500/10 text-red-500 border-red-500/20',
  Cancelled: 'bg-slate-500/10 text-muted border-slate-500/25',
};

const DOCUMENTATION_LABELS: Record<string, string> = {
  Generated: 'Document généré',
  Approved: 'Approuvé',
  Pending: 'En attente',
  Rejected: 'Rejeté',
  Cancelled: 'Annulé',
};

const PRIME_STYLES: Record<string, string> = {
  PendingReview: 'bg-amber-500/10 text-amber-400 border-amber-500/20',
  Pending: 'bg-amber-500/10 text-amber-400 border-amber-500/20',
  Submitted: 'bg-blue-500/10 text-blue-400 border-blue-500/20',
  InReview: 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20',
  Approved: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  'RH Approved': 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  LineRejected: 'bg-red-500/10 text-red-500 border-red-500/20',
  Rejected: 'bg-red-500/10 text-red-500 border-red-500/20',
  Paid: 'bg-purple-500/10 text-purple-400 border-purple-500/20',
  Complete: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  Draft: 'bg-slate-500/10 text-muted border-slate-500/25',
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
    const base = 'inline-flex items-center whitespace-nowrap rounded-full border px-2.5 py-0.5 text-[11px] font-semibold';
    let style = 'bg-slate-500/10 text-muted border-slate-500/20';
    if (this.preset === 'parrainage') style = PARRAINAGE_STYLES[this.status] ?? style;
    if (this.preset === 'documentation') style = DOCUMENTATION_STYLES[this.status] ?? style;
    if (this.preset === 'prime') style = PRIME_STYLES[this.status] ?? style;
    return cn(base, style);
  }
}
