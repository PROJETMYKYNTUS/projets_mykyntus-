import { Injectable } from '@angular/core';
import { BehaviorSubject, Subscription, interval } from 'rxjs';

import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';
import { DocumentationRealtimeService } from '../../../core/services/documentation-realtime.service';
import type { AuditLogDto, DocumentRequestDto } from '../../../shared/models/api.models';
import type { NotificationItemUi } from '../models/notification-item.model';
import { DocumentationApiService } from './documentation-api.service';
import {
  cleanDocumentRequestTypeLabel,
  pilotDisplayNameFromEmployeeName,
} from '../lib/documentation-request-labels';

const READ_STORAGE_KEY = 'documentation-notifications-read-ids';

function parseOccurrence(iso: string): Date {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? new Date() : d;
}

function dateGroupLabel(iso: string): string {
  const d = parseOccurrence(iso);
  const today = new Date();
  const y = (x: Date) => x.toDateString();
  if (y(d) === y(today)) return "Aujourd'hui";
  const yest = new Date(today);
  yest.setDate(yest.getDate() - 1);
  if (y(d) === y(yest)) return 'Hier';
  return 'Plus tÃ´t';
}

function formatTimestamp(iso: string): string {
  const d = parseOccurrence(iso);
  return d.toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
}

function normalizeStatus(status: string | null | undefined): string {
  return (status ?? '').trim().toLowerCase();
}

function requestNotificationId(prefix: string, requestId: string, status: string): string {
  return `${prefix}:${status}:${requestId}`;
}

function sortByNewest<T>(rows: T[], getIso: (row: T) => string): T[] {
  return [...rows].sort((a, b) => parseOccurrence(getIso(b)).getTime() - parseOccurrence(getIso(a)).getTime());
}

function buildUiNotification(
  id: string,
  type: 'documents' | 'system',
  title: string,
  description: string,
  occurredAt: string,
  read: boolean,
  icon: NotificationItemUi['icon'],
  iconColor: string,
  bgColor: string,
): NotificationItemUi {
  return {
    id,
    type,
    icon,
    title,
    description,
    timestamp: formatTimestamp(occurredAt),
    dateGroup: dateGroupLabel(occurredAt),
    read,
    iconColor,
    bgColor,
  };
}

function mapRhPendingRequestToUi(r: DocumentRequestDto, read: boolean): NotificationItemUi {
  const typeLabel = cleanDocumentRequestTypeLabel(r.type);
  const pilot = pilotDisplayNameFromEmployeeName(r.employeeName, '');
  const reference = (r.internalId ?? '').trim();
  const description = [
    pilot ? `${typeLabel} â€” demande de ${pilot}.` : `${typeLabel}.`,
    reference ? `RÃ©fÃ©rence ${reference}.` : '',
  ]
    .filter(Boolean)
    .join(' ');

  return buildUiNotification(
    requestNotificationId('rh-request', r.id, 'pending'),
    'documents',
    'Nouvelle demande Ã  traiter',
    description,
    r.requestDate,
    read,
    'bell',
    'text-amber-300',
    'bg-amber-500/10',
  );
}

function mapPilotRequestToUi(r: DocumentRequestDto, read: boolean): NotificationItemUi {
  const status = normalizeStatus(r.status);
  const typeLabel = cleanDocumentRequestTypeLabel(r.type) || 'Votre document';
  const occurredAt = status === 'generated' ? (r.generatedAt ?? r.decidedAt ?? r.requestDate) : (r.decidedAt ?? r.requestDate);
  const reason = (r.rejectionReason ?? '').trim();

  if (status === 'rejected') {
    const description = reason
      ? `${typeLabel} a Ã©tÃ© rejetÃ©. Motif: ${reason}`
      : `${typeLabel} a Ã©tÃ© rejetÃ©.`;
    return buildUiNotification(
      requestNotificationId('pilot-request', r.id, 'rejected'),
      'documents',
      'Demande rejetÃ©e',
      description,
      occurredAt,
      read,
      'x-circle',
      'text-red-400',
      'bg-red-500/10',
    );
  }

  return buildUiNotification(
    requestNotificationId('pilot-request', r.id, 'generated'),
    'documents',
    'Document disponible',
    `${typeLabel} est prÃªt et disponible dans votre espace.`,
    occurredAt,
    read,
    'check-circle-2',
    'text-emerald-400',
    'bg-emerald-500/10',
  );
}

function auditToUi(a: AuditLogDto, read: boolean): NotificationItemUi {
  const key = (a.action ?? '').trim().toUpperCase();
  switch (key) {
    case 'WORKFLOW_APPROVE':
      return buildUiNotification(
        a.id,
        'documents',
        'Demande approuvÃ©e',
        'Une demande a Ã©tÃ© approuvÃ©e et peut passer Ã  lâ€™Ã©tape suivante.',
        a.occurredAt,
        read,
        'check-circle-2',
        'text-emerald-400',
        'bg-emerald-500/10',
      );
    case 'WORKFLOW_REJECT':
      return buildUiNotification(
        a.id,
        'documents',
        'Demande rejetÃ©e',
        (a.errorMessage ?? '').trim() || 'Une demande a Ã©tÃ© rejetÃ©e.',
        a.occurredAt,
        read,
        'x-circle',
        'text-red-400',
        'bg-red-500/10',
      );
    case 'DOCUMENT_GENERATED':
    case 'DOCUMENT_UPLOADED_READY':
      return buildUiNotification(
        a.id,
        'documents',
        'Document prÃªt',
        'Un document est disponible.',
        a.occurredAt,
        read,
        'file-text',
        'text-blue-400',
        'bg-blue-500/10',
      );
    case 'DOCUMENT_DRAFT_CREATED':
      return buildUiNotification(
        a.id,
        'documents',
        'Brouillon RH crÃ©Ã©',
        'Un brouillon attend une validation RH.',
        a.occurredAt,
        read,
        'file-text',
        'text-amber-300',
        'bg-amber-500/10',
      );
    default: {
      const ok = a.success !== false;
      return buildUiNotification(
        a.id,
        a.entityType?.toLowerCase().includes('notification') ? 'system' : 'documents',
        'Mise Ã  jour du dossier',
        (a.errorMessage ?? '').trim() || 'Une nouvelle activitÃ© a Ã©tÃ© enregistrÃ©e.',
        a.occurredAt,
        read,
        ok ? 'file-text' : 'x-circle',
        ok ? 'text-blue-400' : 'text-red-400',
        ok ? 'bg-blue-500/10' : 'bg-red-500/10',
      );
    }
  }
}

@Injectable({ providedIn: 'root' })
export class NotificationDataService {
  private items: NotificationItemUi[] = [];
  private readonly readIds = new Set<string>();
  private readonly tick = new BehaviorSubject(0);
  private readonly subscriptions = new Subscription();

  /** Notifie les observateurs aprÃ¨s chargement ou changement dâ€™Ã©tat lu. */
  readonly updated$ = this.tick.asObservable();

  constructor(
    private readonly api: DocumentationApiService,
    private readonly identity: DocumentationIdentityService,
    private readonly realtime: DocumentationRealtimeService,
  ) {
    try {
      const raw = localStorage.getItem(READ_STORAGE_KEY);
      if (raw) {
        const arr = JSON.parse(raw) as string[];
        if (Array.isArray(arr)) arr.forEach((id) => this.readIds.add(id));
      }
    } catch {
      /* ignore */
    }
    this.reloadFromApi();
    this.realtime.start();
    this.subscriptions.add(this.identity.contextRevision$.subscribe(() => this.reloadFromApi()));
    this.subscriptions.add(this.realtime.updates$.subscribe(() => this.reloadFromApi()));
    this.subscriptions.add(interval(30000).subscribe(() => this.reloadFromApi()));
  }

  reloadFromApi(): void {
    const role = this.identity.getCurrentRole();

    if (role === 'rh' || role === 'admin') {
      this.api.getAllDocumentRequests({ status: 'pending' }).subscribe({
        next: (requests) => {
          this.items = sortByNewest(
            requests.filter((r) => normalizeStatus(r.status) === 'pending'),
            (r) => r.requestDate,
          ).map((r) => mapRhPendingRequestToUi(r, this.readIds.has(requestNotificationId('rh-request', r.id, 'pending'))));
          this.emit();
        },
        error: () => {
          this.items = [];
          this.emit();
        },
      });
      return;
    }

    if (role === 'pilote') {
      this.api.getAllAssignedDocumentRequests().subscribe({
        next: (requests) => {
          const relevant = requests.filter((r) => {
            const status = normalizeStatus(r.status);
            return status === 'generated' || status === 'rejected';
          });
          this.items = sortByNewest(
            relevant,
            (r) => (normalizeStatus(r.status) === 'generated' ? (r.generatedAt ?? r.decidedAt ?? r.requestDate) : (r.decidedAt ?? r.requestDate)),
          ).map((r) => {
            const status = normalizeStatus(r.status);
            return mapPilotRequestToUi(r, this.readIds.has(requestNotificationId('pilot-request', r.id, status)));
          });
          this.emit();
        },
        error: () => {
          this.items = [];
          this.emit();
        },
      });
      return;
    }

    this.api.getDataAuditLogs(1, 40).subscribe({
      next: (page) => {
        this.items = sortByNewest(page.items, (a) => a.occurredAt).map((a) => auditToUi(a, this.readIds.has(a.id)));
        this.emit();
      },
      error: () => {
        this.items = [];
        this.emit();
      },
    });
  }

  private persistRead(): void {
    localStorage.setItem(READ_STORAGE_KEY, JSON.stringify([...this.readIds]));
  }

  private emit(): void {
    this.tick.next(this.tick.value + 1);
  }

  list(): NotificationItemUi[] {
    // readIds est mis Ã  jour par markRead / markAllRead sans rÃ©Ã©crire this.items : dÃ©river read ici pour rester cohÃ©rent.
    return this.items.map((n) => ({ ...n, read: this.readIds.has(n.id) }));
  }

  unreadCount(): number {
    return this.items.filter((a) => !this.readIds.has(a.id)).length;
  }

  markRead(id: string): void {
    this.readIds.add(id);
    this.persistRead();
    this.emit();
  }

  markAllRead(): void {
    for (const a of this.items) this.readIds.add(a.id);
    this.persistRead();
    this.emit();
  }

  remove(id: string): void {
    this.items = this.items.filter((a) => a.id !== id);
    this.readIds.delete(id);
    this.persistRead();
    this.emit();
  }
}
