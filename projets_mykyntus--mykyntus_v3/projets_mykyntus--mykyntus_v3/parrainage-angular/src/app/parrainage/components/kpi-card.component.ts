import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Activity } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

@Component({
  selector: 'app-kpi-card',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="card-navy p-4 md:p-5 flex flex-col gap-2">
      <div class="flex items-center justify-between">
        <p class="text-xs uppercase tracking-wide text-slate-500">{{ label }}</p>
        <span [class]="'inline-flex h-8 w-8 items-center justify-center rounded-full border ' + accentClass">
          <app-lucide-icon [icon]="activityIcon" className="h-4 w-4 text-soft-blue" />
        </span>
      </div>
      <p class="text-xl md:text-2xl font-semibold text-slate-50">{{ value }}</p>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiCardComponent {
  @Input({ required: true }) label!: string;
  @Input({ required: true }) value!: string | number;
  @Input() accent: 'blue' | 'green' | 'yellow' | 'red' = 'blue';

  readonly activityIcon = Activity;

  get accentClass(): string {
    switch (this.accent) {
      case 'green':
        return 'border-emerald-500/40 bg-emerald-500/5';
      case 'yellow':
        return 'border-yellow-500/40 bg-yellow-500/5';
      case 'red':
        return 'border-red-500/40 bg-red-500/5';
      default:
        return 'border-soft-blue/40 bg-soft-blue/5';
    }
  }
}
