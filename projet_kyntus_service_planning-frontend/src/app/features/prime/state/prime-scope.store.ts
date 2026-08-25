import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, of, type Observable } from 'rxjs';
import {
  draftListOrganizationalKey,
  PrimeCellPrimeApiService,
  type SupervisorPolePrimeDraftListItemDto,
} from '../services/prime-cell-prime-api.service';
import type { SupervisorOrgScopePole } from '../services/prime-org-api.service';
import { RoleService } from './role.service';

export interface PrimeScopePersistedState {
  period: string;
  selectedPoleId: string;
  selectedCelluleId: string;
  selectedTemplateId: string;
}

/** État partagé période / pôle / cellule / gabarit / drafts pour les écrans superviseur Prime. */
@Injectable({ providedIn: 'root' })
export class PrimeScopeStore {
  private readonly api = inject(PrimeCellPrimeApiService);
  private readonly role = inject(RoleService);

  readonly poles = signal<SupervisorOrgScopePole[]>([]);
  readonly selectedPoleId = signal('');
  readonly selectedCelluleId = signal('');
  readonly selectedTemplateId = signal('');
  readonly period = signal(PrimeScopeStore.defaultPeriod());
  readonly activeDrafts = signal<SupervisorPolePrimeDraftListItemDto[]>([]);

  readonly periodYear = computed(() => {
    const m = /^(\d{4})-(\d{2})$/.exec(this.period());
    return m ? Number(m[1]) : new Date().getFullYear();
  });

  readonly periodMonth = computed(() => {
    const m = /^(\d{4})-(\d{2})$/.exec(this.period());
    return m ? Number(m[2]) : new Date().getMonth() + 1;
  });

  readonly selectedPole = computed(() => {
    const poles = this.poles();
    if (poles.length === 0) return null;
    const sel = this.selectedPoleId().trim();
    return poles.find((p) => p.id === sel) ?? poles[0] ?? null;
  });

  readonly periodOptions = computed(() => {
    const fromDrafts = this.activeDrafts()
      .map((d) => (d.period ?? '').trim())
      .filter((p) => /^\d{4}-\d{2}$/.test(p));
    const set = new Set<string>([this.period(), PrimeScopeStore.defaultPeriod(), ...fromDrafts]);
    return [...set].sort((a, b) => b.localeCompare(a));
  });

  static defaultPeriod(): string {
    const d = new Date();
    d.setDate(1);
    d.setMonth(d.getMonth() - 1);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }

  static storageKey(userId: string): string {
    return `kyntus.scope.prime.${userId.trim().toLowerCase()}`;
  }

  /** @deprecated Utiliser storageKey — conservé pour migration silencieuse. */
  static poleStorageKey(userId: string): string {
    return `kyntus.scope.pole.${userId.trim().toLowerCase()}`;
  }

  /** @deprecated Utiliser storageKey — conservé pour migration silencieuse. */
  static celluleStorageKey(userId: string): string {
    return `kyntus.scope.cellule.${userId.trim().toLowerCase()}`;
  }

  setPeriod(period: string, userId?: string): void {
    const next = (period ?? '').trim();
    if (!/^\d{4}-\d{2}$/.test(next) || next === this.period()) return;
    this.period.set(next);
    if (userId) this.persist(userId);
  }

  setPeriodParts(year: number, month: number, userId?: string): void {
    if (!Number.isFinite(year) || month < 1 || month > 12) return;
    this.setPeriod(`${year}-${String(month).padStart(2, '0')}`, userId);
  }

  setPoles(poles: SupervisorOrgScopePole[]): void {
    this.poles.set(poles);
  }

  setActiveDrafts(drafts: SupervisorPolePrimeDraftListItemDto[]): void {
    this.activeDrafts.set(drafts);
  }

  hydrateFromStorage(userId: string): void {
    const uid = userId.trim();
    if (!uid) return;
    try {
      const raw = localStorage.getItem(PrimeScopeStore.storageKey(uid));
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<PrimeScopePersistedState>;
        if (parsed.period && /^\d{4}-\d{2}$/.test(parsed.period)) this.period.set(parsed.period);
        if (parsed.selectedPoleId) this.selectedPoleId.set(parsed.selectedPoleId.trim());
        if (parsed.selectedCelluleId) this.selectedCelluleId.set(parsed.selectedCelluleId.trim());
        if (parsed.selectedTemplateId) this.selectedTemplateId.set(parsed.selectedTemplateId.trim());
        return;
      }
      const legacyPole = (localStorage.getItem(PrimeScopeStore.poleStorageKey(uid)) ?? '').trim();
      const legacyCellule = (localStorage.getItem(PrimeScopeStore.celluleStorageKey(uid)) ?? '').trim();
      if (legacyPole) this.selectedPoleId.set(legacyPole);
      if (legacyCellule) this.selectedCelluleId.set(legacyCellule);
      this.persist(uid);
    } catch {
      /* ignore */
    }
  }

  persist(userId: string): void {
    const uid = userId.trim();
    if (!uid) return;
    const state: PrimeScopePersistedState = {
      period: this.period(),
      selectedPoleId: this.selectedPoleId().trim(),
      selectedCelluleId: this.selectedCelluleId().trim(),
      selectedTemplateId: this.selectedTemplateId().trim(),
    };
    try {
      localStorage.setItem(PrimeScopeStore.storageKey(uid), JSON.stringify(state));
    } catch {
      /* ignore */
    }
  }

  readPersistedPoleId(userId: string): string {
    this.hydrateFromStorage(userId);
    return this.selectedPoleId().trim();
  }

  persistPoleId(userId: string, poleId: string): void {
    this.setSelectedPoleId(poleId, userId);
  }

  pickAndSetActivePoleId(poleIds: readonly string[], userId?: string): string {
    const current = this.selectedPoleId().trim();
    const stored = userId ? this.readPersistedPoleId(userId) : '';
    const active =
      (current && poleIds.includes(current) ? current : null) ||
      (stored && poleIds.includes(stored) ? stored : null) ||
      poleIds[0] ||
      '';
    this.selectedPoleId.set(active);
    if (userId && active) this.persist(userId);
    return active;
  }

  setSelectedPoleId(poleId: string, userId?: string): void {
    const id = (poleId ?? '').trim();
    if (!id || id === this.selectedPoleId()) return;
    this.selectedPoleId.set(id);
    if (userId) this.persist(userId);
  }

  setSelectedCelluleId(celluleId: string, userId?: string): void {
    const id = (celluleId ?? '').trim();
    if (!id || id === this.selectedCelluleId()) return;
    this.selectedCelluleId.set(id);
    if (userId) this.persist(userId);
  }

  setSelectedTemplateId(templateId: string | null, userId?: string): void {
    const id = (templateId ?? '').trim();
    if (id === this.selectedTemplateId()) return;
    this.selectedTemplateId.set(id);
    if (userId) this.persist(userId);
  }

  resolveDraftForPeriod(
    drafts: ReadonlyArray<SupervisorPolePrimeDraftListItemDto> | null | undefined,
    period: string,
    celluleId: string,
  ): SupervisorPolePrimeDraftListItemDto | null {
    const list = drafts ?? this.activeDrafts();
    const forPeriod = list.filter((d) => (d.period ?? '').trim() === period);
    if (!forPeriod.length) return null;
    if (celluleId) {
      const hit = forPeriod.find((d) => draftListOrganizationalKey(d) === celluleId);
      if (hit) return hit;
    }
    return forPeriod[0] ?? null;
  }

  loadActiveDrafts(userId?: string): Observable<SupervisorPolePrimeDraftListItemDto[]> {
    const uid = (userId ?? this.role.currentUser()?.id ?? '').trim();
    if (!uid) {
      this.activeDrafts.set([]);
      return of([]);
    }
    return this.api.listActivePoleDrafts(uid).pipe(
      catchError(() => of([] as SupervisorPolePrimeDraftListItemDto[])),
    );
  }
}
