declare global {
  interface Window {
    __env?: {
      API_BASE_URL?: string;
    };
  }
}

export function getRuntimeApiBaseUrl(fallback: string): string {
  const raw = window.__env?.API_BASE_URL?.trim() ?? '';
  const resolved = raw.length > 0 ? raw : fallback;
  // Les services font `${apiBaseUrl}/api/documentation/...`. Si apiBaseUrl vaut `/api` (ancien env.js
  // ou mauvaise config), on obtient `/api/api/...` → 404 derrière Nginx.
  if (resolved === '/api') {
    return '';
  }
  return resolved;
}
