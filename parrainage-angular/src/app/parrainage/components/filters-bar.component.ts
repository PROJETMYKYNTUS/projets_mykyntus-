import { ChangeDetectionStrategy, Component, Input, Output, EventEmitter } from '@angular/core';
import { Filter } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

@Component({
  selector: 'app-filters-bar',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <div class="card-navy mb-4 p-3 md:p-4 flex flex-col md:flex-row md:items-center md:justify-between gap-3">
      <div class="flex items-center gap-2 text-xs text-slate-400">
        <app-lucide-icon [icon]="filterIcon" className="h-4 w-4 text-soft-blue" />
        <span class="font-medium">Filtres</span>
      </div>
      <div class="flex flex-wrap gap-3">
        <div class="flex flex-col gap-1">
          <label class="text-[11px] text-slate-500">Statut</label>
          <select
            class="rounded-lg border border-navy-800 bg-navy-900 px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50 min-w-[120px]"
            [value]="status"
            (change)="statusChange.emit($any($event.target).value)"
          >
            <option value="all">Tous</option>
            <option value="SUBMITTED">En attente</option>
            <option value="APPROVED">Validé</option>
            <option value="REJECTED">Rejeté</option>
            <option value="REWARDED">Prime versée</option>
          </select>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-[11px] text-slate-500">Période</label>
          <select
            class="rounded-lg border border-navy-800 bg-navy-900 px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50 min-w-[140px]"
            [value]="dateRange"
            (change)="dateRangeChange.emit($any($event.target).value)"
          >
            <option value="3m">3 derniers mois</option>
            <option value="6m">6 derniers mois</option>
            <option value="12m">12 derniers mois</option>
            <option value="all">Depuis le début</option>
          </select>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FiltersBarComponent {
  @Input() status = 'all';
  @Input() dateRange = '6m';
  @Output() statusChange = new EventEmitter<string>();
  @Output() dateRangeChange = new EventEmitter<string>();

  readonly filterIcon = Filter;
}
