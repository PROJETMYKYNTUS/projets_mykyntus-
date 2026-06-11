import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { drillSelectOptions } from '../../lib/hierarchyDrillDown';
import { HierarchyDrillService } from '../../state/hierarchy-drill.service';
import { RoleService } from '../../state/role.service';

@Component({
  selector: 'app-rp-drill-bar',
  standalone: true,
  template: `
    <div class="flex flex-wrap items-center gap-2">
      <select
        class="text-sm pl-3 pr-8 py-2 rounded-lg border border-slate-600 bg-slate-900/80 text-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        [value]="hierarchy.drill().managerId ?? ''"
        (change)="onManager($any($event.target).value)"
      >
        <option value="">Manager</option>
        @for (o of managers(); track o.value) {
          <option [value]="o.value">{{ o.label }}</option>
        }
      </select>
      <select
        class="text-sm pl-3 pr-8 py-2 rounded-lg border border-slate-600 bg-slate-900/80 text-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 disabled:opacity-50"
        [disabled]="!hierarchy.drill().managerId"
        [value]="hierarchy.drill().coachId ?? ''"
        (change)="hierarchy.setCoachId($any($event.target).value || undefined)"
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
