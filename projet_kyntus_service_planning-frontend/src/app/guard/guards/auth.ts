import { Injectable, inject } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, Router } from '@angular/router';
import { KYNTUS_PUBLIC_URLS } from '../../config/kyntus-public-urls';
import { KYNTUS_JWT_CLAIMS } from '../../core/session/kyntus-session.constants';
import { clearStoredTokens, isJwtExpired, readStoredAccessToken } from '../../core/session/kyntus-auth-token.util';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  private readonly router = inject(Router);

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const token = readStoredAccessToken();
    if (!token || isJwtExpired(token)) {
      clearStoredTokens();
      localStorage.removeItem('user');
      this.redirectToLogin();
      return false;
    }

    const allowedRoles = route.data?.['roles'] as string[] | undefined;
    if (allowedRoles?.length) {
      const role = this.getRole(token);
      const normalized = role.toLowerCase();
      const ok = allowedRoles.some((r) => r.toLowerCase() === normalized);
      if (!ok) {
        this.router.navigate(['/unauthorized']);
        return false;
      }
    }

    return true;
  }

  isAdminRole(token: string): boolean {
    const role = this.getRole(token);
    return ['Admin', 'RH'].includes(role);
  }

  getRole(token: string): string {
    try {
      const payload = JSON.parse(atob(token.split('.')[1])) as Record<string, unknown>;
      const role = payload[KYNTUS_JWT_CLAIMS.role];
      return typeof role === 'string' ? role : '';
    } catch {
      return '';
    }
  }

  isTokenExpired(token: string): boolean {
    return isJwtExpired(token);
  }

  private redirectToLogin(): void {
    window.location.href = KYNTUS_PUBLIC_URLS.authLogin;
  }
}
