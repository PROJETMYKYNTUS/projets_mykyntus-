import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { Eye, Inbox } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { SeverityBadgeComponent, ActionNatureBadgeComponent } from './audit-badges.component';
import type { JournalRow, SortKey } from '../../audit/audit-types';

@Component({
  selector: 'app-audit-table',
  standalone: true,
  imports: [LucideIconComponent, SeverityBadgeComponent, ActionNatureBadgeComponent],
  template: `
    @if (hasNoData) {
      <div class="card-navy p-4 flex items-center gap-3 border border-navy-700/70 bg-navy-900/45">
        <app-lucide-icon [icon]="inboxIcon" className="w-5 h-5 text-slate-400" />
        <div>
          <p class="text-slate-200 text-sm">Aucune donnée disponible</p>
          <p class="text-xs text-slate-500">Affichage de démonstration avec des lignes fictives.</p>
        </div>
      </div>
    }

    <div class="card-navy overflow-x-auto border border-navy-800/80 transition-shadow hover:shadow-lg hover:shadow-navy-950/40">
      <table class="w-full text-sm min-w-[1100px]">
        <thead class="bg-navy-800/55 text-slate-300 font-semibold">
          <tr>
            <th class="px-3 py-3 text-left cursor-pointer whitespace-nowrap" (click)="toggleSort.emit('datetime')">Date / heure {{ arrow('datetime') }}</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('employee')">Utilisateur</th>
            <th class="px-3 py-3 text-left">IP</th>
            <th class="px-3 py-3 text-left">Device</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('severity')">Gravité</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('departement')">Dépt.</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('pole')">Pôle</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('cellule')">Cellule</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('roleMetier')">Rôle</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('action')">Action</th>
            <th class="px-3 py-3 text-left cursor-pointer" (click)="toggleSort.emit('item')">Élément</th>
            <th class="px-3 py-3 text-left">Voir</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-navy-800">
          @for (r of visibleRows; track r.id) {
            <tr class="hover:bg-navy-800/40 transition-colors duration-150">
              <td class="px-3 py-3 text-slate-400 whitespace-nowrap text-xs">{{ r.datetime }}</td>
              <td class="px-3 py-3 text-slate-200">{{ r.employee }}</td>
              <td class="px-3 py-3 text-slate-400 font-mono text-xs" title="Adresse IP source">{{ r.ip }}</td>
              <td class="px-3 py-3 text-slate-500 text-xs max-w-[140px] truncate" [title]="r.device">{{ r.device }}</td>
              <td class="px-3 py-3"><span [title]="'Code: ' + r.actionCode"><app-severity-badge [level]="r.severity" /></span></td>
              <td class="px-3 py-3 text-slate-400 text-xs">{{ r.departement }}</td>
              <td class="px-3 py-3 text-slate-400 text-xs">{{ r.pole }}</td>
              <td class="px-3 py-3 text-slate-400 text-xs">{{ r.cellule }}</td>
              <td class="px-3 py-3 text-slate-200 text-xs">{{ r.roleMetier }}</td>
              <td class="px-3 py-3"><app-action-nature-badge [action]="r.action" /></td>
              <td class="px-3 py-3 text-slate-300 max-w-[180px] truncate" [title]="r.item">{{ r.item }}</td>
              <td class="px-3 py-3">
                <button type="button" (click)="view.emit(r)" class="inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded-md border border-blue-500/30 bg-blue-600/15 hover:bg-blue-500/30 text-blue-200 text-xs transition-all duration-200">
                  <app-lucide-icon [icon]="eyeIcon" className="w-3.5 h-3.5" />
                  Voir
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
      @if (isMockDisplay) {
        <p class="px-4 py-2 text-[11px] text-slate-500 border-t border-navy-800">Mode démo actif (aucune ligne réelle sur ce filtre).</p>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditTableComponent {
  @Input({ required: true }) visibleRows: JournalRow[] = [];
  @Input() hasNoData = false;
  @Input() isMockDisplay = false;
  @Input({ required: true }) sortKey!: SortKey;
  @Input({ required: true }) sortDir: 'asc' | 'desc' = 'desc';
  @Output() toggleSort = new EventEmitter<SortKey>();
  @Output() view = new EventEmitter<JournalRow>();

  readonly eyeIcon = Eye;
  readonly inboxIcon = Inbox;

  arrow(k: SortKey): string {
    return this.sortKey === k ? (this.sortDir === 'asc' ? '↑' : '↓') : '';
  }
}
