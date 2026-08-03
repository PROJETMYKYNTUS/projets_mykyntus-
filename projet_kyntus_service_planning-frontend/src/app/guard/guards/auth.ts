import { Injectable, inject } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, Router } from '@angular/router';
import { KYNTUS_JWT_CLAIMS } from '../../core/session/kyntus-session.constants';
import { clearStoredTokens, isJwtExpired, readStoredAccessToken, readStoredRefreshToken } from '../../core/session/kyntus-auth-token.util';
import { redirectToAuthLogin } from '../../core/session/kyntus-auth-refresh.service';
import { currentAppReturnUrl } from '../../core/session/kyntus-return-url.util';
import { roleNamesMatch } from '../../core/org/org-role-assignment';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  private readonly router = inject(Router);

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const token = readStoredAccessToken();
    const refresh = readStoredRefreshToken();
    if (!token && !refresh) {
      clearStoredTokens();
      localStorage.removeItem('user');
      redirectToAuthLogin(currentAppReturnUrl() ?? undefined);
      return false;
    }
    // Access expiré : on laisse passer si un refresh existe (renouvellement via interceptor).
    if (token && isJwtExpired(token) && !refresh) {
      clearStoredTokens();
      localStorage.removeItem('user');
      redirectToAuthLogin(currentAppReturnUrl() ?? undefined);
      return false;
    }

    const allowedRoles = route.data?.['roles'] as string[] | undefined;
    if (allowedRoles?.length) {
      const roleToken = token && !isJwtExpired(token) ? token : null;
      if (!roleToken) {
        // Rôle indisponible tant que le refresh n’a pas abouti — autoriser, le menu filtrera.
        return true;
      }
      const role = this.getRole(roleToken);
      const ok = allowedRoles.some((r) => roleNamesMatch(r, role));
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
}
