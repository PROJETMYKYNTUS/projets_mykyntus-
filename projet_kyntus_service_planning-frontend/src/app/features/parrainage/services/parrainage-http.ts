/**
 * Couche HTTP bas niveau du module parrainage.
 * Même origine que l'app : le nginx du conteneur proxy `/api/` vers l'API gateway,
 * qui route `/api/parrainage/*` vers `parrainage-backend`.
 * Calquée sur prime-angular/src/app/prime/services/prime-http.ts.
 */
export const PARRAINAGE_API_BASE =
  (import.meta as unknown as { env?: { NG_APP_PARRAINAGE_API_BASE?: string } }).env?.NG_APP_PARRAINAGE_API_BASE ?? '';

/** En-têtes démo (rôle/utilisateur courant) repris par le backend pour le filtrage. */
let demoHeaders: Record<string, string> = {};

export function setParrainageDemoContext(role: string, userId: string, projectId?: string): void {
  demoHeaders = {
    'X-Parrainage-Role': role,
    'X-Parrainage-User-Id': userId,
    ...(projectId ? { 'X-Parrainage-Project-Id': projectId } : {}),
  };
}

function authHeaders(): Record<string, string> {
  const token = localStorage.getItem('token') || localStorage.getItem('accessToken');
  const h: Record<string, string> = {};
  if (token) h['Authorization'] = `Bearer ${token}`;
  return h;
}

function buildHeaders(json: boolean): Record<string, string> {
  return {
    ...(json ? { 'Content-Type': 'application/json' } : {}),
    ...authHeaders(),
    ...demoHeaders,
  };
}

async function parseError(res: Response): Promise<never> {
  const text = await res.text().catch(() => '');
  throw new Error(text || `HTTP ${res.status}`);
}

export async function parrainageApiGet<T>(path: string): Promise<T> {
  const res = await fetch(`${PARRAINAGE_API_BASE}${path}`, {
    credentials: 'include',
    headers: buildHeaders(false),
  });
  if (!res.ok) return parseError(res);
  return res.json() as Promise<T>;
}

export async function parrainageApiSend<T>(
  method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
  path: string,
  body?: unknown,
): Promise<T> {
  const res = await fetch(`${PARRAINAGE_API_BASE}${path}`, {
    method,
    credentials: 'include',
    headers: buildHeaders(body !== undefined),
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
  if (!res.ok) return parseError(res);
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
