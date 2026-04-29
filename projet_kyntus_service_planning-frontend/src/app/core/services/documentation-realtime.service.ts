import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { environment } from '../../../environments/environment';

/**
 * Pont temps réel pour Documentation.
 * Le backend peut recevoir un webhook puis publier un événement Hub;
 * ce service écoute ces événements et notifie le frontend pour auto-refresh.
 */
@Injectable({ providedIn: 'root' })
export class DocumentationRealtimeService {
  private hubConnection: signalR.HubConnection | null = null;
  private readonly updateTick = new Subject<void>();
  private started = false;

  /** Flux déclenché à chaque événement métier reçu. */
  readonly updates$ = this.updateTick.asObservable();

  start(): void {
    if (this.started) {
      return;
    }
    this.started = true;

    const enabled = (environment as { documentationRealtimeEnabled?: boolean }).documentationRealtimeEnabled ?? true;
    if (!enabled) {
      return;
    }

    const hubUrl =
      (environment as { documentationRealtimeHubUrl?: string }).documentationRealtimeHubUrl?.trim() ||
      '/hubs/documentation';
    const token = typeof localStorage !== 'undefined' ? localStorage.getItem('token') || '' : '';

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .build();

    const notifyUpdate = () => this.updateTick.next();
    this.hubConnection.on('DocumentationUpdated', notifyUpdate);
    this.hubConnection.on('DocumentRequestChanged', notifyUpdate);
    this.hubConnection.on('AuditLogCreated', notifyUpdate);
    this.hubConnection.on('NotificationUpdated', notifyUpdate);

    void this.hubConnection.start().catch(() => {
      // Fallback polling reste actif côté services, donc on ignore l'échec.
    });
  }

  stop(): void {
    const conn = this.hubConnection;
    this.hubConnection = null;
    this.started = false;
    if (conn) {
      void conn.stop();
    }
  }
}
