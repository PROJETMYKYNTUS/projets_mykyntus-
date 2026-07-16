import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import type { IconNode } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

export interface KpiStatItem {
  label: string;
  value: string | number;
  accent?: 'blue' | 'green' | 'yellow' | 'red' | 'purple' | 'orange';
  icon?: IconNode;
}

@Component({
  selector: 'app-kpi-stats',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 xl:grid-cols-5 gap-6">
      @for (item of items; track item.label) {
        <div class="card-navy p-6 flex items-center justify-between">
          <div>
            <p class="text-sm text-muted font-medium mb-1">{{ item.label }}</p>
            <h3 class="text-3xl font-bold text-primary">{{ item.value }}</h3>
          </div>
          @if (item.icon) {
            <div [class]="'w-12 h-12 ' + accentClass(item.accent) + ' rounded-xl flex items-center justify-center shrink-0'">
              <app-lucide-icon [icon]="item.icon" className="w-6 h-6" />
            </div>
          }
        </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiStatsComponent {
  @Input({ required: true }) items: KpiStatItem[] = [];

  accentClass(accent?: string): string {
    switch (accent) {
      case 'green':
        return 'text-[var(--success-text)] bg-[var(--success-bg)]';
      case 'yellow':
      case 'orange':
        return 'text-[var(--warning-text)] bg-[var(--warning-bg)]';
      case 'red':
        return 'text-[var(--danger-text)] bg-[var(--danger-bg)]';
      case 'purple':
        return 'text-[var(--electric-blue)] bg-[var(--info-bg)]';
      default:
        return 'text-[var(--info-text)] bg-[var(--info-bg)]';
    }
  }
}
