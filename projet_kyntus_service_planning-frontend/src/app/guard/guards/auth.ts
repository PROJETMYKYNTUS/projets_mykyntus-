import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, Router } from '@angular/router';
import { JwtRoleService } from '../../core/auth/jwt-role.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {

  constructor(
    private router: Router,
    private readonly jwtRole: JwtRoleService,
  ) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const token = localStorage.getItem('token');
  console.log('=== AuthGuard ===');
  console.log('Token présent :', !!token);
  console.log('Token valeur :', token);
  console.log('isExpired :', token ? this.isTokenExpired(token) : 'N/A');
  console.log('Route roles :', route.data?.['roles']);
  console.log('Role extrait :', token ? this.getRole(token) : 'N/A');
    if (!token) {
      window.location.href = 'http://localhost:4201/login';
      return false;
    }

    if (this.isTokenExpired(token)) {
      localStorage.removeItem('token');
      window.location.href = 'http://localhost:4201/login';
      return false;
    }

    // Si la route exige un rôle spécifique
    const allowedRoles = route.data?.['roles'] as string[];
    if (allowedRoles?.length) {
      const role = this.jwtRole.getRoleFromToken(token);
      if (!allowedRoles.includes(role)) {
        this.router.navigate(['/unauthorized']);
        return false;
      }
    }

    return true;
  }
  
  isAdminRole(token: string): boolean {
    const role = this.jwtRole.getRoleFromToken(token);
    return ['Admin', 'RH'].includes(role);
  }

  /** Exposé pour les composants qui dupliquaient le décodage JWT (ex. boutons Documentation). */
  getRole(token: string): string {
    return this.jwtRole.getRoleFromToken(token);
  }

  isTokenExpired(token: string): boolean {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 < Date.now();
    } catch {
      return true;
    }
  }
}