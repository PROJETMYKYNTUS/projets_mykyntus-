/** Clé sessionStorage — survit au clear des tokens localStorage. */
export const KYNTUS_RETURN_URL_KEY = 'kyntus.returnUrl';

const BLOCKED_PATH_PREFIXES = ['/auth-callback', '/login'];

/**
 * Valide une URL de retour interne (chemin relatif SPA uniquement).
 * Rejette les URLs absolues / protocol-relative / externes.
 */
export function sanitizeReturnUrl(raw: string | null | undefined): string | null {
  if (!raw) return null;
  let value = raw.trim();
  if (!value) return null;

  try {
    value = decodeURIComponent(value);
  } catch {
    // garder la valeur brute si déjà décodée
  }

  if (!value.startsWith('/') || value.startsWith('//')) return null;
  if (/^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(value)) return null;

  const pathOnly = value.split(/[?#]/)[0] ?? value;
  if (BLOCKED_PATH_PREFIXES.some((p) => pathOnly === p || pathOnly.startsWith(`${p}/`))) {
    return null;
  }

  return value;
}

/** URL courante (path + search + hash), ou null si page auth. */
export function currentAppReturnUrl(): string | null {
  if (typeof window === 'undefined') return null;
  const url = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  return sanitizeReturnUrl(url);
}

export function persistReturnUrl(returnUrl: string | null | undefined): void {
  const safe = sanitizeReturnUrl(returnUrl);
  if (typeof sessionStorage === 'undefined') return;
  if (!safe) {
    sessionStorage.removeItem(KYNTUS_RETURN_URL_KEY);
    return;
  }
  sessionStorage.setItem(KYNTUS_RETURN_URL_KEY, safe);
}

export function readPersistedReturnUrl(): string | null {
  if (typeof sessionStorage === 'undefined') return null;
  return sanitizeReturnUrl(sessionStorage.getItem(KYNTUS_RETURN_URL_KEY));
}

export function clearPersistedReturnUrl(): void {
  if (typeof sessionStorage === 'undefined') return;
  sessionStorage.removeItem(KYNTUS_RETURN_URL_KEY);
}

/** Résout returnUrl depuis query, puis sessionStorage. */
export function resolveReturnUrl(queryReturnUrl?: string | null): string | null {
  return sanitizeReturnUrl(queryReturnUrl) ?? readPersistedReturnUrl();
}
