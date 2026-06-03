import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  Check,
  ChevronRight,
  ClipboardList,
  Download,
  ExternalLink,
  Eye,
  Grid3x3,
  RefreshCw,
  User,
} from 'lucide';
import { catchError, forkJoin, map, of, type Observable } from 'rxjs';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import {
  PrimeCellSaisieBlockComponent,
  type CellSaisieSaveResult,
} from '../components/prime-cell-saisie-block.component';
import { PrimeNavRequestService } from '../services/prime-nav-request.service';
import {
  draftResponseSaisieJson,
  ficheListDraftId,
  ficheResponseSaisieJson,
  PrimeCellPrimeApiService,
  type CellPilotageSummaryDto,
  type EmployeePrimeCellFicheListItemDto,
} from '../services/prime-cell-prime-api.service';
import { PrimeOrgApiService } from '../services/prime-org-api.service';
import { PrimeCellSaisieContextService } from '../services/prime-cell-saisie-context.service';
import { RoleService } from '../state/role.service';
import { parsePrimeSchemaFromDraftJson } from '../lib/prime-cell-schema-merge';
import {
  computeMergedEmployeeFichePreview,
  MERGED_PREVIEW_MISSING_SNAPSHOT_HINT,
  type MergedEmployeeFichePreviewResult,
} from '../lib/prime-employee-fiche-merged-preview';
import {
  buildStyledMergedFicheWorkbook,
  downloadStyledFicheWorkbook,
} from '../lib/prime-fiche-xlsx-export';
import {
  mergedFicheActionsDisabledHint,
  mergedFicheActionsEnabled,
} from '../lib/prime-fiche-distribution-access';

function httpErr(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const b = err.error as { error?: string } | undefined;
    if (b?.error) return b.error;
    return err.message;
  }
  return err instanceof Error ? err.message : 'Erreur';
}

/** Pilote sélectionné + contexte cellule pour le panneau de droite. */
interface PilotSelection {
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
  celluleId: string;
  serviceId: string;
  serviceName: string;
  celluleName: string;
  poleName: string;
  /** Clé brouillon partie commune (`getPoleDraft`). */
  poleId: string;
  linkedTemplateId: string | null;
  fillingStatus: string;
  linkedTemplateDisplayName: string | null;
}

/** Regroupement pilotage : une cellule RH → plusieurs services → pilotes. */
interface PilotageCelluleGroup {
  celluleId: string;
  celluleName: string;
  poleName: string;
  commonPartStatus: string;
  complete: number;
  readyForValidation: number;
  readyCount: number;
  submittedForValidationCount: number;
  inProgress: number;
  notStarted: number;
  aggregateState: string;
  services: CellPilotageSummaryDto[];
}

@Component({
  selector: 'app-prime-fiches-pilotes-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCellSaisieBlockComponent],
  template: `
    <div class="p-4 sm:p-6 pb-20 bg-navy-950 min-h-full">
      <div class="max-w-[1600px] mx-auto space-y-5">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 class="text-2xl font-bold tracking-tight text-slate-100 sm:text-3xl flex items-center gap-2">
              <app-lucide-icon [icon]="icons.board" className="w-8 h-8 text-blue-400 shrink-0" />
              Fiches PRIME — pilotage
            </h1>
            <p class="text-slate-400 mt-2 max-w-2xl text-sm leading-relaxed">
              Choisissez la <strong class="text-slate-200">période</strong> : le brouillon pôle (partie commune RACC/SAV)
              s’applique à toute la cellule. À gauche : <strong class="text-slate-200">Pôle → Cellule → Service → Pilote</strong>
              (pilotes uniquement, depuis les affectations RH). À droite : saisie du pilote sélectionné.
            </p>
          </div>
          <button
            type="button"
            (click)="reload()"
            [disabled]="loading()"
            class="inline-flex items-center gap-2 rounded-lg border border-navy-600 bg-navy-900 px-4 py-2 text-sm font-medium text-slate-200 hover:bg-navy-800 disabled:opacity-50"
          >
            <app-lucide-icon [icon]="icons.refresh" className="w-4 h-4" />
            Actualiser
          </button>
        </div>

        <div class="flex flex-wrap items-end gap-4">
          <div>
            <label class="block text-xs font-medium text-slate-500 mb-1">Période</label>
            <input
              type="month"
              [value]="period()"
              (change)="onPeriodChange($event)"
              class="rounded-lg border border-navy-600 bg-navy-900 px-3 py-2 text-sm text-slate-100"
            />
          </div>
          @if (globalTemplateHint()) {
            <p class="text-xs text-slate-500 max-w-xl pb-1">
              Partie commune liée :
              <span class="text-slate-300 font-medium">{{ globalTemplateHint() }}</span>
            </p>
          }
        </div>

        @if (error()) {
          <div class="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200" role="alert">
            {{ error() }}
          </div>
        }

        @if (pageNotice()) {
          <div
            class="rounded-lg border border-emerald-500/45 bg-emerald-500/15 px-4 py-3 text-sm text-emerald-100 flex items-start gap-2"
            role="status"
          >
            <app-lucide-icon [icon]="icons.check" className="w-5 h-5 shrink-0 mt-0.5" />
            <span>{{ pageNotice() }}</span>
          </div>
        }

        @if (showCommonPartValidationBanner()) {
          <div
            class="rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-100"
            role="status"
          >
            {{ commonPartValidationBannerText() }}
          </div>
        }

        @if (loading()) {
          <div class="flex justify-center py-16">
            <div class="animate-spin rounded-full h-10 w-10 border-2 border-blue-500 border-t-transparent"></div>
          </div>
        } @else {
          <div
            class="flex flex-col lg:flex-row gap-6 lg:items-stretch lg:min-h-[calc(100dvh-12rem)]"
          >
            <aside
              class="lg:w-[min(100%,380px)] shrink-0 flex flex-col rounded-xl border border-navy-700 bg-navy-900/50 overflow-hidden max-h-[52vh] lg:max-h-none"
            >
              <div
                class="px-4 py-3 border-b border-navy-700 text-xs font-semibold uppercase tracking-wide text-slate-500"
              >
                Cellule, services et pilotes
              </div>
              <div class="overflow-y-auto flex-1 p-3 space-y-4">
                @for (grp of pilotageTree(); track grp.celluleId) {
                  <section class="rounded-lg border border-navy-600/80 bg-navy-950/60 overflow-hidden">
                    <div class="flex items-center gap-2 px-3 py-2.5 bg-navy-900 border-b border-navy-700">
                      <span [class]="cellRollupDot(grp.aggregateState)" aria-hidden="true"></span>
                      <div class="min-w-0 flex-1">
                        <div class="text-[10px] uppercase tracking-wide text-slate-500">Cellule</div>
                        <div class="font-semibold text-slate-100 text-sm truncate">{{ grp.celluleName }}</div>
                        @if (grp.poleName) {
                          <div class="text-[11px] text-slate-500 truncate">Pôle : {{ grp.poleName }}</div>
                        }
                        <div class="text-[11px] text-slate-500 mt-0.5">
                          {{ rollupCountsLabel(grp) }}
                        </div>
                        @if (grp.commonPartStatus) {
                          <div class="text-[10px] text-slate-600 mt-0.5">
                            Partie commune : {{ commonPartStatusLabel(grp.commonPartStatus) }}
                          </div>
                        }
                      </div>
                      <span class="text-[10px] font-medium text-slate-500 shrink-0">{{
                        stateShortLabel(grp.aggregateState)
                      }}</span>
                    </div>
                    @for (svc of grp.services; track svc.serviceId) {
                      <div class="border-t border-navy-800">
                        <div class="flex items-center gap-2 px-3 py-2 bg-navy-900/50">
                          <span [class]="cellRollupDot(svc.serviceAggregateState)" aria-hidden="true"></span>
                          <div class="min-w-0 flex-1">
                            <div class="text-[10px] uppercase tracking-wide text-slate-600">Service</div>
                            <div class="font-medium text-slate-200 text-sm truncate">{{ svc.serviceName }}</div>
                            <div class="text-[11px] text-slate-500 mt-0.5">
                              {{ serviceCountsLabel(svc) }}
                            </div>
                          </div>
                        </div>
                        <ul class="divide-y divide-navy-800">
                          @for (emp of employeesForService(svc.serviceId); track emp.employeeId) {
                            <li class="flex items-stretch gap-0.5">
                              <button
                                type="button"
                                (click)="selectPilot(emp, svc)"
                                [class]="pilotRowClass(emp.employeeId) + ' flex-1 min-w-0 rounded-none border-0'"
                              >
                                <span [class]="pilotEmployeeDotClass(emp)" aria-hidden="true"></span>
                                <span class="min-w-0 flex-1 text-left">
                                  <span class="block text-sm font-medium text-slate-100 truncate"
                                    >{{ emp.firstName }} {{ emp.lastName }}</span
                                  >
                                  <span class="block text-[11px] text-slate-500 truncate">{{ emp.email }}</span>
                                </span>
                                <span class="text-[10px] text-slate-500 shrink-0" [title]="pilotValidationTitle(emp)">{{
                                  pilotValidationShort(emp)
                                }}</span>
                                <app-lucide-icon [icon]="icons.chev" className="w-3.5 h-3.5 text-slate-600 shrink-0" />
                              </button>
                              <div class="flex flex-col justify-center gap-0.5 py-1 pr-1 shrink-0 border-l border-navy-800">
                                <button
                                  type="button"
                                  [title]="mergedActionsHint(emp, svc) || 'Aperçu fiche fusionnée (pôle + cellule)'"
                                  (click)="openMergedPreview($event, emp, svc)"
                                  [disabled]="!mergedActionsEnabled(emp, svc)"
                                  class="rounded px-1.5 py-1 text-[10px] font-medium text-blue-300 hover:bg-navy-800 disabled:opacity-30 disabled:pointer-events-none"
                                >
                                  <app-lucide-icon [icon]="icons.eye" className="w-4 h-4 mx-auto" />
                                </button>
                                <button
                                  type="button"
                                  [title]="mergedActionsHint(emp, svc) || 'Télécharger .xlsx (une feuille)'"
                                  (click)="downloadMergedXlsx($event, emp, svc)"
                                  [disabled]="!mergedActionsEnabled(emp, svc)"
                                  class="rounded px-1.5 py-1 text-[10px] font-medium text-emerald-300 hover:bg-navy-800 disabled:opacity-30 disabled:pointer-events-none"
                                >
                                  <app-lucide-icon [icon]="icons.download" className="w-4 h-4 mx-auto" />
                                </button>
                              </div>
                            </li>
                          } @empty {
                            <li class="px-3 py-3 text-xs text-slate-500">Aucun pilote sur ce service.</li>
                          }
                        </ul>
                      </div>
                    }
                  </section>
                }
                @if (pilotageTree().length === 0) {
                  <p class="text-sm text-slate-500 px-2">Aucune cellule ou aucun service dans votre périmètre superviseur.</p>
                }
              </div>
            </aside>

            <main class="flex-1 min-w-0 flex flex-col rounded-xl border border-navy-700 bg-navy-900/30 overflow-hidden">
              <div
                class="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-b border-navy-700 bg-navy-900/60"
              >
                <div class="min-w-0 flex-1 space-y-2">
                  <h2 class="text-sm font-semibold text-slate-200">Saisie cellule</h2>
                  @if (selectedPilot(); as sp) {
                    <div class="flex flex-col sm:flex-row flex-wrap gap-2 min-w-0">
                      @if (sp.poleName) {
                        <div
                          class="min-w-[8rem] flex-1 rounded-lg border border-navy-700/80 bg-navy-900/40 px-3 py-2.5"
                        >
                          <div class="text-[10px] font-semibold uppercase tracking-wide text-slate-500 mb-1">Pôle</div>
                          <div class="text-sm font-medium text-slate-100 truncate">{{ sp.poleName }}</div>
                        </div>
                      }
                      <div
                        class="flex-1 min-w-0 rounded-lg border border-slate-600/80 bg-slate-800/50 px-3 py-2.5 shadow-sm"
                      >
                        <div class="text-[10px] font-semibold uppercase tracking-wide text-slate-500 mb-1">Cellule</div>
                        <div class="flex items-center gap-2 text-sm font-medium text-slate-100">
                          <app-lucide-icon [icon]="icons.grid" className="w-4 h-4 text-slate-400 shrink-0" />
                          <span class="truncate">{{ sp.celluleName }}</span>
                        </div>
                      </div>
                      <div
                        class="min-w-[8rem] flex-1 rounded-lg border border-indigo-500/30 bg-indigo-950/30 px-3 py-2.5"
                      >
                        <div class="text-[10px] font-semibold uppercase tracking-wide text-indigo-300/80 mb-1">Service</div>
                        <div class="text-sm font-medium text-slate-100 truncate">{{ sp.serviceName }}</div>
                      </div>
                      <div
                        class="flex-1 min-w-0 rounded-lg border border-blue-500/35 bg-blue-950/40 px-3 py-2.5 ring-1 ring-inset ring-blue-500/25 shadow-sm"
                      >
                        <div class="text-[10px] font-semibold uppercase tracking-wide text-blue-300/80 mb-1">
                          Pilote
                        </div>
                        <div class="flex items-start gap-2 text-sm text-slate-100">
                          <app-lucide-icon [icon]="icons.user" className="w-4 h-4 text-blue-400 shrink-0 mt-0.5" />
                          <div class="min-w-0">
                            <div class="font-medium truncate">{{ sp.firstName }} {{ sp.lastName }}</div>
                            <div class="text-[11px] text-slate-400 truncate">{{ sp.email }}</div>
                          </div>
                        </div>
                      </div>
                    </div>
                  } @else {
                    <p class="text-xs text-slate-500">Sélectionnez un pilote dans la liste de gauche.</p>
                  }
                </div>
                @if (selectedPilot()) {
                  <button
                    type="button"
                    (click)="openFullPage()"
                    class="inline-flex items-center gap-1.5 rounded-lg border border-navy-600 px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-navy-800 shrink-0"
                  >
                    <app-lucide-icon [icon]="icons.external" className="w-3.5 h-3.5" />
                    Plein écran
                  </button>
                }
              </div>
              <div class="flex-1 overflow-y-auto p-3 sm:p-4">
                @for (p of pilotBlockRows(); track p.employeeId) {
                  <app-prime-cell-saisie-block
                    [employeeId]="p.employeeId"
                    [period]="period()"
                    [linkedTemplateLabel]="p.linkedTemplateDisplayName"
                    [poleId]="p.poleId"
                    [linkedTemplateId]="p.linkedTemplateId"
                    [celluleName]="p.celluleName"
                    [embedded]="true"
                    (saved)="onPilotSaved($event)"
                  />
                }
                @if (!selectedPilot()) {
                  <div
                    class="h-full min-h-[240px] flex flex-col items-center justify-center text-center text-slate-500 text-sm px-6"
                  >
                    <p>Les indicateurs verts, jaunes et rouges indiquent l’avancement de chaque fiche pilote.</p>
                    <p class="mt-2 text-xs">Sans brouillon pôle pour cette période, la saisie affichera une erreur : complétez d’abord la partie commune dans « Fiche PRIME — saisie ».</p>
                  </div>
                }
              </div>
            </main>
          </div>
        }
      </div>
    </div>

    @if (previewOpen()) {
      <div
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60"
        role="dialog"
        aria-modal="true"
        aria-labelledby="prime-pilot-preview-title"
        (click)="closeMergedPreview()"
      >
        <div
          class="max-w-[min(96vw,1400px)] w-full max-h-[min(90vh,900px)] flex flex-col rounded-xl border border-navy-600 bg-navy-950 shadow-xl"
          (click)="$event.stopPropagation()"
        >
          <div class="flex items-center justify-between gap-3 px-4 py-3 border-b border-navy-700 shrink-0">
            <h3 id="prime-pilot-preview-title" class="text-sm font-semibold text-slate-100 truncate">
              {{ previewTitle() }}
            </h3>
            <button
              type="button"
              (click)="closeMergedPreview()"
              class="rounded-lg border border-navy-600 px-3 py-1.5 text-xs font-medium text-slate-200 hover:bg-navy-800 shrink-0"
            >
              Fermer
            </button>
          </div>
          @if (previewBusy()) {
            <div class="flex justify-center py-16 shrink-0">
              <div class="animate-spin rounded-full h-10 w-10 border-2 border-blue-500 border-t-transparent"></div>
            </div>
          } @else {
            @if (previewBanner()) {
              <div
                class="mx-4 mt-3 rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-100 shrink-0"
                role="status"
              >
                {{ previewBanner() }}
              </div>
            }
            @if (previewErrors().length) {
              <div
                class="mx-4 mt-2 rounded-lg border border-rose-500/40 bg-rose-500/10 px-3 py-2 text-xs text-rose-100 max-h-28 overflow-y-auto shrink-0 space-y-0.5"
                role="status"
              >
                @for (er of previewErrors(); track er) {
                  <p>{{ er }}</p>
                }
              </div>
            }
            <div class="flex-1 min-h-0 overflow-auto p-3">
              <table class="text-[11px] border-collapse border border-navy-700 text-slate-200">
                @for (row of previewRows(); track pr; let pr = $index) {
                  <tr>
                    @for (cell of row; track pc; let pc = $index) {
                      <td class="border border-navy-800 px-1 py-0.5 whitespace-nowrap align-top">{{ cell }}</td>
                    }
                  </tr>
                }
              </table>
            </div>
          }
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeFichesPilotesPageComponent implements OnInit {
  private readonly api = inject(PrimeCellPrimeApiService);
  private readonly orgApi = inject(PrimeOrgApiService);
  private readonly role = inject(RoleService);
  private readonly nav = inject(PrimeNavRequestService);
  private readonly cellCtx = inject(PrimeCellSaisieContextService);

  readonly icons = {
    board: ClipboardList,
    refresh: RefreshCw,
    chev: ChevronRight,
    check: Check,
    external: ExternalLink,
    grid: Grid3x3,
    user: User,
    eye: Eye,
    download: Download,
  };

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly summary = signal<CellPilotageSummaryDto[]>([]);
  readonly period = signal(this.defaultPeriod());
  readonly employeesByServiceId = signal<Record<string, EmployeePrimeCellFicheListItemDto[]>>({});
  readonly selectedPilot = signal<PilotSelection | null>(null);

  readonly previewOpen = signal(false);
  readonly previewBusy = signal(false);
  readonly previewTitle = signal('');
  readonly previewRows = signal<string[][]>([]);
  readonly previewErrors = signal<string[]>([]);
  readonly previewBanner = signal<string | null>(null);
  readonly pageNotice = signal<string | null>(null);

  readonly pilotBlockRows = computed(() => {
    const s = this.selectedPilot();
    return s ? [s] : [];
  });

  /** Premier libellé de template lié trouvé dans le résumé (info globale). */
  readonly globalTemplateHint = computed(() => {
    const rows = this.summary();
    const withName = rows.find((r) => (r.linkedTemplateDisplayName ?? '').trim().length > 0);
    if (withName?.linkedTemplateDisplayName?.trim()) return withName.linkedTemplateDisplayName.trim();
    const withId = rows.find((r) => (r.linkedTemplateId ?? '').trim().length > 0);
    return withId?.linkedTemplateId?.trim() ?? null;
  });

  readonly pilotageTree = computed((): PilotageCelluleGroup[] => {
    const rows = this.summary();
    const byCell = new Map<string, CellPilotageSummaryDto[]>();
    for (const r of rows) {
      const cid = r.celluleId?.trim() || '';
      if (!cid) continue;
      const list = byCell.get(cid) ?? [];
      list.push(r);
      byCell.set(cid, list);
    }
    const groups: PilotageCelluleGroup[] = [];
    for (const [celluleId, services] of byCell) {
      const sorted = [...services].sort((a, b) => a.serviceName.localeCompare(b.serviceName));
      const first = sorted[0];
      let complete = 0;
      let readyForValidation = 0;
      let readyCount = 0;
      let submittedForValidationCount = 0;
      let inProgress = 0;
      let notStarted = 0;
      for (const s of sorted) {
        complete += s.complete;
        readyForValidation += s.readyForValidation ?? 0;
        readyCount += s.readyCount ?? 0;
        submittedForValidationCount += s.submittedForValidationCount ?? 0;
        inProgress += s.inProgress;
        notStarted += s.notStarted;
      }
      const total = complete + inProgress + notStarted;
      let aggregateState = 'Empty';
      if (total > 0) {
        if (notStarted === total) aggregateState = 'NotStarted';
        else if (complete === total) aggregateState = 'Done';
        else aggregateState = 'InProgress';
      }
      groups.push({
        celluleId,
        celluleName: (first?.celluleName ?? '').trim() || celluleId,
        poleName: (first?.poleName ?? '').trim(),
        commonPartStatus: (first?.commonPartStatus ?? '').trim(),
        complete,
        readyForValidation,
        readyCount,
        submittedForValidationCount,
        inProgress,
        notStarted,
        aggregateState,
        services: sorted,
      });
    }
    return groups.sort((a, b) => a.celluleName.localeCompare(b.celluleName));
  });

  readonly showCommonPartValidationBanner = computed(() => {
    const tree = this.pilotageTree();
    if (tree.length === 0) return false;
    const complete = tree.reduce((a, g) => a + g.complete, 0);
    const ready = tree.reduce((a, g) => a + g.readyForValidation, 0);
    return complete > 0 && ready === 0;
  });

  ngOnInit(): void {
    // Si une période a été demandée par la liste fiches communes, l'appliquer
    // avant le premier chargement.
    const requested = this.nav.requestedPeriod();
    if (requested && /^\d{4}-\d{2}$/.test(requested)) {
      this.period.set(requested);
      this.nav.clearRequestedPeriod();
    }
    void this.reload();
  }

  defaultPeriod(): string {
    const d = new Date();
    d.setDate(1);
    d.setMonth(d.getMonth() - 1);
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    return `${y}-${m}`;
  }

  onPeriodChange(ev: Event): void {
    const v = (ev.target as HTMLInputElement).value;
    if (!v) return;
    this.pageNotice.set(null);
    this.period.set(v);
    this.selectedPilot.set(null);
    void this.reload();
  }

  employeesForService(serviceId: string): EmployeePrimeCellFicheListItemDto[] {
    return this.employeesByServiceId()[serviceId] ?? [];
  }

  reload(): void {
    void this.loadPilotData(true);
  }

  /** Rafraîchit la liste sans masquer l’écran (après enregistrement cellule). */
  private refreshPilotListOnly(): void {
    void this.loadPilotData(false);
  }

  private loadPilotData(fullPageSpinner: boolean): void {
    const u = this.role.currentUser();
    if (fullPageSpinner) {
      this.loading.set(true);
      this.error.set(null);
    }
    forkJoin({
      summary: this.api.cellsSummary(u.id, this.period()),
      scope: this.orgApi.getSupervisorScope(u.id).pipe(catchError(() => of([]))),
    }).subscribe({
      next: ({ summary: rows }) => {
        this.summary.set(rows);
        if (rows.length === 0) {
          this.employeesByServiceId.set({});
          if (fullPageSpinner) this.loading.set(false);
          this.syncSelectionAfterReload();
          return;
        }
        const serviceIds = [...new Set(rows.map((c) => c.serviceId))];
        forkJoin(
          serviceIds.map((sid) =>
            this.api.listEmployeeFiches(this.period(), u.id, { serviceId: sid }).pipe(
              map((emps) => ({ id: sid, emps })),
              catchError(() => of({ id: sid, emps: [] as EmployeePrimeCellFicheListItemDto[] })),
            ),
          ),
        ).subscribe({
          next: (parts) => {
            const m: Record<string, EmployeePrimeCellFicheListItemDto[]> = {};
            for (const p of parts) m[p.id] = p.emps;
            this.employeesByServiceId.set(m);
            if (fullPageSpinner) this.loading.set(false);
            this.syncSelectionAfterReload();
            this.backfillMergedTotals(rows);
          },
          error: (e) => {
            this.error.set(httpErr(e));
            if (fullPageSpinner) this.loading.set(false);
          },
        });
      },
      error: (e) => {
        this.error.set(httpErr(e));
        if (fullPageSpinner) this.loading.set(false);
      },
    });
  }

  private syncSelectionAfterReload(): void {
    const cur = this.selectedPilot();
    if (!cur) return;
    const list = this.employeesForService(cur.serviceId);
    const updated = list.find((e) => e.employeeId === cur.employeeId);
    if (!updated) {
      this.selectedPilot.set(null);
      return;
    }
    this.selectedPilot.set({
      ...cur,
      fillingStatus: updated.fillingStatus,
    });
  }

  selectPilot(emp: EmployeePrimeCellFicheListItemDto, cell: CellPilotageSummaryDto): void {
    this.pageNotice.set(null);
    const name =
      (cell.linkedTemplateDisplayName ?? '').trim() ||
      (cell.linkedTemplateId ?? '').trim() ||
      null;
    this.selectedPilot.set({
      employeeId: emp.employeeId,
      firstName: emp.firstName,
      lastName: emp.lastName,
      email: emp.email,
      celluleId: cell.celluleId,
      serviceId: cell.serviceId,
      serviceName: cell.serviceName,
      celluleName: (cell.celluleName ?? '').trim() || cell.celluleId,
      poleName: (cell.poleName ?? '').trim(),
      poleId: cell.celluleId,
      linkedTemplateId: (cell.linkedTemplateId ?? '').trim() || null,
      fillingStatus: emp.fillingStatus,
      linkedTemplateDisplayName: name,
    });
  }

  pilotRowClass(employeeId: string): string {
    const base =
      'w-full flex items-center gap-2 px-3 py-2.5 text-left transition-colors hover:bg-navy-800/80 ';
    const sel = this.selectedPilot()?.employeeId === employeeId;
    return sel ? base + 'bg-blue-600/15 ring-1 ring-inset ring-blue-500/40' : base;
  }

  mergedActionsEnabled(emp: EmployeePrimeCellFicheListItemDto, cell: CellPilotageSummaryDto): boolean {
    return mergedFicheActionsEnabled(
      this.role.currentRole() as string,
      emp,
      cell,
      !!ficheListDraftId(emp),
    );
  }

  mergedActionsHint(emp: EmployeePrimeCellFicheListItemDto, cell: CellPilotageSummaryDto): string {
    return mergedFicheActionsDisabledHint(
      this.role.currentRole() as string,
      emp,
      cell,
      !!ficheListDraftId(emp),
    );
  }

  private fetchMerged$(
    emp: EmployeePrimeCellFicheListItemDto,
    cell: CellPilotageSummaryDto,
  ): Observable<MergedEmployeeFichePreviewResult> {
    const u = this.role.currentUser();
    const tid = (cell.linkedTemplateId ?? '').trim();
    return forkJoin({
      draft: this.api.getPoleDraft(u.id, cell.celluleId.trim(), this.period(), tid),
      fiche: this.api.getFicheForEmployee(u.id, emp.employeeId, this.period(), tid),
      inds: this.api.getIndicators(emp.serviceId, u.id),
    }).pipe(
      map(({ draft, fiche, inds }) => {
        const schema = parsePrimeSchemaFromDraftJson(draft.schemaJson);
        return computeMergedEmployeeFichePreview({
          schema,
          poleSaisieJson: draftResponseSaisieJson(draft),
          cellSaisieJson: ficheResponseSaisieJson(fiche),
          templateCalcSnapshotJson: draft.templateCalcSnapshotJson,
          indicators: inds,
          templateId: tid,
        });
      }),
    );
  }

  /** Persiste les montants Prime/Challenge/Total calcules dans l'apercu fusionne sur la fiche. */
  private persistMergedTotals(emp: EmployeePrimeCellFicheListItemDto, res: MergedEmployeeFichePreviewResult): void {
    const ficheId = (emp.ficheId ?? '').trim();
    if (!ficheId || !res.totals) return;
    const u = this.role.currentUser();
    this.api.persistFicheAmounts(ficheId, u.id, res.totals).subscribe({ error: () => undefined });
  }

  /**
   * Recalcule et persiste les montants des fiches deja completes pour que la synthese et la
   * validation affichent les vrais montants sans action manuelle. Best-effort, erreurs ignorees.
   */
  private backfillMergedTotals(cells: CellPilotageSummaryDto[]): void {
    const byService = this.employeesByServiceId();
    const role = this.role.currentRole() as string;
    const tasks: Observable<unknown>[] = [];
    for (const cell of cells) {
      const emps = byService[cell.serviceId] ?? [];
      for (const emp of emps) {
        const ficheId = (emp.ficheId ?? '').trim();
        if (!ficheId) continue;
        if ((emp.fillingStatus ?? '').trim().toLowerCase() !== 'complete') continue;
        if (!mergedFicheActionsEnabled(role, emp, cell, !!ficheListDraftId(emp))) continue;
        tasks.push(
          this.fetchMerged$(emp, cell).pipe(
            map((res) => {
              if (res.totals) this.persistMergedTotals(emp, res);
              return null;
            }),
            catchError(() => of(null)),
          ),
        );
      }
    }
    if (tasks.length === 0) return;
    forkJoin(tasks).subscribe({ error: () => undefined });
  }

  openMergedPreview(ev: Event, emp: EmployeePrimeCellFicheListItemDto, cell: CellPilotageSummaryDto): void {
    ev.stopPropagation();
    if (!this.mergedActionsEnabled(emp, cell)) return;
    this.previewOpen.set(true);
    this.previewBusy.set(true);
    this.previewRows.set([]);
    this.previewErrors.set([]);
    this.previewBanner.set(null);
    this.previewTitle.set(`Aperçu — ${emp.firstName} ${emp.lastName} — ${this.period()}`);
    this.fetchMerged$(emp, cell).subscribe({
      next: (res) => {
        this.persistMergedTotals(emp, res);
        this.previewRows.set(res.rows);
        this.previewErrors.set(res.errors);
        if (res.missingSnapshot) this.previewBanner.set(MERGED_PREVIEW_MISSING_SNAPSHOT_HINT);
        else if (!res.rows.length && !res.errors.length) this.previewBanner.set('Aucune donnée à afficher.');
        else this.previewBanner.set(null);
        this.previewBusy.set(false);
      },
      error: (e) => {
        this.previewBanner.set(httpErr(e));
        this.previewBusy.set(false);
      },
    });
  }

  downloadMergedXlsx(ev: Event, emp: EmployeePrimeCellFicheListItemDto, cell: CellPilotageSummaryDto): void {
    ev.stopPropagation();
    if (!this.mergedActionsEnabled(emp, cell)) return;
    this.fetchMerged$(emp, cell).subscribe({
      next: (res) => {
        this.persistMergedTotals(emp, res);
        if (res.missingSnapshot) {
          window.alert(MERGED_PREVIEW_MISSING_SNAPSHOT_HINT);
          return;
        }
        if (!res.rows.length) {
          window.alert(res.errors[0] ?? 'Export impossible — grille vide.');
          return;
        }
        if (!res.effectiveSchema) {
          window.alert('Schéma indisponible : impossible de générer le livrable stylé.');
          return;
        }
        const sheetName =
          (res.previewSheetName || 'Fiche_PRIME').replace(/[:\\/?*[\]]/g, '_').slice(0, 31) || 'Fiche_PRIME';
        const safe =
          `${emp.lastName}_${emp.firstName}_${this.period()}`.replace(/[<>:"/\\|?*]+/g, '_').trim() || 'fiche';
        void buildStyledMergedFicheWorkbook(res.rows, res.effectiveSchema, sheetName)
          .then((wb) => downloadStyledFicheWorkbook(wb, `PRIME_fiche_${safe}.xlsx`))
          .catch((e: unknown) => window.alert(httpErr(e)));
      },
      error: (e) => window.alert(httpErr(e)),
    });
  }

  closeMergedPreview(): void {
    this.previewOpen.set(false);
    this.previewBusy.set(false);
    this.previewBanner.set(null);
    this.previewErrors.set([]);
    this.previewRows.set([]);
  }

  pilotEmployeeDotClass(emp: EmployeePrimeCellFicheListItemDto): string {
    const val = (emp.validationStatus ?? 'AwaitingData').trim().toLowerCase();
    const fill = emp.fillingStatus.trim().toLowerCase();
    const green =
      'inline-block h-2.5 w-2.5 shrink-0 rounded-full bg-emerald-500 shadow-[0_0_0_1px_rgba(16,185,129,0.35)]';
    const greenSoft =
      'inline-block h-2.5 w-2.5 shrink-0 rounded-full bg-emerald-400/90 shadow-[0_0_0_1px_rgba(52,211,153,0.35)]';
    const amber =
      'inline-block h-2.5 w-2.5 shrink-0 rounded-full bg-amber-400 shadow-[0_0_0_1px_rgba(251,191,36,0.35)]';
    const rose =
      'inline-block h-2.5 w-2.5 shrink-0 rounded-full bg-rose-500 shadow-[0_0_0_1px_rgba(244,63,94,0.35)]';
    if (emp.isReadyForValidation === true || val === 'pending') return green;
    if (fill === 'complete') return greenSoft;
    if (fill === 'inprogress') return amber;
    return rose;
  }

  rollupCountsLabel(grp: PilotageCelluleGroup): string {
    const ready = grp.readyCount ?? 0;
    const submitted = grp.submittedForValidationCount ?? 0;
    return `${grp.complete} cellule OK · ${ready} prête(s) · ${submitted} soumise(s) validation · ${grp.inProgress} en cours · ${grp.notStarted} pas commencé`;
  }

  serviceCountsLabel(svc: CellPilotageSummaryDto): string {
    const ready = svc.readyCount ?? 0;
    const submitted = svc.submittedForValidationCount ?? 0;
    return `${svc.complete} cellule OK · ${ready} prête(s) · ${submitted} soumise(s) validation · ${svc.inProgress} en cours · ${svc.notStarted} pas commencé`;
  }

  commonPartStatusLabel(status: string): string {
    const s = status.trim().toLowerCase();
    if (s === 'validated') return 'Validée';
    if (s === 'draft') return 'Brouillon';
    return status || '—';
  }

  commonPartValidationBannerText(): string {
    return 'Des pilotes ont la partie cellule complète, mais la partie commune n’est pas encore validée. Validez-la dans « Fiche PRIME — saisie » (liste fiches communes) pour envoyer les fiches au référent technique.';
  }

  cellRollupDot(state: string): string {
    const s = state.toLowerCase();
    if (s === 'done')
      return 'inline-block h-3 w-3 shrink-0 rounded-full bg-emerald-500 ring-2 ring-emerald-500/30';
    if (s === 'inprogress')
      return 'inline-block h-3 w-3 shrink-0 rounded-full bg-amber-400 ring-2 ring-amber-400/30';
    if (s === 'empty' || s === 'notstarted')
      return 'inline-block h-3 w-3 shrink-0 rounded-full bg-slate-600 ring-2 ring-slate-500/25';
    return 'inline-block h-3 w-3 shrink-0 rounded-full bg-rose-500 ring-2 ring-rose-500/30';
  }

  stateShortLabel(state: string): string {
    const s = state.toLowerCase();
    if (s === 'done') return 'OK';
    if (s === 'inprogress') return 'Encours';
    if (s === 'empty') return 'Vide';
    if (s === 'notstarted') return 'NS';
    return 'À faire';
  }

  statusShort(st: string): string {
    const s = st.toLowerCase();
    if (s === 'complete') return 'OK';
    if (s === 'inprogress') return '…';
    return '·';
  }

  pilotValidationShort(emp: EmployeePrimeCellFicheListItemDto): string {
    const val = (emp.validationStatus ?? 'AwaitingData').trim().toLowerCase();
    const fill = emp.fillingStatus.trim().toLowerCase();
    if (emp.isReadyForValidation === true && val === 'pending') return 'Valid.';
    if (val === 'pending') return 'Valid.';
    if (fill === 'complete' && emp.isReadyForValidation !== true) return 'Cellule OK';
    if (emp.isReadyForValidation === true) return 'Prête';
    return this.statusShort(emp.fillingStatus);
  }

  pilotValidationTitle(emp: EmployeePrimeCellFicheListItemDto): string {
    const val = (emp.validationStatus ?? 'AwaitingData').trim().toLowerCase();
    const fill = emp.fillingStatus.trim().toLowerCase();
    if (emp.isReadyForValidation === true && val === 'pending') {
      return 'Fiche complète — en attente du premier valideur (workflow admin)';
    }
    if (val === 'pending') return 'Fiche soumise au workflow de validation';
    if (fill === 'complete' && emp.isReadyForValidation !== true) {
      return 'Partie cellule complète — validez la partie commune pour lancer la validation';
    }
    if (emp.isReadyForValidation === true) {
      return 'Prête — soumission au workflow en attente. Actualisez la page ; la fiche doit passer en « Valid. » puis apparaître chez le référent technique.';
    }
    return this.statusShort(emp.fillingStatus);
  }

  onPilotSaved(result: CellSaisieSaveResult): void {
    this.pageNotice.set(result.message);
    this.refreshPilotListOnly();
  }

  openFullPage(): void {
    const p = this.selectedPilot();
    if (!p) return;
    this.cellCtx.setContext(p.employeeId, this.period(), {
      templateId: p.linkedTemplateId,
      poleId: p.poleId,
      celluleName: p.celluleName,
    });
    this.nav.requestView('/prime-saisie-cellule');
  }
}
