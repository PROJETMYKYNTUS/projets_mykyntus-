import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { X } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { BodyPortalDirective } from '@/shared/directives/body-portal.directive';
import type { JournalRow } from '../../audit/audit-types';

interface TimelineItem {
  id: string;
  action: string;
  item: string;
  datetime: string;
}

@Component({
  selector: 'app-user-timeline-modal',
  standalone: true,
  imports: [LucideIconComponent, BodyPortalDirective],
  template: `
    @if (open) {
      <div class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-app/70" appBodyPortal (click)="close.emit()" role="presentation">
        <div class="card-navy w-full max-w-lg max-h-[85vh] flex flex-col border border-default shadow-xl duration-200" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
          <div class="flex items-center justify-between p-4 border-b border-default">
            <div>
              <h3 class="text-lg font-semibold text-primary">Timeline — {{ userLabel }}</h3>
              <p class="text-xs text-muted">{{ items.length }} événement(s)</p>
            </div>
            <button type="button" (click)="close.emit()" class="p-2 rounded-lg hover:bg-input transition-colors">
              <app-lucide-icon [icon]="xIcon" className="w-5 h-5 text-muted" />
            </button>
          </div>
          <div class="p-4 overflow-y-auto">
            @if (items.length === 0) {
              <p class="text-sm text-muted">Aucune action pour cet utilisateur sur la période chargée.</p>
            } @else {
              <div class="max-h-72 overflow-y-auto pr-1 space-y-4">
                @for (it of items; track it.id) {
                  <div class="relative pl-8">
                    <span class="absolute left-[11px] top-0 bottom-0 w-px bg-[var(--border-color)]"></span>
                    <span class="absolute left-0 top-1.5 w-[22px] h-[22px] rounded-full bg-input border border-default"></span>
                    <div class="flex items-start justify-between gap-3">
                      <div class="space-y-1">
                        <span [class]="'inline-flex text-[11px] px-2 py-0.5 rounded-md border ' + badgeClass(it.action)">{{ it.action }}</span>
                        <p class="text-sm text-primary">{{ it.item }}</p>
                      </div>
                      <span class="text-xs text-muted whitespace-nowrap">{{ it.datetime }}</span>
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserTimelineModalComponent {
  @Input() userLabel = '';
  @Input() open = false;
  @Input({ required: true }) rows: JournalRow[] = [];
  @Output() close = new EventEmitter<void>();

  readonly xIcon = X;

  get items(): TimelineItem[] {
    return [...this.rows]
      .filter((r) => r.employee === this.userLabel)
      .sort((a, b) => b.datetime.localeCompare(a.datetime))
      .map((r) => ({ id: r.id, action: `${r.action} · ${r.actionCode}`, item: r.item, datetime: r.datetime }));
  }

  badgeClass(action: string): string {
    const a = action.toLowerCase();
    if (a.includes('soumis')) return 'bg-[var(--info-bg)] text-[var(--info-text)] border-[var(--info-border)]';
    if (a.includes('vers')) return 'bg-[var(--info-bg)] text-[var(--electric-blue)] border-[var(--info-border)]';
    if (a.includes('valid')) return 'bg-[var(--success-bg)] text-[var(--success-text)] border-[var(--success-border)]';
    return 'bg-input text-primary border-default';
  }
}
