import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Clock, Check, X } from 'lucide';
import type { IconNode } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

export interface TimelineItem {
  id: string;
  label: string;
  date?: string;
  status: 'done' | 'pending' | 'rejected';
}

@Component({
  selector: 'app-timeline',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <ol class="relative border-l border-default pl-4 space-y-4">
      @for (item of items; track item.id; let last = $last) {
        <li [class]="last ? '' : 'pb-2'">
          <div class="absolute -left-[10px] flex h-5 w-5 items-center justify-center rounded-full border bg-input">
            <span [class]="'flex h-5 w-5 items-center justify-center rounded-full border text-[10px] ' + iconColor(item)">
              <app-lucide-icon [icon]="iconFor(item)" className="h-3 w-3" />
            </span>
          </div>
          <div class="ml-4 space-y-1">
            <p class="text-xs font-semibold text-primary">{{ item.label }}</p>
            @if (item.date) {
              <p class="text-[11px] text-muted flex items-center gap-1">
                <app-lucide-icon [icon]="clockIcon" className="h-3 w-3" />
                {{ item.date }}
              </p>
            }
          </div>
        </li>
      }
    </ol>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimelineComponent {
  @Input({ required: true }) items: TimelineItem[] = [];

  readonly clockIcon = Clock;

  private isReject(item: TimelineItem): boolean {
    const l = item.label.toLowerCase();
    return l.includes('refus') || l.includes('rejet');
  }

  iconFor(item: TimelineItem): IconNode {
    if (this.isReject(item)) return X;
    return item.status === 'done' ? Check : Clock;
  }

  iconColor(item: TimelineItem): string {
    if (this.isReject(item)) return 'text-[var(--danger-text)] bg-[var(--danger-bg)] border-[var(--danger-border)]';
    return item.status === 'done'
      ? 'text-[var(--success-text)] bg-[var(--success-bg)] border-[var(--success-border)]'
      : 'text-[var(--warning-text)] bg-[var(--warning-bg)] border-[var(--warning-border)]';
  }
}
