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
  ChevronDown,
  ChevronRight,
  ListChecks,
  Pencil,
  Plus,
  Save,
  SlidersHorizontal,
  Trash2,
} from 'lucide';
import { catchError, of } from 'rxjs';
import { LucideIconComponent } from '../../shared/lucide-icon.component';
import { PrimeCardComponent } from '../components/prime-card.component';
import { getCellTemplateLinesOrDerived, parsePrimeSchemaFromDraftJson } from '../lib/prime-cell-schema-merge';
import {
  PrimeCellPrimeApiService,
  type PutServicePrimeIndicatorItem,
  type ServicePrimeIndicatorDto,
} from '../services/prime-cell-prime-api.service';
import { RoleService } from '../state/role.service';

function httpErr(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const b = err.error as { error?: string } | undefined;
    if (b?.error) return b.error;
    return err.message;
  }
  return err instanceof Error ? err.message : 'Erreur';
}

type DraftRow = PutServicePrimeIndicatorItem & { localId: string };

@Component({
  selector: 'app-prime-cellule-indicators-page',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent],
  template: `
    <div class="p-6 sm:p-8 space-y-6 max-w-4xl mx-auto pb-16">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-primary sm:text-3xl flex items-center gap-2">
          <app-lucide-icon [icon]="icons.sliders" className="w-8 h-8 text-blue-600 shrink-0" />
          Indicateurs PRIME par cellule
        </h1>
        <p class="text-muted mt-2 max-w-2xl text-sm">
          Définissez les lignes d’indicateurs (libellé, pondérations Prime et Challenge en %) pour chaque cellule de votre
          pôle. La liste est remplacée à chaque enregistrement.
        </p>
      </div>

      <app-prime-card
        title="Cellule"
        description="Choisissez la cellule à éditer. Les indicateurs sont définis par cellule (ils ne varient pas par période) et s'appliquent à toutes les fiches utilisant cette cellule. Les stableId proposés viennent du gabarit Excel actuellement lié à la cellule."
      >
        <div class="flex flex-wrap gap-4 items-end">
          <div class="flex-1 min-w-[12rem]">
            <label class="block text-sm font-medium text-muted mb-1">Cellule</label>
            <select
              [value]="selectedCelluleId()"
              (change)="onCellChange($any($event.target).value)"
              class="w-full rounded-lg border border-default bg-input px-3 py-2 text-sm text-primary"
            >
              <option value="">— Choisir —</option>
              @for (c of cellOptions(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
          </div>
          <button
            type="button"
            (click)="addRow()"
            [disabled]="!selectedCelluleId()"
            class="inline-flex items-center gap-2 rounded-lg border border-default bg-card px-4 py-2 text-sm font-medium text-primary hover:bg-navy-700/40 disabled:opacity-50"
          >
            <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
            Ligne
          </button>
          <button
            type="button"
            (click)="save()"
            [disabled]="!selectedCelluleId() || saving()"
            class="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-50"
          >
            <app-lucide-icon [icon]="icons.save" className="w-4 h-4" />
            Enregistrer
          </button>
        </div>
      </app-prime-card>

      @if (banner()) {
        <div [class]="bannerClass()">
          {{ banner() }}
        </div>
      }

      @if (selectedCelluleId() && rows().length) {
        <app-prime-card title="Indicateurs" description="Ordre = colonne « ordre » (tri croissant)">
          <div class="overflow-x-auto rounded-lg border border-default">
            <table class="w-full text-sm text-left min-w-[720px]">
              <thead class="bg-input text-muted text-xs uppercase">
                <tr>
                  <th class="px-3 py-2">Ordre</th>
                  <th class="px-3 py-2">Libellé</th>
                  <th class="px-3 py-2 min-w-[12rem]">Ligne template (stableId)</th>
                  <th class="px-3 py-2">Pond. Prime %</th>
                  <th class="px-3 py-2">Pond. Challenge %</th>
                  <th class="px-3 py-2">Actif</th>
                  <th class="px-3 py-2 w-10"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                @for (r of rows(); track r.localId) {
                  <tr>
                    <td class="px-3 py-2">
                      <input
                        type="number"
                        [value]="r.sortOrder"
                        (input)="patchRow(r.localId, { sortOrder: num($any($event.target).value) })"
                        class="w-20 rounded border border-default bg-input px-2 py-1 text-primary"
                      />
                    </td>
                    <td class="px-3 py-2">
                      <input
                        type="text"
                        [value]="r.label"
                        (input)="patchRow(r.localId, { label: $any($event.target).value })"
                        class="w-full min-w-[10rem] rounded border border-default bg-input px-2 py-1 text-primary"
                      />
                    </td>
                    <td class="px-3 py-2 align-top">
                      <select
                        class="w-full max-w-[18rem] rounded border border-default bg-input px-2 py-1 text-xs text-primary"
                        [value]="r.templateStableId ?? ''"
                        (change)="onTemplateStableChange(r.localId, $event)"
                      >
                        <option value="">— Ordre / index —</option>
                        @for (o of templateStableOptions(); track o.value) {
                          <option [value]="o.value">{{ o.label }}</option>
                        }
                      </select>
                    </td>
                    <td class="px-3 py-2">
                      <input
                        type="text"
                        [value]="r.ponderationPrimePct ?? ''"
                        (input)="patchRow(r.localId, { ponderationPrimePct: parsePct($any($event.target).value) })"
                        class="w-20 rounded border border-default bg-input px-2 py-1 text-primary"
                        placeholder="—"
                      />
                    </td>
                    <td class="px-3 py-2">
                      <input
                        type="text"
                        [value]="r.ponderationChallengePct ?? ''"
                        (input)="patchRow(r.localId, { ponderationChallengePct: parsePct($any($event.target).value) })"
                        class="w-20 rounded border border-default bg-input px-2 py-1 text-primary"
                        placeholder="—"
                      />
                    </td>
                    <td class="px-3 py-2">
                      <input
                        type="checkbox"
                        [checked]="r.isActive"
                        (change)="patchRow(r.localId, { isActive: $any($event.target).checked })"
                      />
                    </td>
                    <td class="px-3 py-2">
                      <button
                        type="button"
                        (click)="removeRow(r.localId)"
                        class="p-1 rounded text-muted hover:text-rose-600"
                        aria-label="Supprimer"
                      >
                        <app-lucide-icon [icon]="icons.trash" className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </app-prime-card>
      }

      <app-prime-card
        title="Aperçu des cellules"
        description="Toutes les cellules de votre pôle. Cliquez sur une cellule pour voir ses indicateurs déjà définis, ou utilisez « Modifier » pour la charger dans le formulaire ci-dessus."
      >
        @if (cellOptions().length === 0) {
          <p class="text-sm text-muted">Aucune cellule rattachée à votre pôle.</p>
        } @else {
          <ul class="divide-y divide-default rounded-lg border border-default overflow-hidden">
            @for (c of cellOptions(); track c.id) {
              <li class="bg-card">
                <div
                  class="flex flex-wrap items-center gap-2 px-3 py-2 hover:bg-navy-700/40 transition-colors"
                  [class.bg-navy-700]="selectedCelluleId() === c.id"
                >
                  <button
                    type="button"
                    (click)="toggleCellExpansion(c.id)"
                    class="flex flex-1 min-w-0 items-center gap-2 text-left"
                    [attr.aria-expanded]="isCellExpanded(c.id)"
                    [attr.aria-controls]="'cell-panel-' + c.id"
                  >
                    <app-lucide-icon
                      [icon]="isCellExpanded(c.id) ? icons.chevronDown : icons.chevronRight"
                      className="w-4 h-4 text-muted shrink-0"
                    />
                    <span class="truncate text-sm font-medium text-primary">{{ c.name }}</span>
                    @if (isCellLoading(c.id)) {
                      <span class="ml-2 text-xs text-muted italic">chargement…</span>
                    } @else {
                      @if (indicatorsForCell(c.id); as list) {
                        <span
                          class="ml-2 inline-flex items-center gap-1 rounded-md border border-default bg-input px-2 py-0.5 text-[11px] font-semibold text-muted"
                          [title]="list.length + ' indicateur(s) au total — ' + activeIndicatorCount(c.id) + ' actif(s)'"
                        >
                          <app-lucide-icon [icon]="icons.listChecks" className="w-3 h-3" />
                          {{ activeIndicatorCount(c.id) }} / {{ list.length }}
                        </span>
                      }
                    }
                  </button>
                  <button
                    type="button"
                    (click)="selectAndEdit(c.id)"
                    class="inline-flex items-center gap-1.5 rounded-md border border-default bg-card px-2.5 py-1 text-xs font-semibold text-primary hover:bg-navy-700/50"
                    [class.border-blue-500]="selectedCelluleId() === c.id"
                    [class.text-blue-400]="selectedCelluleId() === c.id"
                    title="Charger cette cellule dans le formulaire et faire défiler vers le haut"
                  >
                    <app-lucide-icon [icon]="icons.pencil" className="w-3.5 h-3.5" />
                    Modifier
                  </button>
                </div>

                @if (isCellExpanded(c.id)) {
                  <div [id]="'cell-panel-' + c.id" class="border-t border-default bg-app/40 px-4 py-3">
                    @if (isCellLoading(c.id) && !indicatorsForCell(c.id)) {
                      <p class="text-sm text-muted italic">Chargement des indicateurs…</p>
                    } @else if (indicatorsForCell(c.id) !== null) {
                      @let list = indicatorsForCell(c.id) ?? [];
                      @if (list.length === 0) {
                        <div class="flex flex-wrap items-center justify-between gap-2">
                          <p class="text-sm text-muted">
                            Aucun indicateur défini pour cette cellule.
                          </p>
                          <button
                            type="button"
                            (click)="selectAndEdit(c.id)"
                            class="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-2.5 py-1 text-xs font-semibold text-white hover:bg-blue-700"
                          >
                            <app-lucide-icon [icon]="icons.plus" className="w-3.5 h-3.5" />
                            Ajouter des indicateurs
                          </button>
                        </div>
                      } @else {
                        <div class="overflow-x-auto rounded border border-default">
                          <table class="w-full text-xs text-left">
                            <thead class="bg-input text-muted uppercase text-[11px]">
                              <tr>
                                <th class="px-2 py-1.5 w-10">#</th>
                                <th class="px-2 py-1.5">Libellé</th>
                                <th class="px-2 py-1.5 w-24">Prime %</th>
                                <th class="px-2 py-1.5 w-24">Challenge %</th>
                                <th class="px-2 py-1.5 w-16">Actif</th>
                              </tr>
                            </thead>
                            <tbody class="divide-y divide-default">
                              @for (ind of list; track ind.id) {
                                <tr [class.opacity-50]="!ind.isActive">
                                  <td class="px-2 py-1.5 text-muted">{{ ind.sortOrder }}</td>
                                  <td class="px-2 py-1.5 text-primary">
                                    {{ ind.label || '—' }}
                                    @if (ind.templateStableId) {
                                      <span
                                        class="ml-1 inline-block rounded bg-input px-1 py-0.5 font-mono text-[10px] text-muted"
                                        [title]="'Lié à la ligne template ' + ind.templateStableId"
                                      >
                                        {{ ind.templateStableId }}
                                      </span>
                                    }
                                  </td>
                                  <td class="px-2 py-1.5 text-primary">
                                    {{ ind.ponderationPrimePct ?? '—' }}
                                  </td>
                                  <td class="px-2 py-1.5 text-primary">
                                    {{ ind.ponderationChallengePct ?? '—' }}
                                  </td>
                                  <td class="px-2 py-1.5">
                                    @if (ind.isActive) {
                                      <span class="inline-block rounded bg-emerald-500/15 px-1.5 py-0.5 text-[10px] font-semibold text-emerald-300">
                                        Oui
                                      </span>
                                    } @else {
                                      <span class="inline-block rounded bg-default/40 px-1.5 py-0.5 text-[10px] font-semibold text-muted">
                                        Non
                                      </span>
                                    }
                                  </td>
                                </tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      }
                    } @else {
                      <p class="text-sm text-muted italic">Indisponible pour cette cellule.</p>
                    }
                  </div>
                }
              </li>
            }
          </ul>
        }
      </app-prime-card>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeCelluleIndicatorsPageComponent implements OnInit {
  private readonly api = inject(PrimeCellPrimeApiService);
  private readonly role = inject(RoleService);

  readonly icons = {
    sliders: SlidersHorizontal,
    plus: Plus,
    save: Save,
    trash: Trash2,
    chevronDown: ChevronDown,
    chevronRight: ChevronRight,
    pencil: Pencil,
    listChecks: ListChecks,
  };

  readonly cellOptions = signal<{ id: string; name: string }[]>([]);
  readonly period = signal(this.defaultPeriod());
  readonly templateStableOptions = signal<{ value: string; label: string }[]>([]);
  readonly selectedCelluleId = signal('');
  readonly rows = signal<DraftRow[]>([]);
  readonly saving = signal(false);
  readonly banner = signal<string | null>(null);
  readonly bannerIsError = signal(false);

  /** Cellules actuellement dépliées dans l'aperçu du bas. */
  readonly expandedCellIds = signal<ReadonlySet<string>>(new Set<string>());
  /** Cellules dont les indicateurs sont en cours de chargement. */
  readonly cellIndicatorsLoading = signal<ReadonlySet<string>>(new Set<string>());
  /** Cache des indicateurs déjà chargés par **serviceId** (rafraîchi sur save). */
  readonly cellIndicatorsMap = signal<ReadonlyMap<string, ServicePrimeIndicatorDto[]>>(new Map());

  readonly bannerClass = computed(() => {
    if (!this.banner()) return '';
    return this.bannerIsError()
      ? 'rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-800 dark:text-rose-200'
      : 'rounded-lg border border-emerald-500/40 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-800 dark:text-emerald-200';
  });

  ngOnInit(): void {
    this.reloadCellules();
  }

  /**
   * Période utilisée uniquement en interne pour :
   *  - lister les cellules du pôle (`cellsSummary`)
   *  - retrouver le brouillon pôle lié pour proposer les `stableId` de gabarit
   *
   * Les indicateurs eux-mêmes sont stockés par cellule (pas par période) et s'appliquent
   * à toutes les fiches utilisant cette cellule, c'est pourquoi le sélecteur de période
   * n'est pas exposé à l'utilisateur sur cette page.
   */
  private defaultPeriod(): string {
    const d = new Date();
    d.setDate(1);
    d.setMonth(d.getMonth() - 1);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }

  private reloadCellules(): void {
    const u = this.role.currentUser();
    this.cellIndicatorsMap.set(new Map());
    this.cellIndicatorsLoading.set(new Set());
    this.expandedCellIds.set(new Set());
    this.api.cellsSummary(u.id, this.period()).subscribe({
      next: (list) => {
        this.cellOptions.set(list.map((x) => ({ id: x.serviceId, name: x.serviceName })));
        const cur = this.selectedCelluleId();
        if (cur && !list.some((x) => x.serviceId === cur)) {
          this.selectedCelluleId.set('');
          this.rows.set([]);
          this.templateStableOptions.set([]);
        }
        this.preloadAllCellIndicators(list.map((x) => x.serviceId));
      },
      error: () => {
        this.cellOptions.set([]);
      },
    });
  }

  /**
   * Lance en parallèle un GET indicators par cellule, hydrate le cache et permet
   * à la liste d'aperçu d'afficher le nombre d'indicateurs et le contenu déplié
   * sans attendre un clic utilisateur (les pôles ont typiquement ≤ 20 cellules).
   */
  private preloadAllCellIndicators(celluleIds: readonly string[]): void {
    const u = this.role.currentUser();
    if (!u?.id || celluleIds.length === 0) return;
    this.cellIndicatorsLoading.set(new Set(celluleIds));
    for (const id of celluleIds) {
      this.api
        .getIndicators(id, u.id)
        .pipe(catchError(() => of<ServicePrimeIndicatorDto[]>([])))
        .subscribe((list) => {
          this.cellIndicatorsMap.update((m) => {
            const next = new Map(m);
            next.set(id, list);
            return next;
          });
          this.cellIndicatorsLoading.update((s) => {
            if (!s.has(id)) return s;
            const next = new Set(s);
            next.delete(id);
            return next;
          });
        });
    }
  }

  onCellChange(id: string): void {
    this.selectedCelluleId.set(id);
    this.banner.set(null);
    if (!id) {
      this.rows.set([]);
      this.templateStableOptions.set([]);
      return;
    }
    this.loadIndicatorsAndStableLines(id);
  }

  /** `id` = **serviceId** (clé API indicateurs). */
  indicatorsForCell(serviceId: string): ServicePrimeIndicatorDto[] | null {
    const m = this.cellIndicatorsMap();
    return m.has(serviceId) ? (m.get(serviceId) as ServicePrimeIndicatorDto[]) : null;
  }

  /** Nombre d'indicateurs actifs (utilisé pour le badge de la carte). */
  activeIndicatorCount(serviceId: string): number {
    const list = this.indicatorsForCell(serviceId);
    if (!list) return 0;
    return list.filter((x) => x.isActive).length;
  }

  isCellExpanded(celluleId: string): boolean {
    return this.expandedCellIds().has(celluleId);
  }

  isCellLoading(celluleId: string): boolean {
    return this.cellIndicatorsLoading().has(celluleId);
  }

  toggleCellExpansion(celluleId: string): void {
    this.expandedCellIds.update((s) => {
      const next = new Set(s);
      if (next.has(celluleId)) next.delete(celluleId);
      else next.add(celluleId);
      return next;
    });
  }

  /**
   * Sélectionne la cellule dans le formulaire du haut et fait remonter la page
   * pour que l'utilisateur puisse éditer ses indicateurs en contexte.
   */
  selectAndEdit(celluleId: string): void {
    this.onCellChange(celluleId);
    if (typeof window !== 'undefined') {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }


  private loadIndicatorsAndStableLines(cellId: string): void {
    const u = this.role.currentUser();
    this.api.getIndicators(cellId, u.id).subscribe({
      next: (list) => {
        this.rows.set(list.map((x) => this.fromDto(x)));
        this.api.cellsSummary(u.id, this.period()).subscribe({
          next: (summaries) => {
            const s = summaries.find((x) => x.serviceId === cellId);
            const tid = (s?.linkedTemplateId ?? '').trim();
            const pole = (s?.celluleId ?? '').trim();
            if (!tid || !pole) {
              this.templateStableOptions.set([]);
              return;
            }
            this.api
              .getPoleDraft(u.id, pole, this.period(), tid)
              .pipe(catchError(() => of(null)))
              .subscribe((draft) => {
                if (!draft?.schemaJson) {
                  this.templateStableOptions.set([]);
                  return;
                }
                const schema = parsePrimeSchemaFromDraftJson(draft.schemaJson);
                const optActives = this.rows()
                  .filter((r) => r.isActive && r.label.trim())
                  .map((r) => this.toIndicatorDtoStub(cellId, r));
                const lines = getCellTemplateLinesOrDerived(schema, optActives);
                this.templateStableOptions.set(
                  lines.map((l) => ({
                    value: l.stableId,
                    label: `${l.stableId} — ${(l.indicator ?? '').trim() || '(sans libellé)'}`,
                  })),
                );
              });
          },
          error: () => this.templateStableOptions.set([]),
        });
      },
      error: (e) => {
        this.rows.set([]);
        this.banner.set(httpErr(e));
        this.bannerIsError.set(true);
      },
    });
  }

  private toIndicatorDtoStub(serviceId: string, r: DraftRow): ServicePrimeIndicatorDto {
    return {
      id: r.localId,
      serviceId,
      sortOrder: r.sortOrder,
      label: r.label,
      ponderationPrimePct: r.ponderationPrimePct ?? null,
      ponderationChallengePct: r.ponderationChallengePct ?? null,
      isActive: r.isActive,
      templateStableId: r.templateStableId ?? null,
      createdAt: '',
      updatedAt: null,
    };
  }

  private fromDto(x: ServicePrimeIndicatorDto): DraftRow {
    return {
      localId: x.id,
      sortOrder: x.sortOrder,
      label: x.label,
      ponderationPrimePct: x.ponderationPrimePct ?? null,
      ponderationChallengePct: x.ponderationChallengePct ?? null,
      isActive: x.isActive,
      templateStableId: x.templateStableId,
    };
  }

  addRow(): void {
    const next = this.rows().length;
    this.rows.update((rs) => [
      ...rs,
      {
        localId: `new-${Date.now()}`,
        sortOrder: next,
        label: '',
        ponderationPrimePct: null,
        ponderationChallengePct: null,
        isActive: true,
        templateStableId: null,
      },
    ]);
  }

  removeRow(localId: string): void {
    this.rows.update((rs) => rs.filter((r) => r.localId !== localId));
  }

  patchRow(localId: string, patch: Partial<DraftRow>): void {
    this.rows.update((rs) =>
      rs.map((r) => (r.localId === localId ? { ...r, ...patch } : r)),
    );
  }

  onTemplateStableChange(localId: string, ev: Event): void {
    const v = ((ev.target as HTMLSelectElement).value ?? '').trim();
    this.patchRow(localId, { templateStableId: v ? v : null });
  }

  num(v: string): number {
    const n = Number(v);
    return Number.isFinite(n) ? n : 0;
  }

  parsePct(v: string): number | null {
    const t = v.trim();
    if (!t) return null;
    const n = Number(t.replace(',', '.'));
    return Number.isFinite(n) ? n : null;
  }

  save(): void {
    const cellId = this.selectedCelluleId();
    if (!cellId) return;
    const u = this.role.currentUser();
    const indicators: PutServicePrimeIndicatorItem[] = this.rows()
      .filter((r) => r.label.trim().length > 0)
      .map((r) => ({
        sortOrder: r.sortOrder,
        label: r.label.trim(),
        ponderationPrimePct: r.ponderationPrimePct,
        ponderationChallengePct: r.ponderationChallengePct,
        isActive: r.isActive,
        templateStableId: r.templateStableId ?? null,
      }));
    this.saving.set(true);
    this.banner.set(null);
    this.api.putIndicators(cellId, u.id, indicators).subscribe({
      next: (list) => {
        this.rows.set(list.map((x) => this.fromDto(x)));
        this.cellIndicatorsMap.update((m) => {
          const next = new Map(m);
          next.set(cellId, list);
          return next;
        });
        this.saving.set(false);
        this.banner.set('Indicateurs enregistrés.');
        this.bannerIsError.set(false);
      },
      error: (e) => {
        this.saving.set(false);
        this.banner.set(httpErr(e));
        this.bannerIsError.set(true);
      },
    });
  }
}
