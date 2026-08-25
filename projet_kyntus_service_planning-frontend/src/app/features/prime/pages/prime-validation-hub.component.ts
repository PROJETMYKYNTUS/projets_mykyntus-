import {

  ChangeDetectionStrategy,

  Component,

  OnInit,

  computed,

  effect,

  inject,

  signal,

} from '@angular/core';

import { catchError, firstValueFrom, of } from 'rxjs';

import { AlertTriangle, CheckCircle2 } from 'lucide';

import { LucideIconComponent } from '@/shared/lucide-icon.component';

import { PrimeValidationPageComponent } from './prime-validation-page.component';

import { PrimeValidationHistoryPageComponent } from './prime-validation-history-page.component';

import { PrimeResultsPageComponent } from './prime-results-page.component';

import { TeamPerformancePageComponent } from './team-performance-page.component';

import { PrimeNavRequestService } from '../services/prime-nav-request.service';

import {

  PrimeCellPrimeApiService,

  type SupervisorCelluleCampaignDto,

} from '../services/prime-cell-prime-api.service';

import { RoleService } from '../state/role.service';

import { PrimeScopeStore } from '../state/prime-scope.store';

import { navigatePrimeCampaignPath } from '../lib/prime-campaign-nav';



type ValidationHubTab = 'validate' | 'history' | 'results' | 'performance';



@Component({

  selector: 'app-prime-validation-hub',

  standalone: true,

  imports: [

    LucideIconComponent,

    PrimeValidationPageComponent,

    PrimeValidationHistoryPageComponent,

    PrimeResultsPageComponent,

    TeamPerformancePageComponent,

  ],

  changeDetection: ChangeDetectionStrategy.OnPush,

  template: `

    <div class="flex min-h-0 flex-col gap-3">

      @if (statusRow(); as row) {

        <div

          class="rounded-lg border border-default bg-input px-4 py-3 text-sm"

          role="status"

        >

          <div class="flex flex-wrap items-start justify-between gap-3">

            <div class="space-y-2">

              <p class="text-xs font-semibold uppercase tracking-wide text-muted">

                Validation — {{ row.celluleName }} · {{ row.period }}

              </p>

              <div class="flex flex-wrap gap-x-4 gap-y-1 text-primary">

                <span class="inline-flex items-center gap-1.5">

                  @if (commonPartReady(row)) {

                    <app-lucide-icon [icon]="icons.ok" className="w-4 h-4 text-[color:var(--success-text)]" />

                  } @else {

                    <app-lucide-icon [icon]="icons.warn" className="w-4 h-4 text-[color:var(--warning-text)]" />

                  }

                  Partie commune : {{ commonPartLabel(row) }}

                </span>

                <span>

                  Fiches agents :

                  <strong>{{ row.completeEmployees }} / {{ row.totalEmployees }}</strong> complètes

                </span>

                @if (row.pendingValidationCount > 0) {

                  <span class="text-[color:var(--warning-text)]">

                    {{ row.pendingValidationCount }} en attente de validation

                  </span>

                }

                @if (row.rejectedCount > 0) {

                  <span class="text-[color:var(--danger-text)]">{{ row.rejectedCount }} rejetée(s)</span>

                }

              </div>

              @if (blockedSteps(row).length) {

                <ul class="space-y-1 text-xs text-muted">

                  @for (step of blockedSteps(row); track step.key) {

                    <li class="flex flex-wrap items-center gap-2">

                      <span class="text-[color:var(--warning-text)]">{{ step.label }} : {{ step.reason }}</span>

                      @if (step.actionPath) {

                        <button

                          type="button"

                          (click)="goStep(step.actionPath)"

                          class="font-semibold text-[color:var(--info-text)] hover:underline"

                        >

                          Corriger

                        </button>

                      }

                    </li>

                  }

                </ul>

              }

            </div>

            @if (row.nextActionPath && row.nextActionLabel && tab() === 'validate') {

              <button

                type="button"

                (click)="goStep(row.nextActionPath)"

                class="shrink-0 rounded-lg border border-default bg-input px-3 py-1.5 text-xs font-semibold text-primary hover:bg-input/80"

              >

                {{ row.nextActionLabel }}

              </button>

            }

          </div>

        </div>

      }



      <div class="flex flex-wrap gap-2 border-b border-default px-1 pb-2">

        <button type="button" (click)="tab.set('validate')" [class]="tabClass('validate')">

          Valider les primes

        </button>

        <button type="button" (click)="tab.set('history')" [class]="tabClass('history')">

          Suivi validation

        </button>

        <button type="button" (click)="tab.set('results')" [class]="tabClass('results')">

          Résultats

        </button>

        <button type="button" (click)="tab.set('performance')" [class]="tabClass('performance')">

          Performance équipe

        </button>

      </div>

      @switch (tab()) {

        @case ('validate') {

          <app-prime-validation-page />

        }

        @case ('history') {

          <app-prime-validation-history-page />

        }

        @case ('results') {

          <app-prime-results-page />

        }

        @case ('performance') {

          <app-team-performance-page />

        }

      }

    </div>

  `,

})

export class PrimeValidationHubComponent implements OnInit {

  private readonly nav = inject(PrimeNavRequestService);

  private readonly api = inject(PrimeCellPrimeApiService);

  private readonly role = inject(RoleService);

  private readonly scope = inject(PrimeScopeStore);



  readonly tab = signal<ValidationHubTab>('validate');

  readonly campaign = signal<SupervisorCelluleCampaignDto[]>([]);

  readonly icons = { ok: CheckCircle2, warn: AlertTriangle };



  readonly statusRow = computed(() => {

    const rows = this.campaign();

    if (!rows.length) return null;

    const cell = this.scope.selectedCelluleId().trim();

    return rows.find((r) => r.celluleId === cell) ?? rows[0] ?? null;

  });



  constructor() {

    effect(() => {

      const requested = this.nav.requestedTab();

      if (!requested) return;

      const mapped = this.mapTab(requested);

      if (mapped) {

        this.tab.set(mapped);

        this.nav.clearRequestedTab();

      }

    });

  }



  ngOnInit(): void {

    const uid = this.role.currentUser()?.id;

    if (uid) this.scope.hydrateFromStorage(uid);

    const requested = this.nav.requestedTab();

    if (requested) {

      const mapped = this.mapTab(requested);

      if (mapped) this.tab.set(mapped);

      this.nav.clearRequestedTab();

    }

    void this.loadCampaign();

  }



  commonPartReady(row: SupervisorCelluleCampaignDto): boolean {

    return (row.commonPartStatus ?? '').toLowerCase() === 'validated';

  }



  commonPartLabel(row: SupervisorCelluleCampaignDto): string {

    return this.commonPartReady(row) ? 'prête' : 'à finaliser';

  }



  blockedSteps(row: SupervisorCelluleCampaignDto) {

    return (row.steps ?? []).filter((s) => s.state === 'blocked' && s.reason);

  }



  goStep(path: string | null | undefined): void {

    navigatePrimeCampaignPath(this.nav, path);

  }



  private mapTab(raw: string): ValidationHubTab | null {

    const t = raw.trim().toLowerCase();

    if (t === 'validate' || t === 'validation' || t === 'valider') return 'validate';

    if (t === 'history' || t === 'suivi' || t === 'validation-history') return 'history';

    if (t === 'results' || t === 'resultats') return 'results';

    if (t === 'performance' || t === 'team-performance') return 'performance';

    return null;

  }



  tabClass(t: ValidationHubTab): string {

    const active = this.tab() === t;

    return active

      ? 'rounded-lg bg-blue-600 px-3 py-1.5 text-sm font-semibold text-white'

      : 'rounded-lg border border-default bg-card px-3 py-1.5 text-sm font-medium text-primary hover:bg-input/40';

  }



  private async loadCampaign(): Promise<void> {

    const u = this.role.currentUser();

    if (!u?.id) {

      this.campaign.set([]);

      return;

    }

    const list = await firstValueFrom(

      this.api.getSupervisorCampaign(u.id, this.scope.period()).pipe(

        catchError(() => of<SupervisorCelluleCampaignDto[]>([])),

      ),

    );

    this.campaign.set(list);

  }

}


