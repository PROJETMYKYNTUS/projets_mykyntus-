import { Injectable } from '@angular/core';
import type { KyntusStoredUser } from './kyntus-session.constants';
import {
  clearStoredTokens,
  isJwtExpired,
  persistAccessTokens,
  readStoredAccessToken,
  readStoredRefreshToken,
  decodeJwtPayload,
  readJwtEmail,
  readJwtNameIdentifier,
  readJwtRole,
} from './kyntus-auth-token.util';

@Injectable({ providedIn: 'root' })
export class KyntusSessionService {
  getToken(): string | null {
    const token = readStoredAccessToken();
    if (!token) return null;
    // Access expiré : ne pas effacer le refresh — l’interceptor pourra renouveler.
    if (isJwtExpired(token)) return null;
    return token;
  }

  isAuthenticated(): boolean {
    if (this.getToken()) return true;
    // Access expiré mais refresh encore présent → session renouvelable.
    return !!this.getRefreshToken();
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
    const token = readStoredAccessToken();
    const fromJwt = token ? readJwtEmail(token) : '';
    if (fromJwt.includes('@')) return fromJwt.trim();
    const name = this.getStoredUser()?.username ?? '';
    return name.includes('@') ? name.trim() : '';
  }

  getRole(): string {
    const fromJwt = this.getJwtRole();
    if (fromJwt) return fromJwt;
    return this.getStoredUser()?.role?.trim() ?? '';
  }

  getSubjectId(): string | null {
    const token = readStoredAccessToken();
    const payload = token ? decodeJwtPayload(token) : null;
    const sub = payload?.['sub'];
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
    const token = readStoredAccessToken();
    const raw = token ? readJwtNameIdentifier(token) : '';
    const n = parseInt(raw, 10);
    return Number.isFinite(n) ? n : 0;
  }

  private getJwtRole(): string {
    const token = readStoredAccessToken();
    if (!token) return '';
    return readJwtRole(token);
  }
}
