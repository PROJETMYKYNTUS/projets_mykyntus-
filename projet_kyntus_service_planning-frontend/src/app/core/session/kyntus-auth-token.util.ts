export const KYNTUS_ACCESS_TOKEN_KEYS = ['token', 'accessToken', 'access_token'] as const;
export const KYNTUS_REFRESH_TOKEN_KEYS = ['refreshToken', 'refresh_token'] as const;

export function readStoredAccessToken(): string | null {
  for (const key of KYNTUS_ACCESS_TOKEN_KEYS) {
    const value = localStorage.getItem(key)?.trim();
    if (value) return value;
  }
  return null;
}

export function readStoredRefreshToken(): string | null {
  for (const key of KYNTUS_REFRESH_TOKEN_KEYS) {
    const value = localStorage.getItem(key)?.trim();
    if (value) return value;
  }
  return null;
}

/** Même règle partout : exp absent = valide côté client ; léger skew pour l'horloge locale. */
export function isJwtExpired(token: string, skewMs = 0): boolean {
  try {
    const part = token.split('.')[1];
    if (!part) return true;
    const payload = JSON.parse(atob(part)) as { exp?: unknown };
    const exp = typeof payload.exp === 'number' ? payload.exp : Number(payload.exp);
    if (!Number.isFinite(exp) || exp <= 0) return false;
    return exp * 1000 <= Date.now() + skewMs;
  } catch {
    return true;
  }
}

export function persistAccessTokens(accessToken: string, refreshToken?: string | null): void {
  const trimmed = accessToken.trim();
  if (!trimmed) return;
  for (const key of KYNTUS_ACCESS_TOKEN_KEYS) {
    localStorage.setItem(key, trimmed);
  }
  if (refreshToken?.trim()) {
    for (const key of KYNTUS_REFRESH_TOKEN_KEYS) {
      localStorage.setItem(key, refreshToken.trim());
    }
  }
}

export function clearStoredTokens(): void {
  for (const key of KYNTUS_ACCESS_TOKEN_KEYS) localStorage.removeItem(key);
  for (const key of KYNTUS_REFRESH_TOKEN_KEYS) localStorage.removeItem(key);
}

export function bearerAuthHeader(token: string | null | undefined): Record<string, string> {
  const trimmed = token?.trim();
  return trimmed ? { Authorization: `Bearer ${trimmed}` } : {};
}
