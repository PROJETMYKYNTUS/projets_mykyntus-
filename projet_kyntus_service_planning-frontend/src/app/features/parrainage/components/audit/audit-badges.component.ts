import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import type { SeverityLevel } from '../../audit/audit-types';

const SEVERITY_MAP: Record<SeverityLevel, string> = {
  INFO: 'bg-slate-600/30 text-primary border-slate-500/40',
  WARNING: 'bg-amber-500/15 text-amber-300 border-amber-500/40',
  CRITICAL: 'bg-rose-600/25 text-rose-200 border-rose-500/50',
};

@Component({
  selector: 'app-severity-badge',
  standalone: true,
  template: `
    <span [class]="'inline-flex px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide rounded border ' + cls">{{ level }}</span>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeverityBadgeComponent {
  @Input({ required: true }) level!: SeverityLevel;
  get cls(): string {
    return SEVERITY_MAP[this.level];
  }
}

@Component({
  selector: 'app-action-nature-badge',
  standalone: true,
  template: `
    <span [class]="'inline-flex px-2 py-0.5 text-xs rounded-md border ' + cls" [title]="action">{{ action }}</span>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActionNatureBadgeComponent {
  @Input({ required: true }) action!: string;
  get cls(): string {
    if (this.action === 'Validation') return 'bg-emerald-500/15 text-emerald-300 border-emerald-500/35';
    if (this.action === 'Modification') return 'bg-amber-500/15 text-amber-300 border-amber-500/35';
    if (this.action === 'Suppression') return 'bg-rose-500/15 text-rose-300 border-rose-500/40';
    if (this.action === 'Création') return 'bg-blue-500/15 text-blue-300 border-blue-500/35';
    return 'bg-slate-500/15 text-primary border-slate-600/40';
  }
}
