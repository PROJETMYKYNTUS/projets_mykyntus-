import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';

export interface PlanningNotification {
  weekCode: string;
  subServiceName: string;
  message: string;
  receivedAt: Date;
  read: boolean;
  type?: 'planning' | 'reclamation' | 'proposition';
  icon?: string;
}

export interface ReclamationNotif {
  titre: string;
  message: string;
  type: 'info' | 'success' | 'warning';
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {

  private connection!: signalR.HubConnection;
  private reclamationConnection!: signalR.HubConnection;

  private notificationsSubject = new BehaviorSubject<PlanningNotification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  private unreadCountSubject = new BehaviorSubject<number>(0);
  public unreadCount$ = this.unreadCountSubject.asObservable();

  public reclamationNotif$ = new Subject<ReclamationNotif>();

  // ── Token ──────────────────────────────────────────────────
  private getToken(): string {
    return localStorage.getItem('access_token')
        || localStorage.getItem('token')
        || localStorage.getItem('jwt')
        || '';
  }

  // ── AuthUserId depuis JWT ──────────────────────────────────
  private getAuthUserIdFromToken(): string {
    const token = this.getToken();
    if (!token) return '';
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
          ?? payload.sub
          ?? payload.nameid
          ?? '';
    } catch {
      return '';
    }
  }

  // ── API publique ───────────────────────────────────────────
  connect(userId: number): void {
    this.connectPlanningHub();
    this.connectReclamationHub(userId, false);
  }

  connectAsManager(userId: number): void {
    this.connectPlanningHub();
    this.connectReclamationHub(userId, true);
  }

  disconnect(): void {
    this.connection?.stop();
    this.reclamationConnection?.stop();
  }

  markAllRead(): void {
    const updated = this.notificationsSubject.value.map(n => ({ ...n, read: true }));
    this.notificationsSubject.next(updated);
    this.updateUnreadCount();
  }

  // ── Planning Hub ───────────────────────────────────────────
  private connectPlanningHub(): void {
    const authUserId = this.getAuthUserIdFromToken();
    console.log('🔑 AuthUserId extrait du token:', authUserId);

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/planning', {
        accessTokenFactory: () => this.getToken()
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.connection.on('PlanningPublished', (data: any) => {
      console.log('📅 PlanningPublished reçu:', data);
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

    this.connection.onreconnected(async () => {
      console.log('🔄 Planning Hub reconnecté');
      await this.connection.invoke('JoinUserGroup', authUserId);
    });

    this.connection.onclose(err =>
      console.warn('⚠️ Planning Hub fermé', err)
    );

    this.connection.start()
      .then(async () => {
        console.log('✅ Planning Hub connecté');
        console.log('🔑 JoinUserGroup avec AuthUserId:', authUserId);
        await this.connection.invoke('JoinUserGroup', authUserId);
      })
      .catch(err => console.error('❌ Planning Hub erreur:', err));
  }

  // ── Reclamation Hub ────────────────────────────────────────
  private connectReclamationHub(userId: number, isManager: boolean): void {
    this.reclamationConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/reclamation', {
        accessTokenFactory: () => {
          const token = this.getToken();
          console.log('🔑 Token reclamation hub:', token ? 'OK ✅' : 'VIDE ❌');
          return token;
        }
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.reclamationConnection.on('ReclamationNotification', (data: ReclamationNotif) => {
      console.log('📨 ReclamationNotification reçue:', data);
      this.reclamationNotif$.next(data);
      const isProposition = data.titre.toLowerCase().includes('proposition');
      this.pushNotification({
        weekCode:       '',
        subServiceName: '',
        message:        `${data.titre} — ${data.message}`,
        receivedAt:     new Date(),
        read:           false,
        type:           isProposition ? 'proposition' : 'reclamation',
        icon:           isProposition ? '💡' : '💬'
      });
    });

    const joinGroups = async (): Promise<void> => {
      if (isManager) {
        await this.reclamationConnection.invoke('JoinManagerGroup');
        console.log('✅ Reclamation Hub — groupe managers rejoint');
      }
      await this.reclamationConnection.invoke('JoinUserGroup', userId.toString());
      console.log(`✅ Reclamation Hub — groupe user_${userId} rejoint`);
    };

    this.reclamationConnection.onreconnected(async () => {
      try { await joinGroups(); }
      catch (err) { console.error('❌ Reclamation Hub re-join échoué:', err); }
    });

    this.reclamationConnection.onclose(err =>
      console.warn('⚠️ Reclamation Hub fermé', err)
    );

    this.reclamationConnection.start()
      .then(async () => {
        console.log('✅ Reclamation Hub connecté — state:', this.reclamationConnection.state);
        await joinGroups();
      })
      .catch(err => console.error('❌ Reclamation Hub erreur:', err));
  }

  // ── Helpers privés ─────────────────────────────────────────
  private pushNotification(notification: PlanningNotification): void {
    const current = this.notificationsSubject.value;
    this.notificationsSubject.next([notification, ...current]);
    this.updateUnreadCount();
  }

  private updateUnreadCount(): void {
    const count = this.notificationsSubject.value.filter(n => !n.read).length;
    this.unreadCountSubject.next(count);
  }
}