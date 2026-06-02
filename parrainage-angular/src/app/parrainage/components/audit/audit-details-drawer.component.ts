import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { X } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { SeverityBadgeComponent } from './audit-badges.component';
import type { JournalRow } from '../../audit/audit-types';

@Component({
  selector: 'app-audit-details-drawer',
  standalone: true,
  imports: [LucideIconComponent, SeverityBadgeComponent],
  template: `
    @if (selected) {
      <div class="fixed inset-0 z-50 flex justify-end bg-navy-950/55">
        <button type="button" class="flex-1 cursor-default" aria-label="Fermer" (click)="close.emit()"></button>
        <div class="w-full max-w-md h-full bg-navy-900 border-l border-navy-800 p-5 space-y-4 overflow-y-auto shadow-2xl">
          <div class="flex items-center justify-between">
            <h4 class="text-lg font-semibold text-white">Détail technique</h4>
            <button type="button" (click)="close.emit()" class="p-1.5 rounded-md hover:bg-navy-800 transition-colors">
              <app-lucide-icon [icon]="xIcon" className="w-4 h-4 text-slate-300" />
            </button>
          </div>
          <div class="text-sm space-y-3">
            <div class="flex items-center gap-2 flex-wrap">
              <app-severity-badge [level]="selected.severity" />
              <span class="text-slate-500 font-mono text-xs">{{ selected.actionCode }}</span>
            </div>
            <div><span class="text-slate-500">Action</span><p class="text-slate-200">{{ selected.action }}</p></div>
            <div><span class="text-slate-500">IP</span><p class="text-slate-200 font-mono text-sm">{{ selected.ip }}</p></div>
            <div><span class="text-slate-500">Device / navigateur</span><p class="text-slate-200">{{ selected.device }}</p></div>
            <div><span class="text-slate-500">Département / Pôle / Cellule</span><p class="text-slate-200">{{ selected.departement }} · {{ selected.pole }} · {{ selected.cellule }}</p></div>
            <div><span class="text-slate-500">Rôle</span><p class="text-slate-200">{{ selected.roleMetier }}</p></div>
            <div><span class="text-slate-500">Élément</span><p class="text-slate-200">{{ selected.item }}</p></div>
            <div>
              <span class="text-slate-500">Avant / après</span>
              <div class="mt-1 grid grid-cols-1 gap-2 sm:grid-cols-2">
                <pre class="p-3 rounded-lg bg-navy-950 border border-navy-800 text-[11px] text-slate-400 overflow-x-auto max-h-48">{{ json(selected.beforeState) }}</pre>
                <pre class="p-3 rounded-lg bg-navy-950 border border-emerald-900/30 text-[11px] text-emerald-100/90 overflow-x-auto max-h-48">{{ json(selected.afterState) }}</pre>
              </div>
            </div>
            <div>
              <span class="text-slate-500">Métadonnées</span>
              <pre class="mt-1 p-3 rounded-lg bg-navy-950 border border-navy-800 text-[11px] text-slate-400 overflow-x-auto">{{ json(selected.metadata) }}</pre>
            </div>
            <div class="flex flex-col gap-2 pt-2">
              <button type="button" (click)="openUserTimeline.emit()" class="w-full py-2.5 rounded-lg border border-blue-500/40 bg-blue-600/15 text-blue-200 text-sm hover:bg-blue-600/25 transition-colors">
                Voir toutes les actions de cet utilisateur (timeline)
              </button>
              <button type="button" (click)="investigateUser.emit()" class="w-full py-2.5 rounded-lg border border-amber-500/40 bg-amber-500/10 text-amber-200 text-sm hover:bg-amber-500/20 transition-colors">
                Mode investigation — filtrer le journal sur cet utilisateur
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditDetailsDrawerComponent {
  @Input() selected: JournalRow | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() investigateUser = new EventEmitter<void>();
  @Output() openUserTimeline = new EventEmitter<void>();

  readonly xIcon = X;

  json(v: unknown): string {
    return JSON.stringify(v, null, 2);
  }
}
