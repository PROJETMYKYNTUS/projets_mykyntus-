import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { KyntusAuditDrawerComponent, type KyntusAuditField } from './kyntus-audit-drawer.component';
import { KyntusPageHeaderComponent } from './kyntus-page-header.component';

@Component({
  selector: 'app-kyntus-audit-log-page',
  standalone: true,
  imports: [KyntusPageHeaderComponent, KyntusAuditDrawerComponent],
  template: `
    <section class="kyntus-audit-log-page">
      <app-kyntus-page-header [title]="title" [subtitle]="subtitle" [eyebrow]="eyebrow">
        <ng-content select="[headerActions]" actions />
      </app-kyntus-page-header>

      <div class="kyntus-audit-filters">
        <ng-content select="[filters]" />
      </div>

      <div class="kyntus-audit-table">
        <ng-content select="[table]" />
      </div>

      <ng-content select="[footer]" />

      <app-kyntus-audit-drawer
        [open]="drawerOpen"
        [title]="drawerTitle"
        [fields]="drawerFields"
        (close)="drawerClose.emit()"
      >
        <ng-content select="[drawerExtra]" />
        <div actions>
          <ng-content select="[drawerActions]" />
        </div>
      </app-kyntus-audit-drawer>
    </section>
  `,
  styles: [`
    .kyntus-audit-log-page {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }
    .kyntus-audit-filters:empty,
    .kyntus-audit-table:empty {
      display: none;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KyntusAuditLogPageComponent {
  @Input({ required: true }) title!: string;
  @Input() subtitle = '';
  @Input() eyebrow = '';
  @Input() drawerOpen = false;
  @Input() drawerTitle = 'Détail';
  @Input() drawerFields: KyntusAuditField[] = [];
  @Output() drawerClose = new EventEmitter<void>();
}
