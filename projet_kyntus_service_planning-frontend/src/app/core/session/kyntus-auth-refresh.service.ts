import { Injectable } from '@angular/core';
import { KYNTUS_PUBLIC_URLS } from '../../config/kyntus-public-urls';
import {
  isJwtExpired,
  persistAccessTokens,
  readStoredRefreshToken,
  clearStoredTokens,
} from './kyntus-auth-token.util';

interface RefreshAuthResponse {
  accessToken?: string;
  refreshToken?: string;
}

let refreshInFlight: Promise<string | null> | null = null;

export async function refreshAccessTokenOnce(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;
  refreshInFlight = doRefreshAccessToken().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

async function doRefreshAccessToken(): Promise<string | null> {
  const refreshToken = readStoredRefreshToken();
  if (!refreshToken) return null;

  try {
    const res = await fetch('/api/Auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
    if (!res.ok) return null;

    const data = (await res.json()) as RefreshAuthResponse;
    const accessToken = data.accessToken?.trim();
    if (!accessToken || isJwtExpired(accessToken)) return null;

    persistAccessTokens(accessToken, data.refreshToken ?? refreshToken);
    return accessToken;
  } catch {
    return null;
  }
}

export function redirectToAuthLogin(returnUrl?: string): void {
  clearStoredTokens();
  localStorage.removeItem('user');
  const base = KYNTUS_PUBLIC_URLS.authLogin;
  const url =
    returnUrl && returnUrl.trim()
      ? `${base}?returnUrl=${encodeURIComponent(returnUrl.trim())}`
      : base;
  // replace : empêche le bouton Retour d’afficher une page SPA encore « authentifiée ».
  window.location.replace(url);
}

@Injectable({ providedIn: 'root' })
export class KyntusAuthRefreshService {
  refreshAccessToken(): Promise<string | null> {
    return refreshAccessTokenOnce();
  }

  redirectToLogin(): void {
    redirectToAuthLogin();
  }
}
