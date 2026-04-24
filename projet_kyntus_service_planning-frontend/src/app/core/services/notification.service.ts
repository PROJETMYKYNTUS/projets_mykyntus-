import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject } from 'rxjs';

export interface PlanningNotification {
  weekCode: string;
  subServiceName: string;
  message: string;
  receivedAt: Date;
  read: boolean;
  type?: 'planning' | 'reclamation' | 'proposition';
  icon?: string;
}

const GATEWAY_URL = ''; // ✅ Ocelot gateway

@Injectable({ providedIn: 'root' })
export class NotificationService {

  private connection!: signalR.HubConnection;
  private reclamationConnection!: signalR.HubConnection;
  private readonly TOKEN_KEY = 'token';

  private notificationsSubject = new BehaviorSubject<PlanningNotification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  private unreadCountSubject = new BehaviorSubject<number>(0);
  public unreadCount$ = this.unreadCountSubject.asObservable();

  connect(userId: number): void {
    this.connectPlanningHub(userId);
    this.connectReclamationHub(userId, false);
  }

  connectAsManager(userId: number): void {
    this.connectPlanningHub(userId);
    this.connectReclamationHub(userId, true);
  }

  private connectPlanningHub(userId: number): void {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/planning`, {          // ✅ via gateway
        transport: signalR.HttpTransportType.LongPolling,
        accessTokenFactory: () => localStorage.getItem(this.TOKEN_KEY) || ''
      })
      .withAutomaticReconnect()
      .build();

    this.connection.on('PlanningPublished', (data: any) => {
      this.pushNotification({
        weekCode:       data.weekCode,
        subServiceName: data.subServiceName,
        message:        data.message,
        receivedAt:     new Date(),
        read:           false,
        type:           'planning',
        icon:           '📅'
      });
    });

    this.connection.start()
      .then(() => this.connection.invoke('JoinUserGroup', userId.toString()))
      .then(() => console.log('✅ Planning Hub — userId:', userId))
      .catch(err => console.error('❌ Planning Hub erreur:', err));
  }

  private connectReclamationHub(userId: number, isManager: boolean): void {
    this.reclamationConnection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/reclamation`, {       // ✅ via gateway
        transport: signalR.HttpTransportType.LongPolling,
        accessTokenFactory: () => localStorage.getItem(this.TOKEN_KEY) || ''
      })
      .withAutomaticReconnect()
      .build();

    this.reclamationConnection.on('ReclamationNotification', (data: any) => {
      this.pushNotification({
        weekCode:       '',
        subServiceName: '',
        message:        `${data.titre} — ${data.message}`,
        receivedAt:     new Date(),
        read:           false,
        type:           'reclamation',
        icon:           '💬'
      });
    });

    this.reclamationConnection.start()
      .then(() => isManager
        ? this.reclamationConnection.invoke('JoinManagerGroup')
        : this.reclamationConnection.invoke('JoinUserGroup', userId.toString())
      )
      .then(() => console.log('✅ Reclamation Hub — manager:', isManager))
      .catch(err => console.error('❌ Reclamation Hub erreur:', err));
  }

  private pushNotification(notification: PlanningNotification): void {
    const current = this.notificationsSubject.value;
    this.notificationsSubject.next([notification, ...current]);
    this.updateUnreadCount();
  }

  markAllRead(): void {
    const updated = this.notificationsSubject.value.map(n => ({ ...n, read: true }));
    this.notificationsSubject.next(updated);
    this.updateUnreadCount();
  }

  private updateUnreadCount(): void {
    const count = this.notificationsSubject.value.filter(n => !n.read).length;
    this.unreadCountSubject.next(count);
  }

  disconnect(): void {
    if (this.connection)            this.connection.stop();
    if (this.reclamationConnection) this.reclamationConnection.stop();
  }
}