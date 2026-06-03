import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import type { Referral } from '../models/referral.model';

interface MonthBar {
  label: string;
  count: number;
  pct: number;
}

@Component({
  selector: 'app-monthly-chart',
  standalone: true,
  template: `
    <div class="card-navy p-4 md:p-5">
      <p class="text-xs uppercase tracking-wide text-slate-500 mb-4">Évolution mensuelle des parrainages</p>
      <div class="flex items-end gap-3" style="height: 120px">
        @for (d of data; track d.label) {
          <div class="flex-1 flex flex-col items-center h-full">
            <div class="flex-1 w-full flex flex-col justify-end">
              <div class="w-full bg-soft-blue/60 rounded-t transition-all" [style.height.%]="d.pct < 8 ? 8 : d.pct"></div>
            </div>
            <span class="text-[10px] text-slate-500 mt-1">{{ d.label }}</span>
            <span class="text-xs font-medium text-slate-300">{{ d.count }}</span>
          </div>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonthlyChartComponent {
  data: MonthBar[] = [];

  @Input({ required: true }) set referrals(list: Referral[]) {
    this.data = this.compute(list);
  }

  private compute(referrals: Referral[]): MonthBar[] {
    const now = new Date();
    const months = Array.from({ length: 6 }, (_, idx) => {
      const i = 5 - idx;
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      return {
        label: d.toLocaleDateString('fr-FR', { month: 'short', year: '2-digit' }),
        count: 0,
        year: d.getFullYear(),
        month: d.getMonth(),
      };
    });
    for (const r of referrals) {
      const d = new Date(r.createdAt);
      const m = months.find((x) => x.year === d.getFullYear() && x.month === d.getMonth());
      if (m) m.count++;
    }
    const max = Math.max(1, ...months.map((m) => m.count));
    return months.map((m) => ({ label: m.label, count: m.count, pct: max > 0 ? (m.count / max) * 100 : 0 }));
  }
}
