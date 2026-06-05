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
import { ArrowDown, ArrowUp, Gauge } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { PrimeService } from '../../services/prime.service';
import type { PrimeResult } from '../../models';
import { RoleService } from '../../state/role.service';

echarts.use([LineChart, GridComponent, TooltipComponent, CanvasRenderer]);

interface ChartPoint {
  month: string;
  myScore: number;
  teamAverage: number;
}

@Component({
  selector: 'app-my-performance-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent, NgxEchartsDirective],
  providers: [provideEchartsCore({ echarts })],
  template: `
    @if (loading()) {
      <div class="p-8 text-slate-400">Loading your performance...</div>
    } @else {
      <div class="p-8 space-y-6 bg-app min-h-full">
        <div>
          <h1 class="text-3xl font-bold text-slate-100">Ma performance PRIME</h1>
          <p class="text-slate-400 mt-1">
            Évolution de vos scores de prime et comparaison à la moyenne de votre équipe (données PRIME).
          </p>
        </div>

        <app-prime-card
          title="Score evolution"
          description="My score vs team average"
          className="card-navy"
        >
          @if (chartData().length === 0) {
            <div class="text-slate-400 py-10 text-center">No primes yet</div>
          } @else {
            <div class="h-80" echarts [options]="chartOptions()"></div>
          }
        </app-prime-card>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <app-prime-card className="card-navy">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-slate-400 text-sm">Average score</p>
                <p class="text-2xl font-bold text-blue-300">{{ averageScore() }}</p>
              </div>
              <app-lucide-icon [icon]="icons.gauge" className="w-5 h-5 text-blue-300" />
            </div>
          </app-prime-card>
          <app-prime-card className="card-navy">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-slate-400 text-sm">Best month</p>
                <p class="text-lg font-semibold text-emerald-400">
                  {{ bestMonth() ? bestMonth()!.month + ' (' + bestMonth()!.myScore + ')' : '-' }}
                </p>
              </div>
              <app-lucide-icon [icon]="icons.up" className="w-5 h-5 text-emerald-400" />
            </div>
          </app-prime-card>
          <app-prime-card className="card-navy">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-slate-400 text-sm">Worst month</p>
                <p class="text-lg font-semibold text-rose-400">
                  {{ worstMonth() ? worstMonth()!.month + ' (' + worstMonth()!.myScore + ')' : '-' }}
                </p>
              </div>
              <app-lucide-icon [icon]="icons.down" className="w-5 h-5 text-rose-400" />
            </div>
          </app-prime-card>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyPerformancePageComponent {
  private readonly roleService = inject(RoleService);

  readonly icons = { gauge: Gauge, up: ArrowUp, down: ArrowDown };

  readonly myResults = signal<PrimeResult[]>([]);
  readonly allResults = signal<PrimeResult[]>([]);
  readonly loading = signal(true);

  readonly user = computed(() => this.roleService.currentUser());

  readonly chartData = computed<ChartPoint[]>(() => {
    const monthMap = new Map<string, { mineScores: number[]; teamScores: number[] }>();
    const userId = this.user().id;
    this.myResults().forEach((result) => {
      const month = result.period;
      const sameMonthTeam = this.allResults().filter(
        (item) => item.period === month && item.employeeId !== userId,
      );
      const current = monthMap.get(month) ?? { mineScores: [], teamScores: [] };
      current.mineScores.push(result.score);
      sameMonthTeam.forEach((teamResult) => current.teamScores.push(teamResult.score));
      monthMap.set(month, current);
    });
    return [...monthMap.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([month, values]) => {
        const myAvg = values.mineScores.length
          ? values.mineScores.reduce((sum, s) => sum + s, 0) / values.mineScores.length
          : 0;
        const teamAvg = values.teamScores.length
          ? values.teamScores.reduce((sum, s) => sum + s, 0) / values.teamScores.length
          : 0;
        return {
          month,
          myScore: Number(myAvg.toFixed(1)),
          teamAverage: Number(teamAvg.toFixed(1)),
        };
      });
  });

  readonly averageScore = computed(() => {
    const data = this.chartData();
    return data.length
      ? Number((data.reduce((sum, item) => sum + item.myScore, 0) / data.length).toFixed(1))
      : 0;
  });

  readonly bestMonth = computed<ChartPoint | undefined>(
    () => [...this.chartData()].sort((a, b) => b.myScore - a.myScore)[0],
  );
  readonly worstMonth = computed<ChartPoint | undefined>(
    () => [...this.chartData()].sort((a, b) => a.myScore - b.myScore)[0],
  );

  readonly chartOptions = computed<EChartsCoreOption>(() => {
    const data = this.chartData();
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
          name: 'My score',
          type: 'line',
          smooth: true,
          data: data.map((d) => d.myScore),
          lineStyle: { color: '#60a5fa', width: 3 },
          itemStyle: { color: '#60a5fa' },
          symbol: 'circle',
          symbolSize: 8,
        },
        {
          name: 'Team average',
          type: 'line',
          smooth: true,
          data: data.map((d) => d.teamAverage),
          lineStyle: { color: '#a78bfa', width: 2, type: 'dashed' },
          itemStyle: { color: '#a78bfa' },
          symbol: 'circle',
          symbolSize: 6,
        },
      ],
    };
  });

  constructor() {
    effect(() => {
      void this.user().id;
      this.fetch();
    });
  }

  private fetch(): void {
    this.loading.set(true);
    const userId = this.user().id;
    void Promise.all([
      PrimeService.getMyPrimeResults(userId),
      PrimeService.getPrimeResultsScoped('Pilote', userId),
    ]).then(([mine, all]) => {
      this.myResults.set(mine);
      this.allResults.set(all);
      this.loading.set(false);
    });
  }
}
