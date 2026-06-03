import { Injectable } from '@angular/core';
import { KYNTUS_JWT_CLAIMS, type KyntusStoredUser } from './kyntus-session.constants';

@Injectable({ providedIn: 'root' })
export class KyntusSessionService {
  getToken(): string | null {
    return localStorage.getItem('token') || localStorage.getItem('accessToken');
  }

  getStoredUser(): KyntusStoredUser | null {
    try {
      const raw = localStorage.getItem('user');
      if (!raw) return null;
      const u = JSON.parse(raw) as Partial<KyntusStoredUser>;
      if (!u || typeof u !== 'object') return null;
      return {
        id: Number(u.id) || 0,
        username: String(u.username ?? ''),
        email: String(u.email ?? '').trim(),
        role: String(u.role ?? '').trim(),
      };
    } catch {
      return null;
    }
  }

  getEmail(): string {
    const fromUser = this.getStoredUser()?.email;
    if (fromUser?.includes('@')) return fromUser;
    const fromJwt = this.getJwtPayload()[KYNTUS_JWT_CLAIMS.email];
    if (typeof fromJwt === 'string' && fromJwt.includes('@')) return fromJwt.trim();
    const name = this.getStoredUser()?.username ?? '';
    return name.includes('@') ? name.trim() : '';
  }

  getRole(): string {
    const fromJwt = this.getJwtRole();
    if (fromJwt) return fromJwt;
    return this.getStoredUser()?.role?.trim() ?? '';
  }

  getSubjectId(): string | null {
    const sub = this.getJwtPayload()['sub'];
    return typeof sub === 'string' && sub.length > 0 ? sub : null;
  }

  getAuthUserId(): number {
    const fromUser = this.getStoredUser()?.id;
    if (fromUser && fromUser > 0) return fromUser;
    const raw = this.getJwtPayload()[KYNTUS_JWT_CLAIMS.nameIdentifier];
    const n = typeof raw === 'string' ? parseInt(raw, 10) : Number(raw);
    return Number.isFinite(n) ? n : 0;
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  private getJwtRole(): string {
    const v = this.getJwtPayload()[KYNTUS_JWT_CLAIMS.role];
    return typeof v === 'string' ? v.trim() : '';
  }

  private getJwtPayload(): Record<string, unknown> {
    const token = this.getToken();
    if (!token) return {};
    try {
      const part = token.split('.')[1];
      if (!part) return {};
      return JSON.parse(atob(part)) as Record<string, unknown>;
    } catch {
      return {};
    }
  }
}
