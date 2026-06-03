import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { catchError, forkJoin, of } from 'rxjs';
import { CheckCircle2, FileSpreadsheet, Save } from 'lucide';
import {
  CHALLENGE_FIELD_LABELS,
  PRIME_INPUT_FIELD_CLASS,
  PRIME_KPI_GRID_CLASS,
  PRIME_FIELD_LABELS,
  customBandHeading,
} from '../lib/prime-fiche-sector-field-meta';
import {
  applyIndicatorPonderationsToDynamic,
  buildCellSaisieJsonV2,
  buildDerivedCellTemplateLines,
  cellRowPayloadForJson,
  getCellTemplateLinesOrDerived,
  hydrateDynamicFromCellRowFlat,
  matchIndicatorToTemplateLine,
  parseCellSaisieJson,
  parsePrimeSchemaFromDraftJson,
  schemaHasExcelNativeCellRows,
  templateLineForCellIndicator,
} from '../lib/prime-cell-schema-merge';
import {
  emptySecteurPairValues,
  isEmptyOrNonNegativeNumberString,
  sanitizeNonNegativeNumberInput,
  type PrimeFicheSecteurPairValues,
} from '../models/prime-fiche-ligne.model';
import {
  hasNegativeDynamicValues,
  ligneDynamicFromTemplateLine,
  type PrimeFicheLigneDynamic,
  type PrimeFicheTemplateLine,
} from '../models/prime-fiche-template.schema';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { PrimeCardComponent } from './prime-card.component';
import { PrimeNavRequestService } from '../services/prime-nav-request.service';
import {
  ficheDraftIdString,
  ficheResponseSaisieJson,
  PrimeCellPrimeApiService,
  type CellulePrimeIndicatorDto,
  type EmployeePrimeCellFicheDto,
  type SupervisorPolePrimeDraftDto,
} from '../services/prime-cell-prime-api.service';
import { PrimeCellSaisieContextService } from '../services/prime-cell-saisie-context.service';
import { RoleService } from '../state/role.service';
import type { Employee } from '../models';

function httpErr(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const b = err.error as { error?: string } | undefined;
    if (b?.error) return b.error;
    return err.message;
  }
  return err instanceof Error ? err.message : 'Erreur';
}

export interface CellIndicatorRun {
  indicator: CellulePrimeIndicatorDto;
  templateLine: PrimeFicheTemplateLine;
  subtitle: string | null;
  warnings: string[];
}

export interface CellSaisieSaveResult {
  message: string;
  fillingStatus: string;
  validationStatus: string;
  isReadyForValidation: boolean;
}

@Component({
  selector: 'app-prime-cell-saisie-block',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent],
  template: `
    <div [class]="embedded() ? 'space-y-4 pb-8' : 'p-4 sm:p-6 max-w-3xl mx-auto pb-20 space-y-6'">
      @if (!embedded()) {
        <header class="space-y-2">
          <h2 class="text-xl font-bold text-primary flex items-center gap-2">
            <app-lucide-icon [icon]="icons.sheet" className="w-7 h-7 text-blue-600 shrink-0" />
            Saisie — partie cellule
          </h2>
          <p class="text-sm text-muted max-w-prose leading-relaxed">
            Grille alignée sur le gabarit partie « Cellule » du brouillon pôle. Les pondérations % proviennent de la
            configuration indicateurs (lecture seule).
          </p>
        </header>
      }

      <app-prime-card [title]="employeeTitle()" [description]="blockSubtitle()">
        <div class="flex flex-wrap gap-3 text-sm text-muted">
          <span>Statut fiche : <strong class="text-primary">{{ fillingStatus() }}</strong></span>
        </div>
      </app-prime-card>

      @if (schemaBanner()) {
        <div
          [class]="bannerIsInfo()
            ? 'rounded-lg border border-emerald-500/40 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-900 dark:text-emerald-100'
            : 'rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-900 dark:text-amber-100'"
          role="status"
        >
          {{ schemaBanner() }}
        </div>
      }

      @if (runWarnings().length) {
        <div
          class="rounded-lg border border-slate-600/60 bg-slate-800/40 px-4 py-3 text-xs text-slate-200 space-y-1"
          role="status"
        >
          @for (w of runWarnings(); track w) {
            <p>{{ w }}</p>
          }
        </div>
      }

      @if (loadError()) {
        <div class="rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-primary" role="alert">
          {{ loadError() }}
        </div>
      } @else if (loading()) {
        <div class="flex justify-center py-12 text-muted text-sm">Chargement…</div>
      } @else if (hasNoActiveIndicators()) {
        <app-prime-card
          title="Aucun indicateur configuré"
          description="Configurez d’abord les indicateurs pour le service de cet employé (écran « Indicateurs PRIME »)."
        >
          <button
            type="button"
            (click)="goIndicators()"
            class="rounded-lg border border-blue-500/50 bg-blue-600/15 px-4 py-2 text-sm font-semibold text-blue-700 dark:text-blue-300"
          >
            Ouvrir la configuration indicateurs
          </button>
        </app-prime-card>
      } @else {
        <div class="space-y-4">
          <div
            class="rounded-xl border border-default bg-card p-4 sm:p-5 shadow-sm space-y-4"
          >
            <h3 class="text-sm font-semibold text-primary">Plafonds (pilote)</h3>
            <p class="text-xs text-muted">
              Montants ou seuils plafond saisis une fois par fiche pilote (partie cellule).
            </p>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-muted mb-2">Plafond Prime</label>
                <input
                  type="number"
                  min="0"
                  step="any"
                  [value]="plafondPrime()"
                  (input)="onPlafondPrimeInput($any($event.target).value)"
                  [class]="inputFieldClass"
                  autocomplete="off"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-muted mb-2">Plafond Challenge</label>
                <input
                  type="number"
                  min="0"
                  step="any"
                  [value]="plafondChallenge()"
                  (input)="onPlafondChallengeInput($any($event.target).value)"
                  [class]="inputFieldClass"
                  autocomplete="off"
                />
              </div>
            </div>
          </div>

          @for (run of runs(); track run.indicator.id) {
            <div
              class="rounded-xl border border-default bg-card p-4 sm:p-5 shadow-sm space-y-4 hover:border-blue-500/30 transition-colors"
            >
              <div class="flex flex-wrap items-baseline justify-between gap-2">
                <div class="min-w-0">
                  <h3 class="text-base font-semibold text-primary leading-snug">{{ run.indicator.label }}</h3>
                  @if (run.subtitle) {
                    <p class="text-xs text-muted mt-1">{{ run.subtitle }}</p>
                  }
                </div>
              </div>
              @for (s of run.templateLine.secteurs; track s.sectorIndex) {
                <div class="space-y-6 rounded-lg border border-default/80 bg-card/40 p-4">
                  <h4 class="text-sm font-semibold text-primary border-b border-default pb-2">
                    {{ s.label }}
                  </h4>
                  <div class="flex flex-col gap-4">
                    <div
                      class="space-y-3 rounded-lg border border-blue-500/35 bg-blue-600/10 p-4 dark:bg-blue-500/15"
                    >
                      <h5 class="text-xs font-semibold uppercase tracking-wide text-primary">Prime (Secteur)</h5>
                      <div [class]="kpiGridClass">
                        @for (fl of primeFieldLabels; track fl.key) {
                          <div class="min-w-0">
                            <label class="mb-1 block text-sm font-medium text-muted">{{ fl.label }}</label>
                            @if (fl.key === 'ponderationPrime') {
                              <div
                                class="w-full px-3 py-2 rounded-lg border border-default bg-input/60 text-primary text-sm"
                              >
                                {{ dynSector(run.indicator.id, s.sectorIndex)[fl.key] || '—' }}
                              </div>
                              <p class="text-[10px] text-muted mt-1">Pondération issue de la config indicateur (%).</p>
                            } @else {
                              <input
                                type="number"
                                step="any"
                                min="0"
                                [class]="inputFieldClass"
                                [value]="dynSector(run.indicator.id, s.sectorIndex)[fl.key]"
                                (input)="onSectorInput(run.indicator.id, s.sectorIndex, fl.key, $any($event.target).value)"
                              />
                            }
                          </div>
                        }
                      </div>
                    </div>
                    <div
                      class="space-y-3 rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 dark:bg-amber-500/15"
                    >
                      <h5 class="text-xs font-semibold uppercase tracking-wide text-primary">Challenge (Secteur)</h5>
                      <div [class]="kpiGridClass">
                        @for (fl of challengeFieldLabels; track fl.key) {
                          <div class="min-w-0">
                            <label class="mb-1 block text-sm font-medium text-muted">{{ fl.label }}</label>
                            @if (fl.key === 'ponderationChallenge') {
                              <div
                                class="w-full px-3 py-2 rounded-lg border border-default bg-input/60 text-primary text-sm"
                              >
                                {{ dynSector(run.indicator.id, s.sectorIndex)[fl.key] || '—' }}
                              </div>
                              <p class="text-[10px] text-muted mt-1">Pondération issue de la config indicateur (%).</p>
                            } @else {
                              <input
                                type="number"
                                step="any"
                                min="0"
                                [class]="inputFieldClass"
                                [value]="dynSector(run.indicator.id, s.sectorIndex)[fl.key]"
                                (input)="onSectorInput(run.indicator.id, s.sectorIndex, fl.key, $any($event.target).value)"
                              />
                            }
                          </div>
                        }
                      </div>
                    </div>
                    @if (s.customKpis?.length) {
                      <div
                        class="space-y-3 rounded-lg border border-violet-500/40 bg-violet-500/10 p-4 dark:bg-violet-900/20"
                      >
                        <h5 class="text-xs font-semibold uppercase tracking-wide text-primary">
                          {{ customBandHeading(s) }}
                        </h5>
                        <p class="text-[11px] text-muted">KPI additionnels (même logique que la saisie pôle).</p>
                        <div [class]="kpiGridClass">
                          @for (ck of s.customKpis; track ck.id) {
                            <div class="min-w-0">
                              <label class="mb-1 block text-sm font-medium text-muted">{{ ck.header }}</label>
                              <input
                                type="number"
                                step="any"
                                min="0"
                                [class]="inputFieldClass"
                                [value]="dynCustom(run.indicator.id, s.sectorIndex, ck.id)"
                                (input)="
                                  onCustomInput(run.indicator.id, s.sectorIndex, ck.id, $any($event.target).value)
                                "
                              />
                            </div>
                          }
                        </div>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>
          }
        </div>

        @if (saveBanner()) {
          <div
            class="rounded-lg border border-emerald-500/45 bg-emerald-500/15 px-4 py-3 text-sm text-emerald-900 dark:text-emerald-100 flex items-start gap-2"
            role="status"
          >
            <app-lucide-icon [icon]="icons.check" className="w-5 h-5 shrink-0 mt-0.5" />
            <span>{{ saveBanner() }}</span>
          </div>
        }
        @if (saveError()) {
          <div
            class="rounded-lg border border-rose-500/45 bg-rose-500/10 px-4 py-3 text-sm text-rose-700 dark:text-rose-200"
            role="alert"
          >
            {{ saveError() }}
          </div>
        }

        <div
          [class]="
            embedded()
              ? 'flex flex-wrap items-center gap-3 pt-4 border-t border-default mt-4'
              : 'sticky bottom-4 flex flex-wrap items-center gap-3 pt-4'
          "
        >
          <button
            type="button"
            (click)="save()"
            [disabled]="saving()"
            class="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm sm:text-base font-semibold text-white shadow-lg hover:bg-blue-700 disabled:opacity-50"
          >
            <app-lucide-icon [icon]="icons.save" className="w-5 h-5" />
            {{ saving() ? 'Enregistrement…' : 'Enregistrer la saisie cellule' }}
          </button>
        </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeCellSaisieBlockComponent implements OnInit {
  private readonly api = inject(PrimeCellPrimeApiService);
  private readonly http = inject(HttpClient);
  private readonly role = inject(RoleService);
  private readonly nav = inject(PrimeNavRequestService);
  private readonly cellCtx = inject(PrimeCellSaisieContextService);

  readonly icons = { sheet: FileSpreadsheet, save: Save, check: CheckCircle2 };

  readonly employeeId = input.required<string>();
  readonly period = input.required<string>();
  /** Libellé du fichier / template issu du résumé pilotage (partie commune). */
  readonly linkedTemplateLabel = input<string | null>(null);
  readonly poleId = input<string | null>(null);
  readonly linkedTemplateId = input<string | null>(null);
  readonly celluleName = input<string | null>(null);
  readonly embedded = input(false);
  readonly saved = output<CellSaisieSaveResult>();

  readonly primeFieldLabels = PRIME_FIELD_LABELS;
  readonly challengeFieldLabels = CHALLENGE_FIELD_LABELS;
  readonly inputFieldClass = PRIME_INPUT_FIELD_CLASS;
  readonly kpiGridClass = PRIME_KPI_GRID_CLASS;
  readonly customBandHeading = customBandHeading;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly indicators = signal<CellulePrimeIndicatorDto[]>([]);
  readonly runs = signal<CellIndicatorRun[]>([]);
  readonly poleDraftId = signal<string | null>(null);
  readonly fillingStatus = signal<string>('NotStarted');
  readonly celluleId = signal<string | null>(null);
  readonly dynamicByIndicator = signal<Record<string, PrimeFicheLigneDynamic>>({});
  readonly plafondPrime = signal('');
  readonly plafondChallenge = signal('');
  readonly schemaBanner = signal<string | null>(null);
  readonly bannerIsInfo = signal(false);
  readonly saving = signal(false);
  readonly saveBanner = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly employeeLabel = signal<string>('');

  readonly employeeTitle = computed(() => {
    const id = this.employeeId();
    const lbl = this.employeeLabel();
    return lbl ? `${lbl}` : `Employé ${id}`;
  });

  readonly hasNoActiveIndicators = computed(() => !this.indicators().some((i) => i.isActive));

  readonly runWarnings = computed(() => {
    const ws: string[] = [];
    for (const r of this.runs()) {
      for (const w of r.warnings) {
        if (w && !ws.includes(w)) ws.push(w);
      }
    }
    return ws;
  });

  readonly blockSubtitle = computed(() => {
    const t = this.linkedTemplateLabel();
    return (
      `Période ${this.period()}` +
      (t?.trim() ? ` · ${t.trim()}` : ' · template issu de la partie commune (même période)')
    );
  });

  ngOnInit(): void {
    void this.runLoad(this.employeeId(), this.period());
  }

  goIndicators(): void {
    this.nav.requestView('/prime-cellule-indicateurs');
  }

  dynRow(indicatorId: string): PrimeFicheLigneDynamic {
    return this.dynamicByIndicator()[indicatorId] ?? { repartitionRdv: '', secteurValues: [] };
  }

  dynSector(indicatorId: string, sectorIndex: number): PrimeFicheSecteurPairValues {
    const row = this.dynRow(indicatorId);
    return row.secteurValues[sectorIndex]?.core ?? emptySecteurPairValues();
  }

  dynCustom(indicatorId: string, sectorIndex: number, customId: string): string {
    const row = this.dynRow(indicatorId);
    return row.secteurValues[sectorIndex]?.custom[customId] ?? '';
  }

  onPlafondPrimeInput(value: string): void {
    this.plafondPrime.set(sanitizeNonNegativeNumberInput(value));
    this.saveBanner.set(null);
  }

  onPlafondChallengeInput(value: string): void {
    this.plafondChallenge.set(sanitizeNonNegativeNumberInput(value));
    this.saveBanner.set(null);
  }

  onSectorInput(indicatorId: string, sectorIndex: number, field: keyof PrimeFicheSecteurPairValues, value: string): void {
    if (field === 'ponderationPrime' || field === 'ponderationChallenge') return;
    const next = sanitizeNonNegativeNumberInput(value);
    this.dynamicByIndicator.update((m) => {
      const cur = { ...(m[indicatorId] ?? this.dynRow(indicatorId)) };
      const sects = [...cur.secteurValues];
      const prev = sects[sectorIndex] ?? { core: emptySecteurPairValues(), custom: {} };
      sects[sectorIndex] = { ...prev, core: { ...prev.core, [field]: next } };
      return { ...m, [indicatorId]: { ...cur, secteurValues: sects } };
    });
    this.saveBanner.set(null);
  }

  onCustomInput(indicatorId: string, sectorIndex: number, customId: string, value: string): void {
    const next = sanitizeNonNegativeNumberInput(value);
    this.dynamicByIndicator.update((m) => {
      const cur = { ...(m[indicatorId] ?? this.dynRow(indicatorId)) };
      const sects = [...cur.secteurValues];
      const prev = sects[sectorIndex] ?? { core: emptySecteurPairValues(), custom: {} };
      const custom = { ...prev.custom, [customId]: next };
      sects[sectorIndex] = { ...prev, custom };
      return { ...m, [indicatorId]: { ...cur, secteurValues: sects } };
    });
    this.saveBanner.set(null);
  }

  private runLoad(empId: string, period: string): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.saveBanner.set(null);
    this.saveError.set(null);
    this.schemaBanner.set(null);
    this.bannerIsInfo.set(false);
    const sup = this.role.currentUser();
    const explicitTpl =
      (this.linkedTemplateId()?.trim() || this.cellCtx.templateId()?.trim() || '').trim() || null;
    const explicitPole = (this.poleId()?.trim() || this.cellCtx.poleId()?.trim() || '').trim() || null;

    forkJoin({
      emps: this.http.get<Employee[]>('/api/prime/employees').pipe(catchError(() => of([] as Employee[]))),
      fiche: this.api.getFicheForEmployee(sup.id, empId, period, explicitTpl || undefined),
    }).subscribe({
      next: ({ emps, fiche }) => {
        const e = emps.find((x) => x.id === empId);
        if (e) this.employeeLabel.set(`${e.firstName} ${e.lastName}`);
        this.poleDraftId.set(ficheDraftIdString(fiche));
        this.fillingStatus.set(fiche.fillingStatus);
        this.celluleId.set(fiche.celluleId);
        const poleResolved = (explicitPole || fiche.celluleId || '').trim();

        this.api.getIndicators(fiche.serviceId, sup.id).subscribe({
          next: (inds) => {
            this.indicators.set(inds);
            const parsed = parseCellSaisieJson(ficheResponseSaisieJson(fiche));
            this.plafondPrime.set(parsed.plafondPrime);
            this.plafondChallenge.set(parsed.plafondChallenge);

            const resolveDraft = (templateId: string | null) => {
              if (!templateId || !poleResolved) {
                this.finalizeAfterDraft(fiche, inds, null, parsed);
                return;
              }
              this.api
                .getPoleDraft(sup.id, poleResolved, period, templateId)
                .pipe(catchError(() => of(null as SupervisorPolePrimeDraftDto | null)))
                .subscribe((draft) => this.finalizeAfterDraft(fiche, inds, draft, parsed));
            };

            if (explicitTpl) {
              resolveDraft(explicitTpl);
              return;
            }
            this.api.cellsSummary(sup.id, period).subscribe({
              next: (rows) => {
                const row = rows.find((r) => r.celluleId === fiche.celluleId);
                const tid = (row?.linkedTemplateId ?? '').trim() || null;
                resolveDraft(tid);
              },
              error: () => resolveDraft(null),
            });
          },
          error: (err) => {
            this.loadError.set(httpErr(err));
            this.loading.set(false);
          },
        });
      },
      error: (err) => {
        this.loadError.set(httpErr(err));
        this.loading.set(false);
      },
    });
  }

  private finalizeAfterDraft(
    fiche: EmployeePrimeCellFicheDto,
    inds: CellulePrimeIndicatorDto[],
    draft: SupervisorPolePrimeDraftDto | null,
    parsed: ReturnType<typeof parseCellSaisieJson>,
  ): void {
    const schema = parsePrimeSchemaFromDraftJson(draft?.schemaJson ?? '');
    const actives = inds.filter((i) => i.isActive).sort((a, b) => a.sortOrder - b.sortOrder);
    const cellLines = getCellTemplateLinesOrDerived(schema, actives);
    const excelNativeCell = schema ? schemaHasExcelNativeCellRows(schema) : false;
    const derivedBlock = schema && actives.length ? buildDerivedCellTemplateLines(schema, actives).length > 0 : false;

    if (!schema) {
      this.indicators.set(inds);
      this.runs.set([]);
      this.dynamicByIndicator.set({});
      this.schemaBanner.set(null);
      this.bannerIsInfo.set(false);
      this.loadError.set(
        'Brouillon pôle introuvable pour cette période : demandez au superviseur d’enregistrer la partie commune (RACC/SAV) pour ce template.',
      );
      this.loading.set(false);
      return;
    }

    if (!excelNativeCell && actives.length > 0 && !derivedBlock) {
      this.runs.set([]);
      this.dynamicByIndicator.set({});
      this.schemaBanner.set(null);
      this.bannerIsInfo.set(false);
      this.loadError.set(
        'Le gabarit ne contient pas de ligne RACC/SAV exploitable : impossible de cloner les colonnes du bloc Cellule. Vérifiez le fichier importé sur la partie commune.',
      );
      this.loading.set(false);
      return;
    }

    if (!excelNativeCell && derivedBlock) {
      this.schemaBanner.set(
        'Bloc Cellule généré automatiquement à partir des indicateurs configurés (mêmes colonnes et secteurs que la partie pôle).',
      );
      this.bannerIsInfo.set(true);
    } else {
      this.schemaBanner.set(null);
      this.bannerIsInfo.set(false);
    }

    const runs: CellIndicatorRun[] = [];
    const dyn: Record<string, PrimeFicheLigneDynamic> = {};
    let idx = 0;
    for (const ind of actives) {
      const { line: matched, usedIndexFallback } = matchIndicatorToTemplateLine(ind, cellLines, idx);
      const tl = templateLineForCellIndicator(matched, ind.label);
      const tplInd = (matched.indicator ?? '').trim();
      const subtitle =
        tplInd && tplInd.toLowerCase() !== ind.label.trim().toLowerCase() ? `Ligne gabarit : ${tplInd}` : null;
      const warnings: string[] = [];
      if (usedIndexFallback && !(ind.templateStableId ?? '').trim()) {
        warnings.push(
          `Indicateur « ${ind.label} » non rattaché à une ligne précise du gabarit : association par ordre d’apparition. Associez-le à une ligne dans « Indicateurs PRIME ».`,
        );
      }
      let row = ligneDynamicFromTemplateLine(tl);
      applyIndicatorPonderationsToDynamic(row, ind.ponderationPrimePct, ind.ponderationChallengePct);
      const flat = parsed.dynamicFlatByIndicator[ind.id];
      if (flat && Object.keys(flat).length) {
        row = hydrateDynamicFromCellRowFlat(tl, flat);
      } else {
        const leg = parsed.legacyByIndicator[ind.id];
        if (leg && row.secteurValues[0]) {
          row.secteurValues[0].core.resultatPrime = leg.cible ?? '';
          row.secteurValues[0].core.resultatChallenge = leg.realise ?? '';
        }
      }
      applyIndicatorPonderationsToDynamic(row, ind.ponderationPrimePct, ind.ponderationChallengePct);
      dyn[ind.id] = row;
      runs.push({ indicator: ind, templateLine: tl, subtitle, warnings });
      idx++;
    }
    this.runs.set(runs);
    this.dynamicByIndicator.set(dyn);
    this.loading.set(false);
  }

  save(): void {
    const draftId = this.poleDraftId();
    const empId = this.employeeId();
    const period = this.period();
    if (!draftId || !empId || !period) return;
    const sup = this.role.currentUser();
    if (!isEmptyOrNonNegativeNumberString(this.plafondPrime()) || !isEmptyOrNonNegativeNumberString(this.plafondChallenge())) {
      this.saveError.set('Les plafonds Prime / Challenge doivent être des nombres supérieurs ou égaux à 0.');
      return;
    }
    const invalidRow = this.runs().find((r) => hasNegativeDynamicValues(this.dynRow(r.indicator.id)));
    if (invalidRow) {
      this.saveError.set(`L'indicateur « ${invalidRow.indicator.label} » contient une valeur négative ou invalide.`);
      return;
    }
    const rows = this.runs().map((r) => cellRowPayloadForJson(r.indicator.id, this.dynRow(r.indicator.id)));
    const cellSaisieJson = buildCellSaisieJsonV2(this.plafondPrime(), this.plafondChallenge(), rows);
    this.saving.set(true);
    this.saveBanner.set(null);
    this.saveError.set(null);
    this.api
      .upsertEmployeeFiche({
        supervisorUserId: sup.id,
        employeeId: empId,
        period,
        cellulePrimeDraftId: draftId,
        serviceSaisieJson: cellSaisieJson,
      })
      .subscribe({
        next: (f) => {
          this.fillingStatus.set(f.fillingStatus);
          this.saving.set(false);
          const submitted =
            (f.validationStatus ?? '').trim().toLowerCase() === 'pending';
          const complete =
            (f.fillingStatus ?? '').trim().toLowerCase() === 'complete';
          const message = submitted
            ? 'Fiche enregistrée et soumise au workflow de validation (référent technique).'
            : complete
              ? 'Partie cellule enregistrée et complète. Validez la partie commune dans « Fiche PRIME — saisie » pour activer le point vert « Prête » et l’envoi au référent technique.'
              : 'Saisie cellule enregistrée.';
          this.saveBanner.set(message);
          const result: CellSaisieSaveResult = {
            message,
            fillingStatus: f.fillingStatus,
            validationStatus: f.validationStatus ?? 'AwaitingData',
            isReadyForValidation: f.isReadyForValidation === true,
          };
          this.saved.emit(result);
        },
        error: (e) => {
          this.saving.set(false);
          this.saveError.set(httpErr(e));
        },
      });
  }
}
