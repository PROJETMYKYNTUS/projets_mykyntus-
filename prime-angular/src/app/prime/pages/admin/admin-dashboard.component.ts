import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import * as echarts from 'echarts/core';
import { BarChart, LineChart, PieChart } from 'echarts/charts';
import { GridComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { AlertTriangle, Clock3, Database, LoaderCircle } from 'lucide';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { PrimeSectionService } from '../../state/prime-section.service';
import { AdminPrimeService } from '../../services/admin-prime.service';
import { WorkflowConfigAdminComponent } from '../../components/admin/workflow-config-admin.component';
import { RbacAdminComponent } from '../../components/admin/rbac-admin.component';
import { AuditLogsAdminComponent } from '../../components/admin/audit-logs-admin.component';
import { AnomaliesAdminComponent } from '../../components/admin/anomalies-admin.component';

echarts.use([LineChart, BarChart, PieChart, GridComponent, TooltipComponent, CanvasRenderer]);

type DashboardPayload = Awaited<ReturnType<typeof AdminPrimeService.getDashboard>>;

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    PrimeCardComponent,
    LucideIconComponent,
    NgxEchartsDirective,
    WorkflowConfigAdminComponent,
    RbacAdminComponent,
    AuditLogsAdminComponent,
    AnomaliesAdminComponent,
  ],
  providers: [provideEchartsCore({ echarts })],
  template: `
    @if (primeSection.activeAdminSection() === 'workflows') {
      <div class="p-8 space-y-6 bg-navy-950 min-h-full">
        <app-workflow-config-admin />
      </div>
    } @else {
      @if (!data()) {
        <div class="p-8 flex justify-center">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-cyan-500"></div>
        </div>
      } @else {
        @let d = data()!;
        <div class="p-8 space-y-8 bg-navy-950 min-h-full">
          <div>
            <h1 class="text-3xl font-bold text-white tracking-tight">Dashboard Admin Systeme</h1>
            <p class="text-slate-400 mt-1">Supervision technique, gouvernance et controle du moteur PRIME.</p>
          </div>

          @if (primeSection.activeAdminSection() === 'dashboard') {
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
                <app-lucide-icon [icon]="icons.db" className="w-6 h-6 text-cyan-300" />
                <div>
                  <p class="text-slate-400 text-sm">Primes generees</p>
                  <p class="text-white text-2xl font-bold">{{ d.kpis.totalGeneratedPrimes }}</p>
                </div>
              </div>
              <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
                <app-lucide-icon [icon]="icons.loader" className="w-6 h-6 text-amber-300" />
                <div>
                  <p class="text-slate-400 text-sm">Validations en cours</p>
                  <p class="text-white text-2xl font-bold">{{ d.kpis.validationsInProgress }}</p>
                </div>
              </div>
              <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
                <app-lucide-icon [icon]="icons.alert" className="w-6 h-6 text-rose-300" />
                <div>
                  <p class="text-slate-400 text-sm">Erreurs detectees</p>
                  <p class="text-white text-2xl font-bold">{{ d.kpis.errorCount }}</p>
                </div>
              </div>
              <div class="card-navy p-6 rounded-2xl flex items-center gap-4">
                <app-lucide-icon [icon]="icons.clock" className="w-6 h-6 text-emerald-300" />
                <div>
                  <p class="text-slate-400 text-sm">Temps moyen traitement</p>
                  <p class="text-white text-2xl font-bold">{{ d.kpis.avgProcessingTimeSec }}s</p>
                </div>
              </div>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
              <app-prime-card title="Volume primes par mois" className="lg:col-span-2">
                <div class="h-72" echarts [options]="volumeOptions()" [initOpts]="chartInit"></div>
              </app-prime-card>
              <app-prime-card title="Taux de validation">
                <div class="h-72" echarts [options]="validationBarOptions()" [initOpts]="chartInit"></div>
              </app-prime-card>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
              <app-prime-card title="Repartition par departement">
                <div class="h-64" echarts [options]="pieOptions()" [initOpts]="chartInit"></div>
              </app-prime-card>
              <app-prime-card title="Alertes systeme" className="lg:col-span-2">
                <div class="space-y-3">
                  @for (alert of d.alerts; track alert.id) {
                    <div class="rounded-xl border border-default bg-navy-900 p-4">
                      <div class="flex items-center justify-between">
                        <span class="text-slate-200 font-medium">{{ alert.type }}</span>
                        <span class="text-xs text-slate-400">{{ alert.date }}</span>
                      </div>
                      <p class="text-slate-300 mt-1">{{ alert.message }}</p>
                      <p
                        [class]="
                          alert.severity === 'Haute'
                            ? 'text-rose-300 text-xs mt-2'
                            : 'text-amber-300 text-xs mt-2'
                        "
                      >
                        Severite: {{ alert.severity }}
                      </p>
                    </div>
                  }
                </div>
              </app-prime-card>
            </div>
          }

          @if (primeSection.activeAdminSection() === 'access') {
            <app-rbac-admin />
          }

          @if (primeSection.activeAdminSection() === 'logs') {
            <app-audit-logs-admin />
          }

          @if (primeSection.activeAdminSection() === 'anomalies') {
            <app-anomalies-admin />
          }
        </div>
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDashboardComponent implements OnInit {
  readonly primeSection = inject(PrimeSectionService);
  readonly icons = { db: Database, loader: LoaderCircle, alert: AlertTriangle, clock: Clock3 };

  readonly data = signal<DashboardPayload | null>(null);

  readonly chartInit = { renderer: 'canvas' as const };

  readonly volumeOptions = computed(() => {
    const d = this.data();
    if (!d) return {};
    return {
      grid: { left: 0, right: 0, top: 10, bottom: 0, containLabel: true },
      tooltip: {
        backgroundColor: '#0f172a',
        borderColor: '#334155',
        borderWidth: 1,
        borderRadius: 8,
      },
      xAxis: {
        type: 'category',
        data: d.charts.volumeByMonth.map((x) => x.month),
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#94a3b8' },
      },
      yAxis: {
        type: 'value',
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#94a3b8' },
        splitLine: { lineStyle: { color: '#334155', type: 'dashed' } },
      },
      series: [
        {
          type: 'line',
          data: d.charts.volumeByMonth.map((x) => x.value),
          smooth: true,
          symbol: 'none',
          lineStyle: { color: '#22d3ee', width: 2 },
          areaStyle: { color: '#22d3ee33' },
        },
      ],
    };
  });

  readonly validationBarOptions = computed(() => {
    const d = this.data();
    if (!d) return {};
    return {
      grid: { left: 0, right: 0, top: 10, bottom: 0, containLabel: true },
      tooltip: {
        backgroundColor: '#0f172a',
        borderColor: '#334155',
        borderWidth: 1,
        borderRadius: 8,
      },
      xAxis: {
        type: 'category',
        data: d.charts.validationRate.map((x) => x.month),
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#94a3b8' },
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 100,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: '#94a3b8' },
        splitLine: { lineStyle: { color: '#334155', type: 'dashed' } },
      },
      series: [
        {
          type: 'bar',
          data: d.charts.validationRate.map((x) => x.value),
          itemStyle: { color: '#60a5fa', borderRadius: [4, 4, 0, 0] },
        },
      ],
    };
  });

  readonly pieOptions = computed(() => {
    const d = this.data();
    if (!d) return {};
    const colors = ['#22d3ee', '#818cf8', '#34d399'];
    return {
      tooltip: {
        trigger: 'item',
        backgroundColor: '#0f172a',
        borderColor: '#334155',
        borderWidth: 1,
        borderRadius: 8,
      },
      series: [
        {
          type: 'pie',
          radius: '85%',
          data: d.charts.byDepartment.map((x, i) => ({
            name: x.name,
            value: x.value,
            itemStyle: { color: colors[i % colors.length] },
          })),
        },
      ],
    };
  });

  ngOnInit(): void {
    void AdminPrimeService.getDashboard().then((x) => this.data.set(x));
  }

}
