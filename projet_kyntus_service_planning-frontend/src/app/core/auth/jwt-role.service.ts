import { Injectable } from '@angular/core';

/**
 * Lecture du rôle JWT — même claim que {@link AuthGuard} (AuthService / ClaimTypes.Role).
 * Centralise la logique pour les boutons Documentation et les routes /documentation/*.
 */
@Injectable({ providedIn: 'root' })
export class JwtRoleService {
  /** Identique à `AuthGuard` — valeur du claim « rôle » dans le payload JWT. */
  private readonly roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

  /** Rôle RH Planning (binôme) — nom exact dans le JWT / table `Roles` Auth. */
  static readonly planningRhRole = 'RH';

  /** Rôle pilote Documentation ; côté Planning, `Employee` et `Pilote` sont considérés équivalents. */
  static readonly documentationPilot = 'ROLE_PILOT';
  static readonly documentationPilotAuthorizedRoles: readonly string[] = ['Employee', 'Pilote', 'ROLE_PILOT'];

  /** Même périmètre que la route `dashboard` : Admin et RH accèdent au shell RH + Documentation RH. */
  static readonly documentationRhAuthorizedRoles: readonly string[] = ['Admin', 'RH'];

  /** Accès bouton / route Documentation RH (aligné noms rôles binôme). */
  hasDocumentationRhAccess(): boolean {
    const r = this.getRoleFromStorage();
    return JwtRoleService.documentationRhAuthorizedRoles.includes(r);
  }

  /** Accès bouton / route Documentation employé (Employee = Pilote). */
  hasDocumentationPilotAccess(): boolean {
    const r = this.getRoleFromStorage();
    return JwtRoleService.documentationPilotAuthorizedRoles.includes(r);
  }

  getRoleFromToken(token: string | null | undefined): string {
    if (!token) {
      return '';
    }
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload[this.roleClaim] || '';
    } catch {
      return '';
    }
  }

  getRoleFromStorage(): string {
    if (typeof localStorage === 'undefined') {
      return '';
    }
    return this.getRoleFromToken(localStorage.getItem('token'));
  }
}
