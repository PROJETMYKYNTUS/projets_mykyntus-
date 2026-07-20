import {
  bearerAuthHeader,
  isJwtExpired,
  readStoredAccessToken,
  readStoredRefreshToken,
} from '../../../core/session/kyntus-auth-token.util';
import {
  refreshAccessTokenOnce,
  redirectToAuthLogin,
} from '../../../core/session/kyntus-auth-refresh.service';

/** Même origine que l’app (proxy /api vers la gateway). */
function authHeaders(): Record<string, string> {
  const token = readStoredAccessToken();
  if (!token || isJwtExpired(token)) return {};
  return bearerAuthHeader(token);
}

async function isPrimeUserUnresolved(res: Response): Promise<boolean> {
  try {
    const clone = res.clone();
    const text = (await clone.text()).toLowerCase();
    return text.includes('utilisateur prime non résolu');
  } catch {
    return false;
  }
}

async function fetchWithAuth(input: string, init: RequestInit): Promise<Response> {
  const first = await fetch(input, init);
  // 403 métier : JWT OK mais fiche Prime absente — ne pas tenter de refresh/logout.
  if (first.status === 403 && (await isPrimeUserUnresolved(first))) return first;
  if (first.status !== 401) return first;

  // JWT OK mais employé absent de Prime : erreur métier, pas une session morte.
  if (await isPrimeUserUnresolved(first)) return first;

  if (!readStoredRefreshToken()) {
    redirectToAuthLogin();
    return first;
  }

  const refreshed = await refreshAccessTokenOnce();
  if (!refreshed) {
    redirectToAuthLogin();
    return first;
  }

  const retryHeaders = {
    ...(init.headers as Record<string, string> | undefined),
    ...bearerAuthHeader(refreshed),
  };
  const retry = await fetch(input, { ...init, headers: retryHeaders });
  if (retry.status === 401 && (await isPrimeUserUnresolved(retry))) return retry;
  if (retry.status === 401) {
    redirectToAuthLogin();
  }
  return retry;
}

export const PRIME_API_BASE =
  (import.meta as unknown as { env?: { VITE_PRIME_API_BASE_URL?: string } }).env?.VITE_PRIME_API_BASE_URL ?? '';

export async function primeApiGet<T>(path: string): Promise<T> {
  const full = `${PRIME_API_BASE}${path}`;
  const res = await fetchWithAuth(full, { credentials: 'include', headers: authHeaders() });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `HTTP ${res.status}`);
  }
  return (await res.json()) as T;
}

export async function primeApiPut<T>(path: string, body: unknown): Promise<T> {
  const res = await fetchWithAuth(`${PRIME_API_BASE}${path}`, {
    method: 'PUT',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export async function primeApiPost<T>(path: string, body: unknown): Promise<T> {
  const res = await fetchWithAuth(`${PRIME_API_BASE}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export async function primeApiPatch<T>(path: string, body: unknown): Promise<T> {
  const res = await fetchWithAuth(`${PRIME_API_BASE}${path}`, {
    method: 'PATCH',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export async function primeApiDelete<T>(path: string): Promise<T> {
  const res = await fetchWithAuth(`${PRIME_API_BASE}${path}`, {
    method: 'DELETE',
    credentials: 'include',
    headers: authHeaders(),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}
