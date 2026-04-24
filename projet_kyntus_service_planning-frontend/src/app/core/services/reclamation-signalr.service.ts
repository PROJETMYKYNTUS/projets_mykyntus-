// reclamation-signalr.service.ts
import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

export interface ReclamationNotif {
  titre: string;
  message: string;
  type: 'info' | 'success' | 'warning';
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ReclamationSignalRService {
  private hubConnection!: signalR.HubConnection;
  
  // Observable que le composant cloche va écouter
  notificationReceived$ = new Subject<ReclamationNotif>();

  startConnection(token: string): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/reclamation', {
        accessTokenFactory: () => token  // ⚠️ JWT obligatoire (hub [Authorize])
      })
      .withAutomaticReconnect()
      .build();

    // Écouter l'événement côté C#
    this.hubConnection.on('ReclamationNotification', (notif: ReclamationNotif) => {
      this.notificationReceived$.next(notif);
    });

    this.hubConnection
      .start()
      .then(() => {
        console.log('✅ ReclamationHub connecté');
        // ⚠️ CRITIQUE : rejoindre le groupe managers APRÈS la connexion
        this.joinManagerGroup();
      })
      .catch(err => console.error('❌ Erreur connexion ReclamationHub:', err));

    // Re-rejoindre après reconnexion automatique
    this.hubConnection.onreconnected(() => {
      this.joinManagerGroup();
    });
  }

  private joinManagerGroup(): void {
    this.hubConnection
      .invoke('JoinManagerGroup')
      .then(() => console.log('✅ Groupe managers rejoint'))
      .catch(err => console.error('❌ JoinManagerGroup échoué:', err));
  }

  stopConnection(): void {
    this.hubConnection?.stop();
  }
}