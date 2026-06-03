import { Injectable, inject } from '@angular/core';
import { KyntusSessionService } from '../session/kyntus-session.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly session = inject(KyntusSessionService);

  getAuthUserId(): number { return this.session.getAuthUserId(); }
  getRole(): string { return this.session.getRole(); }
  getEmail(): string { return this.session.getEmail(); }
}
