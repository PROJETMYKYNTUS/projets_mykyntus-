import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { EMPTY, Observable, of, throwError } from 'rxjs';
import { catchError, expand, map, reduce, switchMap } from 'rxjs/operators';

import { DocumentationDataApiService } from '../../../core/services/documentation-data-api.service';
import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import type {
  AuditLogDto,
  CreateDocumentRequestPayload,
  DocumentRequestDto,
  DocumentTypeDto,
  PagedResponse,
} from '../../../shared/models/api.models';
import type { AuditLogsQuery, DocumentRequestsQuery } from '../../../core/services/documentation-data-api.service';

function sameGuid(a: string | null | undefined, b: string | null | undefined): boolean {
  const x = (a ?? '').trim().toLowerCase();
  const y = (b ?? '').trim().toLowerCase();
  return x !== '' && x === y;
}

/** AlignÃ© sur le backend : assignÃ© = bÃ©nÃ©ficiaire, ou pas de bÃ©nÃ©ficiaire et demandeur = moi (donnÃ©es legacy). */
function isDocumentRequestAssignedToUser(r: DocumentRequestDto, uidNorm: string): boolean {
  const bid = (r.beneficiaryUserId ?? '').trim().toLowerCase();
  const rid = (r.requesterUserId ?? '').trim().toLowerCase();
  if (bid && bid === uidNorm) {
    return true;
  }
  return !bid && rid === uidNorm;
}

/**
 * FaÃ§ade alignÃ©e sur la dÃ©mo React (noms de mÃ©thodes) â€” dÃ©lÃ¨gue au client REST existant.
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
   * Utilise POST /workflow/* (toujours prÃ©sent sur le backend) â€” Ã©vite le 404 si les routes PUT
   * `document-requests/{id}/approve` ne sont pas dÃ©ployÃ©es.
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

  /** Toutes les pages â€” pour tableaux de bord et agrÃ©gations (limite par page : 100). */
  getAllDocumentRequests(query: DocumentRequestsQuery = {}): Observable<DocumentRequestDto[]> {
    return this.fetchAllDocumentRequestPages((p, ps) => this.getDataDocumentRequests(p, ps, query));
  }

  /**
   * Demandes crÃ©Ã©es par lâ€™utilisateur courant.
   * Si le backend ne expose pas encore GET â€¦/my-requests (404), repli sur la liste paginÃ©e + filtre requesterUserId.
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
   * Demandes dont lâ€™utilisateur courant est le bÃ©nÃ©ficiaire.
   * Repli si 404, ou si 200 avec liste vide cÃ´tÃ© pilote (API ancienne sans OR beneficiary null + demandeur).
   */
  getAllAssignedDocumentRequests(query: DocumentRequestsQuery = {}): Observable<DocumentRequestDto[]> {
    const uid = this.identity.getCurrentUserId()?.trim() ?? '';
    const uidNorm = uid.toLowerCase();
    const role = this.identity.getCurrentRole();

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
      switchMap((rows) => {
        if (rows.length > 0 || !uidNorm) {
          return of(rows);
        }
        if (role !== 'pilote') {
          return of(rows);
        }
        return this.getAllDocumentRequests(query).pipe(
          map((all) => all.filter((r) => isDocumentRequestAssignedToUser(r, uidNorm))),
        );
      }),
    );
  }

  private fetchAllDocumentRequestPages(
    fetchPage: (page: number, pageSize: number) => Observable<PagedResponse<DocumentRequestDto>>,
  ): Observable<DocumentRequestDto[]> {
    return fetchPage(1, 100).pipe(
      expand((res) => {
        if (res.page * res.pageSize >= res.totalCount) return EMPTY;
        return fetchPage(res.page + 1, res.pageSize);
      }),
      reduce((acc, res) => acc.concat(res.items), [] as DocumentRequestDto[]),
    );
  }

  getAllAuditLogs(query: AuditLogsQuery = {}): Observable<AuditLogDto[]> {
    return this.getDataAuditLogs(1, 100, query).pipe(
      expand((res) => {
        if (res.page * res.pageSize >= res.totalCount) return EMPTY;
        return this.getDataAuditLogs(res.page + 1, res.pageSize, query);
      }),
      reduce((acc, res) => acc.concat(res.items), [] as AuditLogDto[]),
    );
  }
}
