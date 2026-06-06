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
  weeklyPlanningId?: number;
}

export interface NewsletterNotification {
  title: string;
  subject: string;
  sentAt: string;
  receivedAt: Date;
  read: boolean;
}

export function planningNotificationId(n: PlanningNotification): string {
  const src = n.type === 'proposition' ? 'proposition' : n.type === 'reclamation' ? 'reclamation' : 'planning';
  const ts = n.receivedAt instanceof Date ? n.receivedAt.getTime() : new Date(n.receivedAt).getTime();
  return `${src}-${n.weekCode || 'na'}-${ts}`;
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
  private readonly TOKEN_KEY = 'token';

  private notificationsSubject = new BehaviorSubject<PlanningNotification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  private newsletterSubject = new BehaviorSubject<NewsletterNotification[]>([]);
  public newsletter$ = this.newsletterSubject.asObservable();

  private newsletterConnection!: signalR.HubConnection;

  private unreadCountSubject = new BehaviorSubject<number>(0);
  public unreadCount$ = this.unreadCountSubject.asObservable();

  // ✅ Subject dédié pour les composants qui écoutent les notifs reclamation
  public reclamationNotif$ = new Subject<ReclamationNotif>();

  // ─────────────────────────────────────────────────────────────
  // API publique
  // ─────────────────────────────────────────────────────────────

  connect(userId: number): void {
    this.connectPlanningHub(userId);
    this.connectReclamationHub(userId, false);
    this.connectNewsletterHub();
  }

  connectAsManager(userId: number): void {
    this.connectPlanningHub(userId);
    this.connectReclamationHub(userId, true);
    this.connectNewsletterHub();
  }

  disconnect(): void {
    this.connection?.stop();
    this.reclamationConnection?.stop();
    this.newsletterConnection?.stop();
  }

  markAllRead(): void {
    const updated = this.notificationsSubject.value.map(n => ({ ...n, read: true }));
    this.notificationsSubject.next(updated);
    this.updateUnreadCount();
  }

  markOneRead(id: string): void {
    const updated = this.notificationsSubject.value.map(n =>
      planningNotificationId(n) === id ? { ...n, read: true } : n,
    );
    this.notificationsSubject.next(updated);
    this.updateUnreadCount();
  }

  markNewsletterRead(index: number): void {
    const list = [...this.newsletterSubject.value];
    if (list[index]) {
      list[index] = { ...list[index], read: true };
      this.newsletterSubject.next(list);
    }
  }

  markAllNewslettersRead(): void {
    this.newsletterSubject.next(this.newsletterSubject.value.map(n => ({ ...n, read: true })));
  }

  // ─────────────────────────────────────────────────────────────
  // Planning Hub
  // ─────────────────────────────────────────────────────────────

private connectPlanningHub(userId: number): void {
  this.connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/planning', {
      transport: signalR.HttpTransportType.WebSockets, // ← WebSocket
      skipNegotiation: false,
      accessTokenFactory: () => localStorage.getItem(this.TOKEN_KEY) || ''
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

    this.connection.on('PlanningPublished', (data: { weekCode: string; subServiceName: string; message: string; weeklyPlanningId?: number }) => {
      this.pushNotification({
        weekCode:       data.weekCode,
        subServiceName: data.subServiceName,
        message:        data.message,
        receivedAt:     new Date(),
        read:           false,
        type:           'planning',
        icon:           'calendar',
        weeklyPlanningId: data.weeklyPlanningId,
      });
    });

    this.connection.onreconnected(async () => {
      console.log('🔄 Planning Hub reconnecté — re-join groupe');
      try {
        await this.connection.invoke('JoinUserGroup', userId.toString());
        console.log('✅ Planning Hub — groupe user re-rejoint:', userId);
      } catch (err) {
        console.error('❌ Planning Hub re-join échoué:', err);
      }
    });

    this.connection.onclose(err =>
      console.warn('⚠️ Planning Hub fermé', err)
    );

    this.connection.start()
      .then(async () => {
        console.log('✅ Planning Hub connecté');
        await this.connection.invoke('JoinUserGroup', userId.toString());
        console.log('✅ Planning Hub — userId:', userId);
      })
      .catch(err => console.error('❌ Planning Hub erreur:', err));
  }

  // ─────────────────────────────────────────────────────────────
  // Reclamation Hub
  // ─────────────────────────────────────────────────────────────

private connectReclamationHub(userId: number, isManager: boolean): void {
  this.reclamationConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/reclamation', {
      transport: signalR.HttpTransportType.WebSockets, // ← WebSocket
      skipNegotiation: false,
      accessTokenFactory: () => {
        const token = localStorage.getItem(this.TOKEN_KEY) || '';
        console.log('🔑 Token envoyé au hub:', token ? 'OK' : 'VIDE ❌');
        return token;
      }
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

    // ✅ Écouter l'événement avant la connexion
   this.reclamationConnection.on('ReclamationNotification', (data: ReclamationNotif) => {
  console.log('📨 ReclamationNotification reçue:', data);

  this.reclamationNotif$.next(data);

  // ✅ Détecter si c'est une proposition ou une réclamation
  const isProposition = data.titre.toLowerCase().includes('proposition');
  
  this.pushNotification({
    weekCode:       '',
    subServiceName: '',
    message:        `${data.titre} — ${data.message}`,
    receivedAt:     new Date(),
    read:           false,
    type:           isProposition ? 'proposition' : 'reclamation', // ✅
    icon:           isProposition ? 'lightbulb' : 'message-circle'
  });
});

    // ✅ Helper pour rejoindre les bons groupes
    const joinGroups = async (): Promise<void> => {
      if (isManager) {
        await this.reclamationConnection.invoke('JoinManagerGroup');
        console.log('✅ Reclamation Hub — groupe managers rejoint');
      }
      // Un manager reçoit aussi ses propres notifs en tant qu'auteur
      await this.reclamationConnection.invoke('JoinUserGroup', userId.toString());
      console.log(`✅ Reclamation Hub — groupe user_${userId} rejoint`);
    };

    // ✅ Re-rejoindre après reconnexion automatique
    this.reclamationConnection.onreconnected(async () => {
      console.log('🔄 Reclamation Hub reconnecté — re-join groupes');
      try {
        await joinGroups();
      } catch (err) {
        console.error('❌ Reclamation Hub re-join échoué:', err);
      }
    });

    this.reclamationConnection.onclose(err =>
      console.warn('⚠️ Reclamation Hub fermé', err)
    );

    // ✅ Démarrer puis rejoindre les groupes
    this.reclamationConnection.start()
      .then(async () => {
        console.log('✅ Reclamation Hub connecté — state:', this.reclamationConnection.state);
        await joinGroups();
      })
      .catch(err => console.error('❌ Reclamation Hub erreur:', err));
  }

  private connectNewsletterHub(): void {
    const role = (localStorage.getItem('user') ? JSON.parse(localStorage.getItem('user') || '{}').role : '') || '';
    const groupMap: Record<string, string> = {
      Admin: 'Admin',
      RH: 'Admin',
      Manager: 'Manager',
      Employee: 'Employee',
    };
    const group = groupMap[role] ?? 'Employee';

    this.newsletterConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/newsletter', {
        accessTokenFactory: () => localStorage.getItem(this.TOKEN_KEY) || '',
      })
      .withAutomaticReconnect()
      .build();

    this.newsletterConnection.on('ReceiveNewsletter', (data: { title: string; subject: string; sentAt: string }) => {
      const current = this.newsletterSubject.value;
      this.newsletterSubject.next([
        {
          title: data.title,
          subject: data.subject,
          sentAt: data.sentAt,
          receivedAt: new Date(),
          read: false,
        },
        ...current,
      ]);
    });

    this.newsletterConnection.start()
      .then(async () => {
        await this.newsletterConnection.invoke('JoinGroup', group);
        await this.newsletterConnection.invoke('JoinGroup', 'All');
      })
      .catch(() => { /* newsletter hub optional */ });
  }

  // ─────────────────────────────────────────────────────────────
  // Helpers privés
  // ─────────────────────────────────────────────────────────────

  private isConnected(connection: signalR.HubConnection): boolean {
    return connection &&
      connection.state !== signalR.HubConnectionState.Disconnected;
  }

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