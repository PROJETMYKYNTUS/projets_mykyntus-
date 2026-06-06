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
            <h3 class="text-3xl font-bold text-white">{{ item.value }}</h3>
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
        return 'text-emerald-500 bg-emerald-500/10';
      case 'yellow':
      case 'orange':
        return 'text-amber-500 bg-amber-500/10';
      case 'red':
        return 'text-red-500 bg-red-500/10';
      case 'purple':
        return 'text-indigo-500 bg-indigo-500/10';
      default:
        return 'text-blue-500 bg-blue-500/10';
    }
  }
}
