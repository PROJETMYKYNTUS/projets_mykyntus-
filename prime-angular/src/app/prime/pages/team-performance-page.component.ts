import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Award, TrendingUp, Users } from 'lucide';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { PrimeCardComponent } from '../components/prime-card.component';
import {
  PrimeFilterBarComponent,
  type PrimeFilterBarFilter,
} from '../components/prime-filter-bar.component';
import type { Employee, Role } from '../models';
import { RoleService } from '../state/role.service';
import {
  PrimeFicheResultService,
  type EmployeePrimeServiceFicheValidationDto,
} from '../services/prime-fiche-result.service';
import { primeHttpErrorDetail } from '../lib/primeHttpErrorMessage';

@Component({
  selector: 'app-team-performance-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent, PrimeFilterBarComponent],
  template: `
    @if (loading()) {
      <div class="p-8 flex justify-center">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
      </div>
    } @else {
      <div class="p-8 space-y-6">
        <div class="flex justify-between items-center">
          <div>
            <h1 class="text-3xl font-bold text-slate-900 tracking-tight">Performance service</h1>
            <p class="text-slate-500 mt-1">
              Synthèse des fiches PRIME et montants pour votre périmètre (pas de tâches externes / ticketing).
            </p>
          </div>
        </div>

        @if (errorMessage()) {
          <app-prime-card>
            <div class="p-4 text-rose-600 text-sm">{{ errorMessage() }}</div>
          </app-prime-card>
        }

        <app-prime-filter-bar [filters]="filterBarFilters()" />

        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div
            class="bg-white p-6 rounded-2xl shadow-sm border border-slate-200 flex items-center gap-4"
          >
            <div
              class="w-12 h-12 bg-indigo-100 text-indigo-600 rounded-xl flex items-center justify-center"
            >
              <app-lucide-icon [icon]="icons.users" className="w-6 h-6" />
            </div>
            <div>
              <p class="text-sm font-medium text-slate-500">Pilotes (fiches)</p>
              <p class="text-2xl font-bold text-slate-900">{{ distinctPilotCount() }}</p>
            </div>
          </div>
          <div
            class="bg-white p-6 rounded-2xl shadow-sm border border-slate-200 flex items-center gap-4"
          >
            <div
              class="w-12 h-12 bg-emerald-100 text-emerald-600 rounded-xl flex items-center justify-center"
            >
              <app-lucide-icon [icon]="icons.award" className="w-6 h-6" />
            </div>
            <div>
              <p class="text-sm font-medium text-slate-500">Montant total (période)</p>
              <p class="text-2xl font-bold text-slate-900">{{ totalAmount() }} MAD</p>
            </div>
          </div>
          <div
            class="bg-white p-6 rounded-2xl shadow-sm border border-slate-200 flex items-center gap-4"
          >
            <div
              class="w-12 h-12 bg-amber-100 text-amber-600 rounded-xl flex items-center justify-center"
            >
              <app-lucide-icon [icon]="icons.trending" className="w-6 h-6" />
            </div>
            <div>
              <p class="text-sm font-medium text-slate-500">Taux validation RH</p>
              <p class="text-2xl font-bold text-slate-900">{{ rhCompletionRate() }}%</p>
            </div>
          </div>
        </div>

        <app-prime-card title="Détail par pilote" className="p-0">
          <div class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-slate-400 uppercase bg-navy-900 border-b border-navy-800">
                <tr>
                  <th class="px-6 py-3 font-medium tracking-wider">Pilote</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Service</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Période</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Total</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Statut</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-navy-800">
                @if (scopedResults().length === 0) {
                  <tr>
                    <td colspan="5" class="px-6 py-8 text-center text-slate-500">
                      Aucune fiche dans votre périmètre pour cette période.
                    </td>
                  </tr>
                } @else {
                  @for (item of scopedResults(); track item.id) {
                    <tr class="bg-navy-900 hover:bg-navy-800 transition-colors">
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        @let emp = getEmployee(item.employeeId);
                        <div class="flex items-center gap-3">
                          <div
                            class="w-8 h-8 rounded-full bg-indigo-100 text-indigo-700 flex items-center justify-center font-bold text-xs"
                          >
                            {{ emp?.firstName?.charAt(0) }}{{ emp?.lastName?.charAt(0) }}
                          </div>
                          <div class="font-medium text-slate-200">
                            {{ emp?.firstName }} {{ emp?.lastName }}
                          </div>
                        </div>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-300">{{ item.serviceId }}</td>
                      <td class="px-6 py-4 whitespace-nowrap font-mono text-slate-200">{{ item.period }}</td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <div class="font-semibold text-emerald-400">{{ formatAmount(item.totalAmount) }}</div>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap">
                        <span [class]="statusBadgeClass(item.validationStatus)">{{ item.validationStatus }}</span>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </app-prime-card>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamPerformancePageComponent {
  private readonly roleService = inject(RoleService);
  private readonly api = inject(PrimeFicheResultService);

  readonly icons = { users: Users, award: Award, trending: TrendingUp };

  readonly results = signal<EmployeePrimeServiceFicheValidationDto[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly periodFilter = signal('2026-04');

  readonly setPeriodFilter = (value: string): void => {
    this.periodFilter.set(value);
  };

  readonly filterBarFilters = computed<PrimeFilterBarFilter[]>(() => [
    {
      name: 'Période',
      value: this.periodFilter(),
      onChange: this.setPeriodFilter,
      options: [
        { label: '2026-04', value: '2026-04' },
        { label: '2026-03', value: '2026-03' },
        { label: '2026-02', value: '2026-02' },
        { label: '2026-01', value: '2026-01' },
      ],
    },
  ]);

  readonly viewerEmployee = computed<Employee | undefined>(() => {
    const id = this.roleService.currentUser().id;
    return this.roleService.employees().find((e) => e.id === id);
  });

  readonly scopedResults = computed(() => {
    const role = this.roleService.currentRole() as Role;
    const me = this.viewerEmployee();
    const rows = this.results();
    if (!me) return rows;
    if (role === 'Superviseur') {
      return rows.filter((r) => r.celluleId === me.celluleId);
    }
    if (role === 'Référent technique' || role === 'Coach') {
      return rows.filter((r) => r.serviceId === me.serviceId);
    }
    if (role === 'Chef de projet' || role === 'Manager' || role === 'RP') {
      const poleId = me.poleId ?? me.departementId ?? '';
      return rows.filter((r) => {
        const emp = this.roleService.employees().find((e) => e.id === r.employeeId);
        return emp && (emp.poleId === poleId || emp.departementId === poleId);
      });
    }
    return rows;
  });

  readonly distinctPilotCount = computed(() => new Set(this.scopedResults().map((r) => r.employeeId)).size);

  readonly totalAmount = computed(() =>
    this.scopedResults().reduce((acc, r) => acc + (r.totalAmount ?? 0), 0),
  );

  readonly rhCompletionRate = computed(() => {
    const rows = this.scopedResults();
    if (rows.length === 0) return 0;
    const done = rows.filter((r) => r.validationStatus === 'RH Approved').length;
    return Math.round((done / rows.length) * 100);
  });

  constructor() {
    effect(() => {
      void this.roleService.currentRole();
      void this.roleService.currentUser().id;
      void this.periodFilter();
      this.fetch();
    });
  }

  private fetch(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.api.list({ period: this.periodFilter() || undefined }).subscribe({
      next: (rows) => {
        this.results.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('[TeamPerformancePage] fetch error', err);
        const detail = primeHttpErrorDetail(err);
        this.errorMessage.set(
          detail
            ? `Impossible de charger les fiches PRIME. ${detail}`
            : 'Impossible de charger les fiches PRIME depuis le backend. Vérifiez que le service est démarré.',
        );
        this.results.set([]);
        this.loading.set(false);
      },
    });
  }

  getEmployee(id: string): Employee | undefined {
    return this.roleService.employees().find((e) => e.id === id);
  }

  formatAmount(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `${value.toFixed(2)}`;
  }

  statusBadgeClass(status: string): string {
    const base = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ';
    if (status === 'RH Approved') return base + 'bg-emerald-100 text-emerald-800';
    if (status === 'Rejected') return base + 'bg-rose-100 text-rose-800';
    if (status === 'Pending') return base + 'bg-amber-100 text-amber-800';
    return base + 'bg-sky-100 text-sky-800';
  }
}
