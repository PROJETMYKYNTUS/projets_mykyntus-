import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CheckCircle2, CircleX, Clock3, Wallet } from 'lucide';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import {
  PrimeFilterBarComponent,
  type PrimeFilterBarFilter,
} from '../../components/prime-filter-bar.component';
import { firstValueFrom } from 'rxjs';
import { PrimeService } from '../../services/prime.service';
import {
  PrimeFicheResultService,
  type EmployeePrimeServiceFicheValidationDto,
} from '../../services/prime-fiche-result.service';
import type { PrimeResult, PrimeType } from '../../models';
import { RoleService } from '../../state/role.service';

@Component({
  selector: 'app-my-primes-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent, PrimeFilterBarComponent],
  template: `
    @if (loading()) {
      <div class="p-8 text-slate-400">Loading your primes...</div>
    } @else {
      <div class="p-8 space-y-6 bg-navy-950 min-h-full">
        <div class="flex items-start justify-between">
          <div>
            <h1 class="text-3xl font-bold text-slate-100">Mes primes</h1>
            <p class="text-slate-400 mt-1">Historique des primes et fiches service PRIME par période.</p>
          </div>
          <div
            class="inline-flex items-center gap-2 bg-navy-900/80 border border-navy-800 rounded-lg px-3 py-2 text-slate-300 text-sm"
          >
            <app-lucide-icon [icon]="icons.wallet" className="w-4 h-4 text-blue-300" />
            <span>{{ user().firstName }} {{ user().lastName }}</span>
          </div>
        </div>

        <app-prime-filter-bar [filters]="filterBarFilters()" />

        <app-prime-card className="p-0 card-navy">
          <div class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-slate-400 uppercase bg-navy-900 border-b border-navy-800">
                <tr>
                  <th class="px-6 py-3 font-medium tracking-wider">Prime Type</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Period</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Score</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Amount</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-navy-800">
                @if (filtered().length === 0) {
                  <tr>
                    <td colspan="5" class="px-6 py-8 text-center text-slate-500">No primes yet</td>
                  </tr>
                } @else {
                  @for (item of filtered(); track item.id) {
                    <tr class="bg-navy-900 hover:bg-navy-800 transition-colors">
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <span class="font-medium text-slate-200">{{ getTypeName(item.primeTypeId) }}</span>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">{{ item.period }}</td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">{{ item.score }}</td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <span class="font-semibold text-emerald-400">{{ item.amount }} MAD</span>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        @switch (normalize(item.status)) {
                          @case ('Approved') {
                            <span
                              class="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium bg-emerald-50 text-emerald-700 border border-emerald-200"
                            >
                              <app-lucide-icon [icon]="icons.check" className="w-3.5 h-3.5" /> Approved
                            </span>
                          }
                          @case ('Pending') {
                            <span
                              class="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200"
                            >
                              <app-lucide-icon [icon]="icons.clock" className="w-3.5 h-3.5" /> Pending
                            </span>
                          }
                          @default {
                            <span
                              class="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium bg-rose-50 text-rose-700 border border-rose-200"
                            >
                              <app-lucide-icon [icon]="icons.cross" className="w-3.5 h-3.5" /> Rejected
                            </span>
                          }
                        }
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </app-prime-card>

        <app-prime-card title="Fiches service PRIME" className="p-0 card-navy">
          <div class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-slate-400 uppercase bg-navy-900 border-b border-navy-800">
                <tr>
                  <th class="px-6 py-3 font-medium tracking-wider">Période</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Statut validation</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Remplissage</th>
                  <th class="px-6 py-3 font-medium tracking-wider text-right">Export</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-navy-800">
                @if (serviceFiches().length === 0) {
                  <tr>
                    <td colspan="4" class="px-6 py-8 text-center text-slate-500">Aucune fiche service pour le moment.</td>
                  </tr>
                } @else {
                  @for (f of serviceFiches(); track f.id) {
                    <tr class="bg-navy-900 hover:bg-navy-800 transition-colors">
                      <td class="px-6 py-4 font-mono text-slate-200">{{ f.period }}</td>
                      <td class="px-6 py-4 text-slate-300">{{ f.validationStatus }}</td>
                      <td class="px-6 py-4 text-slate-400">{{ f.fillingStatus }}</td>
                      <td class="px-6 py-4 text-right">
                        <button
                          type="button"
                          (click)="downloadFicheCsv(f.id)"
                          class="text-cyan-400 hover:text-cyan-300 text-sm underline"
                        >
                          CSV
                        </button>
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
export class MyPrimesPageComponent {
  private readonly roleService = inject(RoleService);
  private readonly ficheApi = inject(PrimeFicheResultService);

  readonly icons = { wallet: Wallet, check: CheckCircle2, clock: Clock3, cross: CircleX };

  readonly user = computed(() => this.roleService.currentUser());

  readonly results = signal<PrimeResult[]>([]);
  readonly types = signal<PrimeType[]>([]);
  readonly serviceFiches = signal<EmployeePrimeServiceFicheValidationDto[]>([]);
  readonly loading = signal(true);
  readonly statusFilter = signal('');
  readonly periodFilter = signal('');

  readonly setStatusFilter = (value: string): void => {
    this.statusFilter.set(value);
  };

  readonly setPeriodFilter = (value: string): void => {
    this.periodFilter.set(value);
  };

  readonly periods = computed(() =>
    [...new Set(this.results().map((r) => r.period))].sort(),
  );

  readonly filterBarFilters = computed<PrimeFilterBarFilter[]>(() => [
    {
      name: 'Status',
      value: this.statusFilter(),
      onChange: this.setStatusFilter,
      options: [
        { label: 'Approved', value: 'Approved' },
        { label: 'Pending', value: 'Pending' },
        { label: 'Rejected', value: 'Rejected' },
      ],
    },
    {
      name: 'Period',
      value: this.periodFilter(),
      onChange: this.setPeriodFilter,
      options: this.periods().map((p) => ({ label: p, value: p })),
    },
  ]);

  readonly filtered = computed(() => {
    const sf = this.statusFilter();
    const pf = this.periodFilter();
    return this.results().filter((r) => {
      const normalized = this.normalize(r.status);
      const statusMatch = sf ? normalized === sf : true;
      const periodMatch = pf ? r.period === pf : true;
      return statusMatch && periodMatch;
    });
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
    void (async () => {
      const [myResults, primeTypes] = await Promise.all([
        PrimeService.getMyPrimeResults(userId),
        PrimeService.getPrimeTypes(),
      ]);
      this.results.set(myResults);
      this.types.set(primeTypes);
      try {
        const fiches = await firstValueFrom(this.ficheApi.list({}));
        this.serviceFiches.set(fiches.filter((f) => f.employeeId === userId));
      } catch {
        this.serviceFiches.set([]);
      }
      this.loading.set(false);
    })();
  }

  downloadFicheCsv(id: string): void {
    const u = this.user();
    window.open(this.ficheApi.exportCsvUrl(id, u.id, u.role), '_blank', 'noopener');
  }

  getTypeName(id: string): string {
    return this.types().find((t) => t.id === id)?.name ?? 'Unknown';
  }

  normalize(status: PrimeResult['status']): 'Approved' | 'Pending' | 'Rejected' {
    if (status === 'Rejected') return 'Rejected';
    if (status === 'Pending') return 'Pending';
    return 'Approved';
  }
}
