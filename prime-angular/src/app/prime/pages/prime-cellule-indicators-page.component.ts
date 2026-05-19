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
import {
  PrimeOrgApiService,
  type SupervisorOrgScopeCellule,
} from '../services/prime-org-api.service';
import { selectValueOrEmpty } from '../lib/prime-select-options';
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
          Indicateurs PRIME par service
        </h1>
        <p class="text-muted mt-2 max-w-2xl text-sm">
          Choisissez une cellule RH puis un service : les indicateurs sont enregistrés par service et s’appliquent aux
          fiches pilotes de ce service. Utilisez le rôle Superviseur (ex. e9) pour voir votre périmètre.
        </p>
      </div>

      <app-prime-card
        title="Cellule et service"
        description="Indicateurs par service (prime_service). Les stableId viennent du gabarit Excel du pôle pour la période de référence."
      >
        <div class="flex flex-wrap gap-4 items-end">
          <div class="flex-1 min-w-[12rem]">
            <label class="block text-sm font-medium text-muted mb-1">Cellule (RH)</label>
            <select
              [value]="selectRhCelluleValue()"
              (change)="onRhCelluleChange($any($event.target).value)"
              class="w-full rounded-lg border border-default bg-input px-3 py-2 text-sm text-primary"
            >
              <option value="">— Choisir —</option>
              @for (c of rhCelluleOptions(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
          </div>
          <div class="flex-1 min-w-[12rem]">
            <label class="block text-sm font-medium text-muted mb-1">Service</label>
            <select
              [value]="selectServiceValue()"
              (change)="onServiceChange($any($event.target).value)"
              [disabled]="!selectedRhCelluleId()"
              class="w-full rounded-lg border border-default bg-input px-3 py-2 text-sm text-primary disabled:opacity-50"
            >
              <option value="">— Choisir —</option>
              @for (s of serviceOptions(); track s.id) {
                <option [value]="s.id">{{ s.name }}</option>
              }
            </select>
          </div>
          <button
            type="button"
            (click)="addRow()"
            [disabled]="!selectedServiceId()"
            class="inline-flex items-center gap-2 rounded-lg border border-default bg-card px-4 py-2 text-sm font-medium text-primary hover:bg-navy-700/40 disabled:opacity-50"
          >
            <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
            Ligne
          </button>
          <button
            type="button"
            (click)="save()"
            [disabled]="!selectedServiceId() || saving()"
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

      @if (selectedServiceId() && rows().length) {
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
        title="Aperçu des services"
        description="Services de votre périmètre superviseur. Dépliez pour voir les indicateurs ou « Modifier » pour éditer."
      >
        @if (previewEntries().length === 0) {
          <p class="text-sm text-muted">Aucune cellule ou service dans votre périmètre (vérifiez le rôle Superviseur).</p>
        } @else {
          <ul class="divide-y divide-default rounded-lg border border-default overflow-hidden">
            @for (e of previewEntries(); track e.serviceId) {
              <li class="bg-card">
                <div
                  class="flex flex-wrap items-center gap-2 px-3 py-2 hover:bg-navy-700/40 transition-colors"
                  [class.bg-navy-700]="selectedServiceId() === e.serviceId"
                >
                  <button
                    type="button"
                    (click)="toggleCellExpansion(e.serviceId)"
                    class="flex flex-1 min-w-0 items-center gap-2 text-left"
                    [attr.aria-expanded]="isCellExpanded(e.serviceId)"
                    [attr.aria-controls]="'svc-panel-' + e.serviceId"
                  >
                    <app-lucide-icon
                      [icon]="isCellExpanded(e.serviceId) ? icons.chevronDown : icons.chevronRight"
                      className="w-4 h-4 text-muted shrink-0"
                    />
                    <span class="truncate text-sm font-medium text-primary">{{ e.label }}</span>
                    @if (isCellLoading(e.serviceId)) {
                      <span class="ml-2 text-xs text-muted italic">chargement…</span>
                    } @else {
                      @if (indicatorsForCell(e.serviceId); as list) {
                        <span
                          class="ml-2 inline-flex items-center gap-1 rounded-md border border-default bg-input px-2 py-0.5 text-[11px] font-semibold text-muted"
                          [title]="list.length + ' indicateur(s) au total — ' + activeIndicatorCount(e.serviceId) + ' actif(s)'"
                        >
                          <app-lucide-icon [icon]="icons.listChecks" className="w-3 h-3" />
                          {{ activeIndicatorCount(e.serviceId) }} / {{ list.length }}
                        </span>
                      }
                    }
                  </button>
                  <button
                    type="button"
                    (click)="selectAndEditService(e.celluleId, e.serviceId)"
                    class="inline-flex items-center gap-1.5 rounded-md border border-default bg-card px-2.5 py-1 text-xs font-semibold text-primary hover:bg-navy-700/50"
                    [class.border-blue-500]="selectedServiceId() === e.serviceId"
                    [class.text-blue-400]="selectedServiceId() === e.serviceId"
                    title="Charger ce service dans le formulaire"
                  >
                    <app-lucide-icon [icon]="icons.pencil" className="w-3.5 h-3.5" />
                    Modifier
                  </button>
                </div>

                @if (isCellExpanded(e.serviceId)) {
                  <div [id]="'svc-panel-' + e.serviceId" class="border-t border-default bg-app/40 px-4 py-3">
                    @if (isCellLoading(e.serviceId) && !indicatorsForCell(e.serviceId)) {
                      <p class="text-sm text-muted italic">Chargement des indicateurs…</p>
                    } @else if (indicatorsForCell(e.serviceId) !== null) {
                      @let list = indicatorsForCell(e.serviceId) ?? [];
                      @if (list.length === 0) {
                        <div class="flex flex-wrap items-center justify-between gap-2">
                          <p class="text-sm text-muted">
                            Aucun indicateur défini pour ce service.
                          </p>
                          <button
                            type="button"
                            (click)="selectAndEditService(e.celluleId, e.serviceId)"
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
                      <p class="text-sm text-muted italic">Indisponible pour ce service.</p>
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
  private readonly orgApi = inject(PrimeOrgApiService);
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

  readonly scopeCellules = signal<SupervisorOrgScopeCellule[]>([]);
  readonly period = signal(this.defaultPeriod());
  readonly templateStableOptions = signal<{ value: string; label: string }[]>([]);
  readonly selectedRhCelluleId = signal('');
  readonly selectedServiceId = signal('');
  readonly rows = signal<DraftRow[]>([]);
  readonly saving = signal(false);
  readonly banner = signal<string | null>(null);
  readonly bannerIsError = signal(false);

  readonly expandedCellIds = signal<ReadonlySet<string>>(new Set<string>());
  readonly cellIndicatorsLoading = signal<ReadonlySet<string>>(new Set<string>());
  readonly cellIndicatorsMap = signal<ReadonlyMap<string, ServicePrimeIndicatorDto[]>>(new Map());

  readonly rhCelluleOptions = computed(() => this.scopeCellules());
  readonly serviceOptions = computed(() => {
    const cid = this.selectedRhCelluleId();
    return this.scopeCellules().find((c) => c.id === cid)?.services ?? [];
  });
  readonly previewEntries = computed(() =>
    this.scopeCellules().flatMap((c) =>
      c.services.map((s) => ({
        serviceId: s.id,
        celluleId: c.id,
        label: c.services.length > 1 ? `${c.name} — ${s.name}` : s.name || c.name,
      })),
    ),
  );

  readonly bannerClass = computed(() => {
    if (!this.banner()) return '';
    return this.bannerIsError()
      ? 'rounded-lg border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-800 dark:text-rose-200'
      : 'rounded-lg border border-emerald-500/40 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-800 dark:text-emerald-200';
  });

  ngOnInit(): void {
    this.reloadScope();
  }

  selectRhCelluleValue(): string {
    const opts = this.rhCelluleOptions().map((c) => c.id);
    return selectValueOrEmpty(this.selectedRhCelluleId(), opts);
  }

  selectServiceValue(): string {
    const opts = this.serviceOptions().map((s) => s.id);
    return selectValueOrEmpty(this.selectedServiceId(), opts);
  }

  private defaultPeriod(): string {
    const d = new Date();
    d.setDate(1);
    d.setMonth(d.getMonth() - 1);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }

  private reloadScope(): void {
    const u = this.role.currentUser();
    this.cellIndicatorsMap.set(new Map());
    this.cellIndicatorsLoading.set(new Set());
    this.expandedCellIds.set(new Set());
    this.orgApi.getSupervisorScope(u.id).subscribe({
      next: (list) => {
        this.scopeCellules.set(list);
        const serviceIds = list.flatMap((c) => c.services.map((s) => s.id));
        const curSvc = this.selectedServiceId();
        if (curSvc && !serviceIds.includes(curSvc)) {
          this.selectedRhCelluleId.set('');
          this.selectedServiceId.set('');
          this.rows.set([]);
          this.templateStableOptions.set([]);
        }
        this.preloadAllCellIndicators(serviceIds);
      },
      error: () => {
        this.scopeCellules.set([]);
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

  onRhCelluleChange(celluleId: string): void {
    this.selectedRhCelluleId.set(celluleId);
    this.banner.set(null);
    if (!celluleId) {
      this.selectedServiceId.set('');
      this.rows.set([]);
      this.templateStableOptions.set([]);
      return;
    }
    const cell = this.scopeCellules().find((c) => c.id === celluleId);
    const firstSvc = cell?.services[0]?.id ?? '';
    this.onServiceChange(firstSvc);
  }

  onServiceChange(serviceId: string): void {
    this.selectedServiceId.set(serviceId);
    this.banner.set(null);
    if (!serviceId) {
      this.rows.set([]);
      this.templateStableOptions.set([]);
      return;
    }
    this.loadIndicatorsAndStableLines(serviceId);
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
  selectAndEditService(celluleId: string, serviceId: string): void {
    this.selectedRhCelluleId.set(celluleId);
    this.onServiceChange(serviceId);
    if (typeof window !== 'undefined') {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }


  private loadIndicatorsAndStableLines(serviceId: string): void {
    const u = this.role.currentUser();
    this.api.getIndicators(serviceId, u.id).subscribe({
      next: (list) => {
        this.rows.set(list.map((x) => this.fromDto(x)));
        this.api.cellsSummary(u.id, this.period()).subscribe({
          next: (summaries) => {
            const s = summaries.find((x) => x.serviceId === serviceId);
            const rhCell = this.findRhCelluleForService(serviceId);
            const tid = (s?.linkedTemplateId ?? '').trim();
            const pole = (rhCell?.rootPoleId ?? s?.celluleId ?? '').trim();
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
                  .map((r) => this.toIndicatorDtoStub(serviceId, r));
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

  private findRhCelluleForService(serviceId: string): SupervisorOrgScopeCellule | undefined {
    return this.scopeCellules().find((c) => c.services.some((s) => s.id === serviceId));
  }

  save(): void {
    const cellId = this.selectedServiceId();
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
