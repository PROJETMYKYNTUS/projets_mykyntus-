// src/app/core/services/redirect.service.ts

import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthGuard } from '../../guard/guards/auth';

@Injectable({ providedIn: 'root' })
export class RedirectService {

  private readonly ADMIN_ROLES = ['Admin', 'RH'];

  constructor(private router: Router, private authGuard: AuthGuard) {}

  redirectAfterLogin(): void {
    const token = localStorage.getItem('token');

    if (!token) {
      window.location.href = 'http://localhost:4201/login';
      return;
    }

    const role = this.authGuard.getRole(token);

    console.log('=== RedirectService ===');
    console.log('Rôle :', role);
    console.log('Destination :', this.ADMIN_ROLES.includes(role) ? '/dashboard' : '/dashboard-employee');

    if (this.ADMIN_ROLES.includes(role)) {
      this.router.navigate(['/dashboard']);          // ← Admin + RH
    } else {
      this.router.navigate(['/dashboard-employee']); // ← tous les autres rôles
    }
  }
}