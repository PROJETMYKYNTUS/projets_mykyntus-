import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import * as echarts from 'echarts/core';
import { BarChart, LineChart } from 'echarts/charts';
import { GridComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { CheckCircle2, ClipboardCheck, GaugeCircle, Target } from 'lucide';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { RpPrimeService, type RpDashboardStats } from '../../services/rp-prime.service';
import { PrimeFicheResultService } from '../../services/prime-fiche-result.service';
import { firstValueFrom } from 'rxjs';
import { PrimeSectionService } from '../../state/prime-section.service';
import { primeChartRgba, primeChartTheme } from '../../lib/allowance-status';
import { RpDrillBarComponent } from './rp-drill-bar.component';
import { RpTeamPerformanceComponent } from './rp-team-performance.component';
import { RpPrimeFichesPanelComponent } from './rp-prime-fiches-panel.component';
import { RpFinalValidationComponent } from './rp-final-validation.component';

echarts.use([LineChart, BarChart, GridComponent, TooltipComponent, CanvasRenderer]);

@Component({
  selector: 'app-rp-dashboard',
  standalone: true,
  imports: [
    PrimeCardComponent,
    LucideIconComponent,
    NgxEchartsDirective,
    RpDrillBarComponent,
    RpTeamPerformanceComponent,
    RpPrimeFichesPanelComponent,
    RpFinalValidationComponent,
  ],
  providers: [provideEchartsCore({ echarts })],
  template: `
    @if (loading() || !stats()) {
      <div class="p-8 flex justify-center items-center h-full">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-[var(--soft-blue)]"></div>
      </div>
    } @else {
      @if (stats(); as st) {
      <div class="prime-page-shell space-y-8">
        <div class="flex justify-end items-end gap-4 flex-wrap">
          <app-rp-drill-bar [rpUserId]="rpUserId()" />
          <div class="text-sm font-medium text-muted card-navy px-4 py-2 rounded-lg">
            {{ periodLabel() }}
          </div>
        </div>

        @if (primeSection.activeRpSection() === 'dashboard') {
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-[var(--info-bg)] text-[var(--info-text)] rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.target" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-muted">Fiches PRIME — avancement pôle</p>
                <p class="text-2xl font-bold text-primary">{{ st.projectProgress }}%</p>
              </div>
            </div>
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-[var(--info-bg)] text-[var(--info-text)] rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.clipboard" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-muted">Fiches complétées (saisie)</p>
                <p class="text-2xl font-bold text-primary">{{ st.completedTasks }}</p>
              </div>
            </div>
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-[var(--success-bg)] text-[var(--success-text)] rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.gauge" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-muted">Taux validation RH (période)</p>
                <p class="text-2xl font-bold text-primary">{{ st.averageTeamPerformance }}%</p>
              </div>
            </div>
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-[var(--warning-bg)] text-[var(--warning-text)] rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.check" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-muted">Validations en attente</p>
                <p class="text-2xl font-bold text-primary">{{ st.pendingValidations }}</p>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
            <app-prime-card title="Performance Evolution" className="lg:col-span-2">
              <div class="h-80 w-full" echarts [options]="areaOptions()" [initOpts]="initOpts"></div>
            </app-prime-card>
            <app-prime-card title="Performance par membre">
              <div class="h-80 w-full" echarts [options]="barOptions()" [initOpts]="initOpts"></div>
              <div class="mt-4 space-y-2">
                @for (member of st.memberPerformance; track member.name) {
                  <div class="flex items-center justify-between text-sm">
                    <span class="text-muted">{{ member.name }}</span>
                    <span [class]="statusClass(member.status)">{{ member.status }}</span>
                  </div>
                }
              </div>
            </app-prime-card>
          </div>
        }

        @if (primeSection.activeRpSection() === 'performance') {
          <app-rp-team-performance [rpUserId]="rpUserId()" />
        }
        @if (primeSection.activeRpSection() === 'validation') {
          <app-rp-final-validation [rpUserId]="rpUserId()" />
        }
        @if (primeSection.activeRpSection() === 'suivi-projet') {
          <app-rp-prime-fiches-panel [rpUserId]="rpUserId()" />
        }
      </div>
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RpDashboardComponent {
  readonly rpUserId = input.required<string>();
  readonly primeSection = inject(PrimeSectionService);
  private readonly ficheApi = inject(PrimeFicheResultService);
  readonly icons = { target: Target, clipboard: ClipboardCheck, gauge: GaugeCircle, check: CheckCircle2 };

  readonly stats = signal<RpDashboardStats | null>(null);
  readonly loading = signal(true);
  readonly periodLabel = signal<string>('—');
  readonly initOpts = { renderer: 'canvas' as const };

  readonly areaOptions = computed(() => {
    const st = this.stats();
    if (!st) return {};
    const c = primeChartTheme();
    const months = st.performanceEvolution.map((x) => x.month);
    const scores = st.performanceEvolution.map((x) => x.score);
    return {
      grid: { left: 0, right: 15, top: 10, bottom: 5, containLabel: true },
      tooltip: {
        trigger: 'axis',
        backgroundColor: c.tooltipBg,
        borderColor: c.tooltipBorder,
        borderWidth: 1,
        borderRadius: c.radiusMd,
        textStyle: { color: c.tooltipText },
      },
      xAxis: {
        type: 'category',
        data: months,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: c.axisLabel, fontSize: 12 },
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 100,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: c.axisLabel, fontSize: 12 },
        splitLine: { lineStyle: { type: 'dashed', color: c.splitLine } },
      },
      series: [
        {
          type: 'line',
          data: scores,
          smooth: true,
          symbol: 'none',
          lineStyle: { color: c.info, width: 3 },
          areaStyle: { color: primeChartRgba('--soft-blue-rgb', 0.25) },
        },
      ],
    };
  });

  readonly barOptions = computed(() => {
    const st = this.stats();
    if (!st) return {};
    const c = primeChartTheme();
    const names = st.memberPerformance.map((m) => m.name);
    const scores = st.memberPerformance.map((m) => m.score);
    return {
      grid: { left: 15, right: 15, top: 5, bottom: 5, containLabel: true },
      tooltip: {
        trigger: 'axis',
        backgroundColor: c.tooltipBg,
        borderColor: c.tooltipBorder,
        borderWidth: 1,
        borderRadius: c.radiusMd,
        textStyle: { color: c.tooltipText },
      },
      xAxis: {
        type: 'value',
        min: 0,
        max: 100,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: c.axisLabel, fontSize: 12 },
        splitLine: { lineStyle: { type: 'dashed', color: c.splitLine } },
      },
      yAxis: {
        type: 'category',
        data: names,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: c.tooltipText, fontSize: 12, width: 100 },
      },
      series: [
        {
          type: 'bar',
          data: scores,
          barWidth: 20,
          itemStyle: { color: c.info, borderRadius: c.barRadiusEnd },
        },
      ],
    };
  });

  constructor() {
    effect(() => {
      const id = this.rpUserId();
      this.loading.set(true);
      RpPrimeService.getRpDashboardStats(id).then(async (data) => {
        this.stats.set(data);
        try {
          const periods = await firstValueFrom(this.ficheApi.periods());
          this.periodLabel.set(periods[0] ?? '—');
        } catch {
          this.periodLabel.set('—');
        }
        this.loading.set(false);
      });
    });
  }

  statusClass(status: string): string {
    if (status === 'Excellent') return 'text-[var(--success-text)]';
    if (status === 'Moyen') return 'text-[var(--warning-text)]';
    return 'text-[var(--danger-text)]';
  }
}
