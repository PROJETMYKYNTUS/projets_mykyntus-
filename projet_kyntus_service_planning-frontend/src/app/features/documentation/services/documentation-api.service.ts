import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { EMPTY, Observable, of, throwError } from 'rxjs';
import { catchError, expand, map, reduce } from 'rxjs/operators';

/** Aligné sur le clamp serveur (DocumentationDataController). */
export const DOCUMENTATION_MAX_PAGE_SIZE = 200;

import { DocumentationDataApiService } from '../../../core/services/documentation-data-api.service';
import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import type {
  AuditLogDto,
  CreateDocumentRequestPayload,
  DocumentRequestDto,
  DocumentTypeDto,
  PagedResponse,
} from '../../../core/models/documentation.models';
import type { AuditLogsQuery, DocumentRequestsQuery } from '../../../core/services/documentation-data-api.service';

function sameGuid(a: string | null | undefined, b: string | null | undefined): boolean {
  const x = (a ?? '').trim().toLowerCase();
  const y = (b ?? '').trim().toLowerCase();
  return x !== '' && x === y;
}

/** Aligné sur le backend : assigné = bénéficiaire, ou pas de bénéficiaire et demandeur = moi (données legacy). */
function isDocumentRequestAssignedToUser(r: DocumentRequestDto, uidNorm: string): boolean {
  const bid = (r.beneficiaryUserId ?? '').trim().toLowerCase();
  const rid = (r.requesterUserId ?? '').trim().toLowerCase();
  if (bid && bid === uidNorm) {
    return true;
  }
  return !bid && rid === uidNorm;
}

/**
 * Façade sur l’API REST (noms de méthodes) — délègue au client HTTP existant.
 */
@Injectable({ providedIn: 'root' })
export class DocumentationApiService {
  constructor(
    private readonly data: DocumentationDataApiService,
    private readonly identity: DocumentationIdentityService,
  ) {}

  getDocTypesForCatalog(): Observable<DocumentTypeDto[]> {
    return this.data.getDocumentTypes();
  }

  createDocumentRequest(body: CreateDocumentRequestPayload): Observable<DocumentRequestDto> {
    return this.data.createDocumentRequest(body);
  }

  /**
   * Utilise POST /workflow/* (toujours présent sur le backend) — évite le 404 si les routes PUT
   * `document-requests/{id}/approve` ne sont pas déployées.
   */
  validateDocumentRequest(internalId: string, comment?: string | null): Observable<DocumentRequestDto> {
    return this.data.workflowValidate({ documentRequestId: internalId, comment });
  }

  approveDocumentRequest(internalId: string): Observable<DocumentRequestDto> {
    return this.data.workflowApprove({ documentRequestId: internalId });
  }

  rejectDocumentRequest(internalId: string, rejectionReason: string): Observable<DocumentRequestDto> {
    return this.data.workflowReject({ documentRequestId: internalId, rejectionReason });
  }

  getDataDocumentRequests(
    page = 1,
    pageSize = 20,
    query: DocumentRequestsQuery = {},
  ): Observable<PagedResponse<DocumentRequestDto>> {
    return this.data.getDocumentRequests(page, pageSize, query);
  }

  getDataDocumentRequestsMine(
    page = 1,
    pageSize = 20,
    query: DocumentRequestsQuery = {},
  ): Observable<PagedResponse<DocumentRequestDto>> {
    return this.data.getDocumentRequestsMine(page, pageSize, query);
  }

  getDataDocumentRequestsAssignedToMe(
    page = 1,
    pageSize = 20,
    query: DocumentRequestsQuery = {},
  ): Observable<PagedResponse<DocumentRequestDto>> {
    return this.data.getDocumentRequestsAssignedToMe(page, pageSize, query);
  }

  getDataAuditLogs(
    page = 1,
    pageSize = 50,
    query: AuditLogsQuery = {},
  ): Observable<PagedResponse<AuditLogDto>> {
    return this.data.getAuditLogs(page, pageSize, query);
  }

  /** Une page (tableaux de bord, notifications) — évite N allers-retours HTTP. */
  getDocumentRequestsPage(
    pageSize = 80,
    query: DocumentRequestsQuery = {},
    page = 1,
  ): Observable<PagedResponse<DocumentRequestDto>> {
    return this.getDataDocumentRequests(page, Math.min(pageSize, DOCUMENTATION_MAX_PAGE_SIZE), query);
  }

  getMyDocumentRequestsPage(
    pageSize = 80,
    query: DocumentRequestsQuery = {},
    page = 1,
  ): Observable<PagedResponse<DocumentRequestDto>> {
    return this.getDataDocumentRequestsMine(page, Math.min(pageSize, DOCUMENTATION_MAX_PAGE_SIZE), query);
  }

  getAssignedDocumentRequestsPage(
    pageSize = 80,
    query: DocumentRequestsQuery = {},
    page = 1,
  ): Observable<PagedResponse<DocumentRequestDto>> {
    return this.getDataDocumentRequestsAssignedToMe(
      page,
      Math.min(pageSize, DOCUMENTATION_MAX_PAGE_SIZE),
      query,
    );
  }

  getAuditLogsPage(
    pageSize = 80,
    query: AuditLogsQuery = {},
    page = 1,
  ): Observable<PagedResponse<AuditLogDto>> {
    return this.getDataAuditLogs(page, Math.min(pageSize, DOCUMENTATION_MAX_PAGE_SIZE), query);
  }

  /** Toutes les pages — réservé aux écrans liste (RH, équipe, etc.). */
  getAllDocumentRequests(query: DocumentRequestsQuery = {}): Observable<DocumentRequestDto[]> {
    return this.fetchAllDocumentRequestPages((p, ps) => this.getDataDocumentRequests(p, ps, query));
  }

  /**
   * Demandes créées par l’utilisateur courant.
   * Si le backend ne expose pas encore GET …/my-requests (404), repli sur la liste paginée + filtre requesterUserId.
   */
  getAllMyDocumentRequests(query: DocumentRequestsQuery = {}): Observable<DocumentRequestDto[]> {
    const uid = this.identity.getCurrentUserId()?.trim() ?? '';
    return this.fetchAllDocumentRequestPages((p, ps) => this.getDataDocumentRequestsMine(p, ps, query)).pipe(
      catchError((err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 404 && uid) {
          return this.getAllDocumentRequests(query).pipe(
            map((rows) => rows.filter((r) => sameGuid(r.requesterUserId, uid))),
          );
        }
        return throwError(() => err);
      }),
    );
  }

  /**
   * Demandes dont l’utilisateur courant est le bénéficiaire.
   * Repli si 404, ou si 200 avec liste vide côté pilote (API ancienne sans OR beneficiary null + demandeur).
   */
  getAllAssignedDocumentRequests(query: DocumentRequestsQuery = {}): Observable<DocumentRequestDto[]> {
    const uid = this.identity.getCurrentUserId()?.trim() ?? '';
    const uidNorm = uid.toLowerCase();

    return this.fetchAllDocumentRequestPages((p, ps) =>
      this.getDataDocumentRequestsAssignedToMe(p, ps, query),
    ).pipe(
      catchError((err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 404 && uidNorm) {
          return this.getAllDocumentRequests(query).pipe(
            map((rows) => rows.filter((r) => isDocumentRequestAssignedToUser(r, uidNorm))),
          );
        }
        return throwError(() => err);
      }),
    );
  }

  private fetchAllDocumentRequestPages(
    fetchPage: (page: number, pageSize: number) => Observable<PagedResponse<DocumentRequestDto>>,
  ): Observable<DocumentRequestDto[]> {
    return fetchPage(1, DOCUMENTATION_MAX_PAGE_SIZE).pipe(
      expand((res) => {
        if (res.page * res.pageSize >= res.totalCount) return EMPTY;
        return fetchPage(res.page + 1, res.pageSize);
      }),
      reduce((acc, res) => acc.concat(res.items), [] as DocumentRequestDto[]),
    );
  }

  getAllAuditLogs(query: AuditLogsQuery = {}): Observable<AuditLogDto[]> {
    return this.getDataAuditLogs(1, DOCUMENTATION_MAX_PAGE_SIZE, query).pipe(
      expand((res) => {
        if (res.page * res.pageSize >= res.totalCount) return EMPTY;
        return this.getDataAuditLogs(res.page + 1, res.pageSize, query);
      }),
      reduce((acc, res) => acc.concat(res.items), [] as AuditLogDto[]),
    );
  }
}
