import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, from, of } from 'rxjs';
import { catchError, map, shareReplay } from 'rxjs/operators';

import { UserService } from '../../features/users/services/user.service';
import { ContractService, type ContractResponse } from '../../features/contract/services/contract.service';
import { ParrainageApiService } from '../../features/parrainage/services/parrainage-api.service';
import { DocumentationDataApiService } from '../services/documentation-data-api.service';
import type { User } from '../../features/users/users-module';
import { userMatchesSearch } from '../hr/user-hr-display.util';

export type GlobalSearchType = 'employee' | 'contract' | 'parrainage' | 'document';

export interface GlobalSearchResult {
  type: GlobalSearchType;
  /** Identifiant utilisé pour la navigation (id Planning, id contrat, id referral, internalId document). */
  id: string;
  title: string;
  subtitle: string;
}

export interface GlobalSearchGroup {
  type: GlobalSearchType;
  label: string;
  results: GlobalSearchResult[];
}

/** Nombre max de résultats affichés par groupe. */
const PER_GROUP_LIMIT = 6;
/** Durée de vie des caches de listes (5 min). */
const CACHE_TTL_MS = 5 * 60_000;

const GROUP_LABELS: Record<GlobalSearchType, string> = {
  employee: 'Employés',
  contract: 'Contrats',
  parrainage: 'Parrainage',
  document: 'Documents',
};

/**
 * Recherche globale multi-sources. Il n'existe pas d'endpoint /api/search unifié :
 * on interroge les endpoints existants (employés en cache + filtre client, contrats,
 * parrainage en recherche serveur, documents) et on agrège des résultats groupés.
 * Chaque source échoue silencieusement (catchError → []) pour ne jamais casser la barre.
 */
@Injectable({ providedIn: 'root' })
export class GlobalSearchService {
  private readonly userService = inject(UserService);
  private readonly contractService = inject(ContractService);
  private readonly parrainageApi = inject(ParrainageApiService);
  private readonly docApi = inject(DocumentationDataApiService);

  private usersCache$: Observable<User[]> | null = null;
  private contractsCache$: Observable<ContractResponse[]> | null = null;

  search(rawTerm: string): Observable<GlobalSearchGroup[]> {
    const term = rawTerm.trim().toLowerCase();
    if (term.length < 2) {
      return of([]);
    }

    return forkJoin({
      employee: this.searchEmployees(term),
      contract: this.searchContracts(term),
      parrainage: this.searchParrainage(term),
      document: this.searchDocuments(term),
    }).pipe(
      map((buckets) =>
        (Object.keys(buckets) as GlobalSearchType[])
          .map((type) => ({
            type,
            label: GROUP_LABELS[type],
            results: buckets[type],
          }))
          .filter((group) => group.results.length > 0),
      ),
    );
  }

  private users$(): Observable<User[]> {
    if (!this.usersCache$) {
      this.usersCache$ = this.userService.getAllUsers().pipe(
        catchError(() => of<User[]>([])),
        shareReplay(1),
      );
      setTimeout(() => (this.usersCache$ = null), CACHE_TTL_MS);
    }
    return this.usersCache$;
  }

  private contracts$(): Observable<ContractResponse[]> {
    if (!this.contractsCache$) {
      this.contractsCache$ = this.contractService.getAll().pipe(
        catchError(() => of<ContractResponse[]>([])),
        shareReplay(1),
      );
      setTimeout(() => (this.contractsCache$ = null), CACHE_TTL_MS);
    }
    return this.contractsCache$;
  }

  private searchEmployees(term: string): Observable<GlobalSearchResult[]> {
    return this.users$().pipe(
      map((users) =>
        users
          .filter((u) => userMatchesSearch(u, term))
          .slice(0, PER_GROUP_LIMIT)
          .map<GlobalSearchResult>((u) => ({
            type: 'employee',
            id: String(u.id),
            title: `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.email || `Employé ${u.id}`,
            subtitle: [u.roleName, u.email].filter(Boolean).join(' · ') || '—',
          })),
      ),
      catchError(() => of<GlobalSearchResult[]>([])),
    );
  }

  private searchContracts(term: string): Observable<GlobalSearchResult[]> {
    return this.contracts$().pipe(
      map((contracts) =>
        contracts
          .filter((c) =>
            [c.userFullName, c.type, c.status].filter(Boolean).join(' ').toLowerCase().includes(term),
          )
          .slice(0, PER_GROUP_LIMIT)
          .map<GlobalSearchResult>((c) => ({
            type: 'contract',
            id: String(c.id),
            title: c.userFullName || `Contrat ${c.id}`,
            subtitle: [c.type, c.status].filter(Boolean).join(' · ') || '—',
          })),
      ),
      catchError(() => of<GlobalSearchResult[]>([])),
    );
  }

  private searchParrainage(term: string): Observable<GlobalSearchResult[]> {
    return from(this.parrainageApi.getLinkableReferrals(term)).pipe(
      map((referrals) =>
        referrals.slice(0, PER_GROUP_LIMIT).map<GlobalSearchResult>((r) => ({
          type: 'parrainage',
          id: r.id,
          title: r.candidateName || 'Parrainage',
          subtitle: [r.referrerName, r.position].filter(Boolean).join(' · ') || '—',
        })),
      ),
      catchError(() => of<GlobalSearchResult[]>([])),
    );
  }

  private searchDocuments(term: string): Observable<GlobalSearchResult[]> {
    return this.docApi.getDocumentRequests(1, 50, {}).pipe(
      map((paged) =>
        (paged.items ?? [])
          .filter((d) =>
            [d.employeeName, d.type, d.status].filter(Boolean).join(' ').toLowerCase().includes(term),
          )
          .slice(0, PER_GROUP_LIMIT)
          .map<GlobalSearchResult>((d) => ({
            type: 'document',
            id: d.internalId,
            title: d.employeeName || 'Demande de document',
            subtitle: [d.type, d.status].filter(Boolean).join(' · ') || '—',
          })),
      ),
      catchError(() => of<GlobalSearchResult[]>([])),
    );
  }
}
