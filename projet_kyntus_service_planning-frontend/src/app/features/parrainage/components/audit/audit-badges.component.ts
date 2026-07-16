import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import type { SeverityLevel } from '../../audit/audit-types';

const SEVERITY_MAP: Record<SeverityLevel, string> = {
  INFO: 'bg-input text-primary border-default',
  WARNING: 'bg-[var(--warning-bg)] text-[var(--warning-text)] border-[var(--warning-border)]',
  CRITICAL: 'bg-[var(--danger-bg)] text-[var(--danger-text)] border-[var(--danger-border)]',
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
    if (this.action === 'Validation') return 'bg-[var(--success-bg)] text-[var(--success-text)] border-[var(--success-border)]';
    if (this.action === 'Modification') return 'bg-[var(--warning-bg)] text-[var(--warning-text)] border-[var(--warning-border)]';
    if (this.action === 'Suppression') return 'bg-[var(--danger-bg)] text-[var(--danger-text)] border-[var(--danger-border)]';
    if (this.action === 'Création') return 'bg-[var(--info-bg)] text-[var(--info-text)] border-[var(--info-border)]';
    return 'bg-input text-primary border-default';
  }
}
