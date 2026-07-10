import { Injectable } from '@angular/core';
import { KYNTUS_JWT_CLAIMS, type KyntusStoredUser } from './kyntus-session.constants';
import {
  clearStoredTokens,
  isJwtExpired,
  persistAccessTokens,
  readStoredAccessToken,
  readStoredRefreshToken,
} from './kyntus-auth-token.util';

@Injectable({ providedIn: 'root' })
export class KyntusSessionService {
  getToken(): string | null {
    const token = readStoredAccessToken();
    if (!token) return null;
    if (isJwtExpired(token)) {
      this.clearAuthStorage();
      return null;
    }
    return token;
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  persistSession(accessToken: string, refreshToken?: string | null): void {
    persistAccessTokens(accessToken, refreshToken);
  }

  clearSession(): void {
    clearStoredTokens();
    localStorage.removeItem('user');
  }

  getRefreshToken(): string | null {
    return readStoredRefreshToken();
  }

  private clearAuthStorage(): void {
    clearStoredTokens();
  }

  getStoredUser(): KyntusStoredUser | null {
    try {
      const raw = localStorage.getItem('user');
      if (!raw) return null;
      const u = JSON.parse(raw) as Partial<KyntusStoredUser>;
      if (!u || typeof u !== 'object') return null;
      return {
        id: u.id ?? 0,
        authUserId: typeof u.authUserId === 'number' ? u.authUserId : undefined,
        subjectId: typeof u.subjectId === 'string' ? u.subjectId : undefined,
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
    const stored = this.getStoredUser();
    if (stored?.authUserId && stored.authUserId > 0) return stored.authUserId;
    const legacyId = stored?.id;
    if (typeof legacyId === 'number' && legacyId > 0) return legacyId;
    if (typeof legacyId === 'string' && /^\d+$/.test(legacyId.trim())) {
      return parseInt(legacyId.trim(), 10);
    }
    const raw = this.getJwtPayload()[KYNTUS_JWT_CLAIMS.nameIdentifier];
    const n = typeof raw === 'string' ? parseInt(raw, 10) : Number(raw);
    return Number.isFinite(n) ? n : 0;
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
