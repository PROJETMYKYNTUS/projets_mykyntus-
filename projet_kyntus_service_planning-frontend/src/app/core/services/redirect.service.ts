// src/app/core/services/redirect.service.ts

import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { redirectToAuthLogin } from '../session/kyntus-auth-refresh.service';
import { readStoredAccessToken } from '../session/kyntus-auth-token.util';

@Injectable({ providedIn: 'root' })
export class RedirectService {

  constructor(private router: Router) {}

  redirectAfterLogin(): void {
    const token = readStoredAccessToken();

    if (!token) {
      redirectToAuthLogin();
      return;
    }

    // replaceUrl : retire auth-callback (+ tokens query) de l’historique navigateur.
    void this.router.navigate(['/home'], { replaceUrl: true });
  }
}
