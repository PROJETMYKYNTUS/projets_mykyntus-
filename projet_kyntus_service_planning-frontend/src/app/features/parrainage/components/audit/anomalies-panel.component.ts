import { ChangeDetectionStrategy, Component, EventEmitter, Output } from '@angular/core';
import { AlertTriangle, GitBranch } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { SeverityBadgeComponent } from './audit-badges.component';
import type { AnomalyRow } from '../../audit/audit-types';

@Component({
  selector: 'app-anomalies-panel',
  standalone: true,
  imports: [LucideIconComponent, SeverityBadgeComponent],
  template: `
    <div class="space-y-4">
      @if (anomalies.length === 0) {
        <p class="text-sm text-muted">Aucune anomalie détectée pour le moment.</p>
      } @else {
        <div class="grid gap-3">
        @for (a of anomalies; track a.id) {
          <div class="card-navy p-4 border border-rose-900/30 bg-rose-950/10 flex flex-col md:flex-row md:items-center md:justify-between gap-3 hover:border-rose-800/50 transition-colors duration-200">
            <div class="space-y-1">
              <div class="flex items-center gap-2 flex-wrap">
                <app-lucide-icon [icon]="alertIcon" className="w-4 h-4 text-rose-400 shrink-0" />
                <span class="font-semibold text-primary">{{ a.title }}</span>
                <app-severity-badge [level]="a.severityUi" />
                <span [class]="'px-2 py-0.5 text-[10px] font-bold rounded border ' + priorityClass(a.priority)">{{ a.priority }}</span>
                <span class="text-[11px] text-muted">{{ a.category }}</span>
              </div>
              <p class="text-sm text-muted">{{ a.description }}</p>
              <p class="text-[11px] text-muted">Détecté : {{ a.detectedAt }}</p>
            </div>
            <div class="flex flex-col sm:flex-row gap-2 shrink-0">
              @if (a.relatedUserLabel) {
                <button type="button" (click)="openTimeline.emit(a)" class="inline-flex items-center justify-center gap-2 px-4 py-2 rounded-lg border border-default text-primary text-sm hover:bg-input transition-colors">
                  <app-lucide-icon [icon]="gitIcon" className="w-4 h-4" />
                  Timeline
                </button>
              }
              <button type="button" (click)="investigate.emit(a)" class="px-4 py-2 rounded-lg border border-blue-500/40 bg-blue-600/15 text-blue-200 text-sm hover:bg-blue-600/25 transition-colors">
                Investiguer
              </button>
            </div>
          </div>
        }
      </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnomaliesPanelComponent {
  @Output() investigate = new EventEmitter<AnomalyRow>();
  @Output() openTimeline = new EventEmitter<AnomalyRow>();

  readonly anomalies: AnomalyRow[] = [];
  readonly alertIcon = AlertTriangle;
  readonly gitIcon = GitBranch;

  priorityClass(p: AnomalyRow['priority']): string {
    if (p === 'P1') return 'bg-rose-600/25 text-rose-200 border-rose-500/50';
    if (p === 'P2') return 'bg-amber-500/20 text-amber-200 border-amber-500/40';
    return 'bg-slate-600/30 text-primary border-slate-500/40';
  }
}
