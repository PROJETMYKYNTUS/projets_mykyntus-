import { isPrimeDemoMockEnabled } from '../mock-data/prime-demo-config';
import { isPrimeDemoEmptyPayload, resolvePrimeDemoGet } from '../mock-data/prime-demo-resolver';

/** Même origine que l’app (proxy /api vers la gateway). */
function authHeaders(): Record<string, string> {
  const token = localStorage.getItem('token') || localStorage.getItem('accessToken');
  const h: Record<string, string> = {};
  if (token) h['Authorization'] = `Bearer ${token}`;
  return h;
}

export const PRIME_API_BASE =
  (import.meta as unknown as { env?: { VITE_PRIME_API_BASE_URL?: string } }).env?.VITE_PRIME_API_BASE_URL ?? '';

function applyDemoFallback<T>(path: string, data: T): T {
  if (!isPrimeDemoMockEnabled()) return data;
  if (!path.includes('/api/prime') && !path.includes('/api/rp')) return data;
  const mock = resolvePrimeDemoGet(path, 'GET');
  if (mock === undefined) return data;
  if (isPrimeDemoEmptyPayload(data)) return mock as T;
  return data;
}

export async function primeApiGet<T>(path: string): Promise<T> {
  const full = `${PRIME_API_BASE}${path}`;
  try {
    const res = await fetch(full, { credentials: 'include', headers: authHeaders() });
    if (!res.ok) {
      const mock = isPrimeDemoMockEnabled() ? resolvePrimeDemoGet(path, 'GET') : undefined;
      if (mock !== undefined) return mock as T;
      const t = await res.text();
      throw new Error(t || `HTTP ${res.status}`);
    }
    const data = (await res.json()) as T;
    return applyDemoFallback(path, data);
  } catch (e) {
    const mock = isPrimeDemoMockEnabled() ? resolvePrimeDemoGet(path, 'GET') : undefined;
    if (mock !== undefined) return mock as T;
    throw e;
  }
}

export async function primeApiPut<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${PRIME_API_BASE}${path}`, {
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
  const res = await fetch(`${PRIME_API_BASE}${path}`, {
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
