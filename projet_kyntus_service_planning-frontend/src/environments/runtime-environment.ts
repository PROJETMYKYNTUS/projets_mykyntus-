declare global {
  interface Window {
    __env?: {
      API_BASE_URL?: string;
    };
  }
}

function isLegacyDocumentationBackendPort(url: string): boolean {
  try {
    const u = url.trim();
    if (!/^https?:\/\//i.test(u)) {
      return false;
    }
    const { port } = new URL(u);
    return port === '5002';
  } catch {
    return false;
  }
}

function normalizeDocumentationApiBaseUrl(resolved: string): string {
  const t = resolved.trim();
  if (t === '/api') {
    return '';
  }
  return t;
}

export function getRuntimeApiBaseUrl(fallback: string): string {
  const runtimeValue = window.__env?.API_BASE_URL?.trim();
  let chosen = runtimeValue && runtimeValue.length > 0 ? runtimeValue : fallback;
  if (isLegacyDocumentationBackendPort(chosen)) {
    chosen = fallback;
  }
  return normalizeDocumentationApiBaseUrl(chosen);
}
