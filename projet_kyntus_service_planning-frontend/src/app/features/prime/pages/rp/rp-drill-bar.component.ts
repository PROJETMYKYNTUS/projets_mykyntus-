import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { KyntusSelectSyncDirective } from '@/shared/directives/kyntus-select-sync.directive';
import { drillSelectOptions } from '../../lib/hierarchyDrillDown';
import { HierarchyDrillService } from '../../state/hierarchy-drill.service';
import { RoleService } from '../../state/role.service';

@Component({
  selector: 'app-rp-drill-bar',
  standalone: true,
  imports: [KyntusSelectSyncDirective],
  template: `
    <div class="flex flex-wrap items-center gap-2">
      <select
        class="text-sm pl-3 pr-8 py-2 rounded-lg border border-default bg-input/80 text-primary focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        [kyntusSelectSync]="hierarchy.drill().managerId ?? ''"
        (kyntusSelectSyncChange)="onManager($event)"
      >
        <option value="">Manager</option>
        @for (o of managers(); track o.value) {
          <option [value]="o.value">{{ o.label }}</option>
        }
      </select>
      <select
        class="text-sm pl-3 pr-8 py-2 rounded-lg border border-default bg-input/80 text-primary focus:ring-2 focus:ring-blue-500 focus:border-blue-500 disabled:opacity-50"
        [disabled]="!hierarchy.drill().managerId"
        [kyntusSelectSync]="hierarchy.drill().coachId ?? ''"
        (kyntusSelectSyncChange)="hierarchy.setCoachId($event || undefined)"
      >
        <option value="">Coach</option>
        @for (o of coaches(); track o.value) {
          <option [value]="o.value">{{ o.label }}</option>
        }
      </select>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RpDrillBarComponent {
  readonly rpUserId = input.required<string>();
  readonly hierarchy = inject(HierarchyDrillService);
  private readonly roles = inject(RoleService);

  readonly managers = computed(() =>
    drillSelectOptions(this.roles.employees(), 'RP', this.rpUserId(), this.hierarchy.drill()).managers,
  );

  readonly coaches = computed(() =>
    drillSelectOptions(this.roles.employees(), 'RP', this.rpUserId(), this.hierarchy.drill()).coaches,
  );

  onManager(v: string): void {
    this.hierarchy.setManagerId(v || undefined);
  }
}
