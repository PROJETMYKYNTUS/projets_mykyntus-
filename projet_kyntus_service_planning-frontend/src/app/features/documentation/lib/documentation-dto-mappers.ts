import type { AuditLogDto, DocumentRequestDto, DocumentTypeDto } from '../../../shared/models/api.models';
import type { DocumentationDocument, DocumentationRequest, DocumentationTemplate } from '../interfaces/documentation-entities';
import { cleanDocumentRequestTypeLabel } from './documentation-request-labels';

/** Affichage : collaborateur â€” type de document â€” date (gÃ©nÃ©ration si dispo, sinon demande). */
export function generatedDocumentDisplayLabel(r: {
  employeeName: string;
  type: string;
  generatedAt?: string | null;
  requestDate?: string | null;
}): string {
  const emp = (r.employeeName ?? '').trim() || 'Collaborateur';
  const typ = cleanDocumentRequestTypeLabel(r.type);
  const raw = (r.generatedAt ?? r.requestDate ?? '').trim();
  if (!raw) return `${emp} â€” ${typ}`;
  const d = new Date(raw.length === 10 ? `${raw}T12:00:00` : raw);
  if (Number.isNaN(d.getTime())) return `${emp} â€” ${typ} â€” ${raw.slice(0, 10)}`;
  const dateStr = d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' });
  return `${emp} â€” ${typ} â€” ${dateStr}`;
}

/** Base de nom de fichier sÃ»re (sans extension). */
export function generatedDocumentExportBaseName(r: {
  employeeName: string;
  type: string;
  generatedAt?: string | null;
  requestDate?: string | null;
}): string {
  return generatedDocumentDisplayLabel(r).replace(/[/\\?%*:|"<>]/g, '-').replace(/\s*â€”\s*/g, '_').replace(/\s+/g, '_');
}

function normalizeRequestStatus(raw: string): DocumentationRequest['status'] {
  const key = raw.trim().toLowerCase();
  const map: Record<string, DocumentationRequest['status']> = {
    pending: 'Pending',
    approved: 'Approved',
    rejected: 'Rejected',
    generated: 'Generated',
    cancelled: 'Cancelled',
  };
  return map[key] ?? (raw as DocumentationRequest['status']);
}

export function mapDocumentRequestDto(d: DocumentRequestDto): DocumentationRequest {
  let status = normalizeRequestStatus(String(d.status ?? ''));
  if (d.generatedDocumentId?.trim()) {
    status = 'Generated';
  }
  return {
    id: d.id,
    internalId: d.internalId,
    type: cleanDocumentRequestTypeLabel(d.type),
    requestDate: d.requestDate,
    status,
    employeeName: d.employeeName,
    employeeId: d.employeeId ?? undefined,
    reason: d.reason ?? undefined,
    complementaryComments: d.complementaryComments?.trim() || undefined,
    rejectionReason: d.rejectionReason ?? undefined,
    allowedActions: d.allowedActions ?? [],
    generatedDocumentId: d.generatedDocumentId?.trim() || undefined,
    generatedAt: d.generatedAt?.trim() || undefined,
    documentUrl: d.documentUrl?.trim() || undefined,
  };
}

export function mapRequestToGeneratedDocument(r: DocumentationRequest): DocumentationDocument | null {
  if (r.status !== 'Generated') return null;
  const genAt = r.generatedAt?.trim();
  const dateCreated = genAt || r.requestDate;
  const typeLabel = cleanDocumentRequestTypeLabel(r.type);
  const base = { employeeName: r.employeeName, type: typeLabel, generatedAt: genAt, requestDate: r.requestDate };
  return {
    id: r.id,
    name: generatedDocumentDisplayLabel(base),
    type: typeLabel,
    dateCreated,
    status: 'Generated',
    employeeName: r.employeeName,
    employeeId: r.employeeId,
    generatedDocumentId: r.generatedDocumentId?.trim(),
    exportFileBase: generatedDocumentExportBaseName(base),
  };
}

/** Liste Â« Mes documents Â» : toutes les demandes assignÃ©es Ã  lâ€™utilisateur (tous statuts). */
export function mapAssignedRequestToDocument(r: DocumentationRequest): DocumentationDocument {
  const hasPdf = !!r.generatedDocumentId?.trim();
  const genAt = r.generatedAt?.trim();
  const dateCreated = hasPdf && genAt ? genAt : r.requestDate;
  const typeLabel = cleanDocumentRequestTypeLabel(r.type);
  const base = { employeeName: r.employeeName, type: typeLabel, generatedAt: genAt, requestDate: r.requestDate };
  return {
    id: r.id,
    name: generatedDocumentDisplayLabel(base),
    type: typeLabel,
    dateCreated,
    status: hasPdf ? 'Generated' : r.status,
    employeeName: r.employeeName,
    employeeId: r.employeeId,
    generatedDocumentId: r.generatedDocumentId?.trim(),
    exportFileBase: hasPdf ? generatedDocumentExportBaseName(base) : undefined,
  };
}

export function mapDocumentTypeDtoToTemplate(t: DocumentTypeDto): DocumentationTemplate {
  return {
    id: t.id,
    name: t.name,
    lastModified: '',
    variables: [],
  };
}
