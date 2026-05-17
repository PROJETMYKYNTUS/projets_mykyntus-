/** Même origine que l’app (proxy /api vers la gateway). */
export const PRIME_API_BASE =
  (import.meta as unknown as { env?: { VITE_PRIME_API_BASE_URL?: string } }).env?.VITE_PRIME_API_BASE_URL ?? '';

export async function primeApiGet<T>(path: string): Promise<T> {
  const res = await fetch(`${PRIME_API_BASE}${path}`, { credentials: 'include' });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export async function primeApiPut<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${PRIME_API_BASE}${path}`, {
    method: 'PUT',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
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
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(t || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}
