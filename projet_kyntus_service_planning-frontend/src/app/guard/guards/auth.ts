import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, Router } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {

  private readonly ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

  constructor(private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const token = localStorage.getItem('access_token');

    if (!token) {
      window.location.href = 'http://localhost:4201/login';
      return false;
    }

    if (this.isTokenExpired(token)) {
      localStorage.removeItem('access_token');
      window.location.href = 'http://localhost:4201/login';
      return false;
    }

    // Si la route exige un rôle spécifique
    const allowedRoles = route.data?.['roles'] as string[];
    if (allowedRoles?.length) {
      const role = this.getRole(token);
      if (!allowedRoles.includes(role)) {
        this.router.navigate(['/unauthorized']);
        return false;
      }
    }

    return true;
  }

  getRole(token: string): string {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload[this.ROLE_CLAIM] || '';
    } catch {
      return '';
    }
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