// src/app/core/services/redirect.service.ts

import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { redirectToAuthLogin } from '../session/kyntus-auth-refresh.service';
import { readStoredAccessToken } from '../session/kyntus-auth-token.util';
import {
  clearPersistedReturnUrl,
  resolveReturnUrl,
} from '../session/kyntus-return-url.util';

@Injectable({ providedIn: 'root' })
export class RedirectService {

  constructor(private router: Router) {}

  redirectAfterLogin(queryReturnUrl?: string | null): void {
    const token = readStoredAccessToken();

    if (!token) {
      redirectToAuthLogin();
      return;
    }

    const target = resolveReturnUrl(queryReturnUrl) ?? '/home';
    clearPersistedReturnUrl();

    // replaceUrl : retire auth-callback (+ tokens query) de l’historique navigateur.
    void this.router.navigateByUrl(target, { replaceUrl: true });
  }
}
