// src/app/core/services/auth.service.ts
import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly NAME_ID  = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
  private readonly ROLE_KEY = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

  private get payload(): any {
    try {
      return JSON.parse(atob((localStorage.getItem('token') || '').split('.')[1]));
    } catch { return {}; }
  }

  getAuthUserId(): number { return +this.payload[this.NAME_ID]; }
  getRole(): string       { return this.payload[this.ROLE_KEY] || ''; }
}

