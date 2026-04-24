import { Injectable } from '@angular/core';
import { PERMISSIONS } from '../models/roles.model';
import { AuthGuard } from '../../guard/guards/auth'; // ← réutilise ton guard existant

@Injectable({ providedIn: 'root' })
export class PermissionService {

  constructor(private authGuard: AuthGuard) {}

  private getUserRole(): string {
    const token = localStorage.getItem('token');
    if (!token) return '';
    return this.authGuard.getRole(token); // ← utilise ta méthode existante
  }

  hasPermission(feature: string, action: string): boolean {
    const userRole = this.getUserRole();
    const allowed: string[] = PERMISSIONS[feature]?.[action] ?? [];
    return allowed.includes(userRole);
  }
}