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
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
      </div>
    } @else {
      @if (stats(); as st) {
      <div class="prime-page-shell space-y-8">
        <div class="flex justify-end items-end gap-4 flex-wrap">
          <app-rp-drill-bar [rpUserId]="rpUserId()" />
          <div class="text-sm font-medium text-slate-300 card-navy px-4 py-2 rounded-lg">
            {{ periodLabel() }}
          </div>
        </div>

        @if (primeSection.activeRpSection() === 'dashboard') {
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-blue-600/10 text-blue-400 rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.target" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-slate-400">Fiches PRIME — avancement pôle</p>
                <p class="text-2xl font-bold text-primary">{{ st.projectProgress }}%</p>
              </div>
            </div>
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-cyan-500/10 text-cyan-300 rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.clipboard" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-slate-400">Fiches complétées (saisie)</p>
                <p class="text-2xl font-bold text-primary">{{ st.completedTasks }}</p>
              </div>
            </div>
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-emerald-500/10 text-emerald-400 rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.gauge" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-slate-400">Taux validation RH (période)</p>
                <p class="text-2xl font-bold text-primary">{{ st.averageTeamPerformance }}%</p>
              </div>
            </div>
            <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
              <div class="w-12 h-12 bg-amber-500/10 text-amber-400 rounded-xl flex items-center justify-center">
                <app-lucide-icon [icon]="icons.check" className="w-6 h-6" />
              </div>
              <div>
                <p class="text-sm font-medium text-slate-400">Validations en attente</p>
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
                    <span class="text-slate-300">{{ member.name }}</span>
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
    const months = st.performanceEvolution.map((x) => x.month);
    const scores = st.performanceEvolution.map((x) => x.score);
    return {
      grid: { left: 0, right: 15, top: 10, bottom: 5, containLabel: true },
      tooltip: {
        trigger: 'axis',
        backgroundColor: '#0f172a',
        borderColor: '#334155',
        borderWidth: 1,
        borderRadius: 10,
        textStyle: { color: '#e2e8f0' },
      },
      xAxis: {
        type: 'category',
        data: months,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#94a3b8', fontSize: 12 },
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 100,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#94a3b8', fontSize: 12 },
        splitLine: { lineStyle: { type: 'dashed', color: '#334155' } },
      },
      series: [
        {
          type: 'line',
          data: scores,
          smooth: true,
          symbol: 'none',
          lineStyle: { color: '#38bdf8', width: 3 },
          areaStyle: { color: 'rgba(56, 189, 248, 0.25)' },
        },
      ],
    };
  });

  readonly barOptions = computed(() => {
    const st = this.stats();
    if (!st) return {};
    const names = st.memberPerformance.map((m) => m.name);
    const scores = st.memberPerformance.map((m) => m.score);
    return {
      grid: { left: 15, right: 15, top: 5, bottom: 5, containLabel: true },
      tooltip: {
        trigger: 'axis',
        backgroundColor: '#0f172a',
        borderColor: '#334155',
        borderWidth: 1,
        borderRadius: 10,
        textStyle: { color: '#e2e8f0' },
      },
      xAxis: {
        type: 'value',
        min: 0,
        max: 100,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#94a3b8', fontSize: 12 },
        splitLine: { lineStyle: { type: 'dashed', color: '#334155' } },
      },
      yAxis: {
        type: 'category',
        data: names,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#cbd5e1', fontSize: 12, width: 100 },
      },
      series: [
        {
          type: 'bar',
          data: scores,
          barWidth: 20,
          itemStyle: { color: '#22d3ee', borderRadius: [0, 4, 4, 0] },
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
    if (status === 'Excellent') return 'text-emerald-400';
    if (status === 'Moyen') return 'text-amber-400';
    return 'text-rose-400';
  }
}
