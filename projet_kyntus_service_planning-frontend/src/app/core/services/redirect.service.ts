// src/app/core/services/redirect.service.ts

import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthGuard } from '../../guard/guards/auth';
import { KYNTUS_PUBLIC_URLS } from '../../config/kyntus-public-urls';

@Injectable({ providedIn: 'root' })
export class RedirectService {

  constructor(private router: Router, private authGuard: AuthGuard) {}

  redirectAfterLogin(): void {
    const token = localStorage.getItem('token');

    if (!token) {
      window.location.href = KYNTUS_PUBLIC_URLS.authLogin;
      return;
    }

    // Atterrissage unifié : le shell + menu global des microservices décide de l'affichage selon le rôle.
    this.router.navigate(['/home']);
  }
}