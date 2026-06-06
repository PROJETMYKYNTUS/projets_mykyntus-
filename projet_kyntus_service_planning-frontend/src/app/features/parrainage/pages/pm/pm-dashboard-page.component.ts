import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { KpiStatsComponent, KpiStatItem } from '../../components/kpi-stats.component';
import { ParrainageStoreService } from '../../services/parrainage-store.service';
import { ParrainageRoleService } from '../../state/parrainage-role.service';
import { HierarchyDrillService } from '../../state/hierarchy-drill.service';
import { getScopedReferrals } from '../../lib/scoping';

@Component({
  selector: 'app-pm-dashboard-page',
  standalone: true,
  imports: [KpiStatsComponent],
  template: `
    <section class="flex-1 space-y-6">
      <p class="text-sm text-muted max-w-3xl">
        Vue d'ensemble des parrainages de votre équipe.
      </p>

      <app-kpi-stats [items]="items()" />
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PmDashboardPageComponent {
  private readonly store = inject(ParrainageStoreService);
  private readonly role = inject(ParrainageRoleService);
  private readonly drill = inject(HierarchyDrillService);

  readonly scoped = computed(() =>
    getScopedReferrals(this.store.referrals(), this.role.user(), this.drill.drill()),
  );

  readonly items = computed((): KpiStatItem[] => {
    const s = this.scoped();
    const total = s.length;
    const validated = s.filter((r) => r.status === 'APPROVED' || r.status === 'REWARDED').length;
    const successRate = total > 0 ? Math.round((validated / total) * 100) : 0;
    return [
      { label: 'Parrainages équipe', value: total },
      { label: 'Validés', value: validated, accent: 'green' },
      { label: 'Taux de succès', value: `${successRate}%`, accent: 'blue' },
    ];
  });
}
