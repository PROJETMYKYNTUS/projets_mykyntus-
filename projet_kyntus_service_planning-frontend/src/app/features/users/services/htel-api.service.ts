import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const htelBase = '/api/directory/htel';

export interface HtelTechnicienDto {
  idTechnicien: number;
  technicien: string;
  actif: number;
  code: string;
}

export interface HtelLinkedEmployeeDto {
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
  idTechnicien: number;
  htelCode?: string | null;
  htelTechnicienName?: string | null;
}

export interface HtelOrphanTechnicienDto {
  idTechnicien: number;
  technicien: string;
  actif: number;
  code: string;
}

export interface HtelEmployeeCandidateDto {
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface HtelAmbiguousMatchDto {
  idTechnicien: number;
  technicien: string;
  code: string;
  candidates: HtelEmployeeCandidateDto[];
}

export interface HtelUnlinkedEmployeeDto {
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface HtelLiaisonsReportDto {
  linked: HtelLinkedEmployeeDto[];
  orphansHtel: HtelOrphanTechnicienDto[];
  ambiguous: HtelAmbiguousMatchDto[];
  unlinkedEmployees: HtelUnlinkedEmployeeDto[];
}

export interface HtelSyncReportDto {
  techniciensFetched: number;
  linkedUpdated: number;
  newlyLinked: number;
  orphansHtel: number;
  ambiguous: number;
  unlinkedEmployees: number;
}

@Injectable({ providedIn: 'root' })
export class HtelApiService {
  private readonly http = inject(HttpClient);

  listTechniciens(actifOnly = true): Observable<HtelTechnicienDto[]> {
    return this.http.get<HtelTechnicienDto[]>(`${htelBase}/techniciens`, {
      params: actifOnly ? { actifOnly: 'true' } : {},
    });
  }

  getLiaisons(): Observable<HtelLiaisonsReportDto> {
    return this.http.get<HtelLiaisonsReportDto>(`${htelBase}/liaisons`);
  }

  sync(): Observable<HtelSyncReportDto> {
    return this.http.post<HtelSyncReportDto>(`${htelBase}/techniciens/sync`, {});
  }

  link(employeeId: string, idTechnicien: number): Observable<void> {
    return this.http.post<void>(`${htelBase}/liaisons/link`, { employeeId, idTechnicien });
  }

  unlink(employeeId: string): Observable<void> {
    return this.http.post<void>(`${htelBase}/liaisons/unlink`, { employeeId });
  }
}

/** Aligné sur HtelNameNormalizer côté backend. */
export function normalizeHtelName(value: string | null | undefined): string {
  if (!value?.trim()) return '';
  const collapsed = value.trim().replace(/\s+/g, ' ');
  return collapsed
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase();
}

export function employeeHtelNameKeys(firstName: string, lastName: string): string[] {
  const first = normalizeHtelName(firstName);
  const last = normalizeHtelName(lastName);
  if (!first && !last) return [];
  if (first && last) return [`${last} ${first}`, `${first} ${last}`];
  return [last || first];
}

export function findUniqueHtelMatch(
  firstName: string,
  lastName: string,
  techniciens: HtelTechnicienDto[],
): HtelTechnicienDto | null {
  const keys = new Set(employeeHtelNameKeys(firstName, lastName));
  const matches = techniciens.filter((t) => keys.has(normalizeHtelName(t.technicien)));
  return matches.length === 1 ? matches[0] : null;
}
