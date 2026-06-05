import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import * as echarts from 'echarts/core';
import type { EChartsCoreOption } from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { GridComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { CalendarClock, Clock3, Sparkles, Wallet } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { firstValueFrom } from 'rxjs';
import { PrimeService } from '../../services/prime.service';
import {
  PrimeFicheResultService,
  type EmployeePrimeServiceFicheValidationDto,
} from '../../services/prime-fiche-result.service';
import type { PrimeResult, PrimeType } from '../../models';
import { RoleService } from '../../state/role.service';

echarts.use([LineChart, GridComponent, TooltipComponent, CanvasRenderer]);

@Component({
  selector: 'app-employee-dashboard-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent, NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    @if (loading()) {
      <div class="p-8 text-slate-400">Loading your dashboard...</div>
    } @else {
      <div class="p-8 space-y-6 bg-app min-h-full">
        <div>
          <h1 class="text-3xl font-bold text-slate-100">Welcome back, {{ user().firstName }}</h1>
          <p class="text-slate-400 mt-1">
            Your personal PRIME space - read-only and focused on your results.
          </p>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <app-prime-card className="card-navy">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-slate-400 text-sm">Total primes earned</p>
                <p class="text-2xl font-bold text-emerald-400">{{ totalEarned() }} MAD</p>
              </div>
              <app-lucide-icon [icon]="icons.wallet" className="w-5 h-5 text-emerald-300" />
            </div>
          </app-prime-card>
          <app-prime-card className="card-navy">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-slate-400 text-sm">Current month primes</p>
                <p class="text-2xl font-bold text-blue-300">{{ currentMonthEarned() }} MAD</p>
              </div>
              <app-lucide-icon [icon]="icons.calendar" className="w-5 h-5 text-blue-300" />
            </div>
          </app-prime-card>
          <app-prime-card className="card-navy">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-slate-400 text-sm">Fiches PRIME — en cours de validation</p>
                <p class="text-2xl font-bold text-amber-300">{{ pendingFicheFlow() }}</p>
              </div>
              <app-lucide-icon [icon]="icons.clock" className="w-5 h-5 text-amber-300" />
            </div>
          </app-prime-card>
        </div>

        <app-prime-card
          title="Monthly earnings"
          description="Your prime evolution by month"
          className="card-navy"
        >
          @if (monthlyData().length === 0) {
            <div class="text-slate-400 py-10 text-center">No primes yet</div>
          } @else {
            <div class="h-72" echarts [options]="chartOptions()"></div>
          }
        </app-prime-card>

        <app-prime-card title="Recent primes" className="card-navy">
          @if (recentPrimes().length === 0) {
            <div class="text-slate-400 py-8 text-center">No primes yet</div>
          } @else {
            <div class="space-y-3">
              @for (prime of recentPrimes(); track prime.id) {
                <div
                  class="bg-navy-900/70 border border-navy-800 rounded-lg px-4 py-3 flex items-center justify-between"
                >
                  <div class="flex items-center gap-3">
                    <app-lucide-icon [icon]="icons.sparkles" className="w-4 h-4 text-blue-300" />
                    <div>
                      <p class="text-slate-200 font-medium">{{ prime.typeName }}</p>
                      <p class="text-slate-400 text-xs">{{ prime.period }}</p>
                    </div>
                  </div>
                  <div class="text-emerald-400 font-semibold">{{ prime.amount }} MAD</div>
                </div>
              }
            </div>
          }
        </app-prime-card>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeDashboardPageComponent {
  private readonly roleService = inject(RoleService);
  private readonly ficheApi = inject(PrimeFicheResultService);

  readonly icons = {
    wallet: Wallet,
    calendar: CalendarClock,
    clock: Clock3,
    sparkles: Sparkles,
  };

  readonly user = computed(() => this.roleService.currentUser());
  readonly currentMonth = computed(() => {
    const periods = this.results().map((r) => r.period);
    if (periods.length === 0) return '';
    return periods.sort().at(-1) ?? '';
  });

  readonly results = signal<PrimeResult[]>([]);
  readonly types = signal<PrimeType[]>([]);
  readonly serviceFiches = signal<EmployeePrimeServiceFicheValidationDto[]>([]);
  readonly loading = signal(true);

  readonly totalEarned = computed(() =>
    this.results().reduce((sum, item) => sum + item.amount, 0),
  );

  readonly currentMonthEarned = computed(() =>
    this.results()
      .filter((item) => item.period === this.currentMonth())
      .reduce((sum, item) => sum + item.amount, 0),
  );

  readonly pendingFicheFlow = computed(() => {
    const rows = this.serviceFiches();
    return rows.filter((f) => f.validationStatus !== 'RH Approved' && f.validationStatus !== 'Rejected').length;
  });

  readonly monthlyData = computed(() => {
    const grouped = new Map<string, number>();
    this.results().forEach((item) => {
      grouped.set(item.period, (grouped.get(item.period) ?? 0) + item.amount);
    });
    return [...grouped.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([month, amount]) => ({ month, amount }));
  });

  readonly recentPrimes = computed(() =>
    [...this.results()]
      .sort((a, b) => b.date.localeCompare(a.date))
      .slice(0, 5)
      .map((result) => ({
        ...result,
        typeName: this.types().find((type) => type.id === result.primeTypeId)?.name ?? 'Unknown',
      })),
  );

  readonly chartOptions = computed<EChartsCoreOption>(() => {
    const data = this.monthlyData();
    return {
      grid: { left: 0, right: 0, top: 10, bottom: 0, containLabel: true },
      tooltip: { trigger: 'axis' },
      xAxis: {
        type: 'category',
        data: data.map((d) => d.month),
        axisLine: { lineStyle: { color: '#1e293b' } },
        axisLabel: { color: '#94a3b8' },
      },
      yAxis: {
        type: 'value',
        axisLine: { lineStyle: { color: '#1e293b' } },
        axisLabel: { color: '#94a3b8' },
        splitLine: { lineStyle: { color: '#1e293b', type: 'dashed' } },
      },
      series: [
        {
          type: 'line',
          smooth: true,
          symbol: 'none',
          data: data.map((d) => d.amount),
          lineStyle: { color: '#60a5fa', width: 2 },
          areaStyle: {
            opacity: 1,
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0.05, color: 'rgba(96, 165, 250, 0.45)' },
                { offset: 0.95, color: 'rgba(96, 165, 250, 0.04)' },
              ],
            },
          },
        },
      ],
    };
  });

  constructor() {
    effect(() => {
      void this.user().id;
      void this.fetch();
    });
  }

  private async fetch(): Promise<void> {
    this.loading.set(true);
    const userId = this.user().id;
    try {
      const [myResults, primeTypes, allFiches] = await Promise.all([
        PrimeService.getMyPrimeResults(userId),
        PrimeService.getPrimeTypes(),
        firstValueFrom(this.ficheApi.list({})),
      ]);
      this.results.set(myResults);
      this.types.set(primeTypes);
      this.serviceFiches.set(allFiches.filter((f) => f.employeeId === userId));
    } finally {
      this.loading.set(false);
    }
  }
}
