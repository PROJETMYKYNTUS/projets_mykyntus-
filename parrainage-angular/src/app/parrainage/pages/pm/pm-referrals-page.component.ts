import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ReferralTableComponent } from '../../components/referral-table.component';
import { ParrainageStoreService } from '../../services/parrainage-store.service';
import { ParrainageRoleService } from '../../state/parrainage-role.service';
import { HierarchyDrillService } from '../../state/hierarchy-drill.service';
import { getScopedReferrals } from '../../lib/scoping';

@Component({
  selector: 'app-pm-referrals-page',
  standalone: true,
  imports: [ReferralTableComponent],
  template: `
    <div class="space-y-4">
      <h2 class="text-lg font-semibold text-slate-50">Parrainages (équipe)</h2>
      <app-referral-table [referrals]="scoped()" scope="pm" [showActions]="false" />
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PmReferralsPageComponent {
  private readonly store = inject(ParrainageStoreService);
  private readonly role = inject(ParrainageRoleService);
  private readonly drill = inject(HierarchyDrillService);
  readonly scoped = computed(() =>
    getScopedReferrals(this.store.referrals(), this.role.user(), this.drill.drill()),
  );
}
