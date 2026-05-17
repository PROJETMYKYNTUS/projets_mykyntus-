import { HttpErrorResponse } from '@angular/common/http';

/** Extrait un message d’erreur lisible depuis une réponse API Prime (middleware JSON). */
export function primeHttpErrorDetail(err: unknown): string | null {
  if (!(err instanceof HttpErrorResponse)) return null;
  const body = err.error;
  if (body && typeof body === 'object' && 'error' in body && typeof (body as { error: unknown }).error === 'string') {
    return (body as { error: string }).error;
  }
  if (typeof body === 'string' && body.trim().length > 0 && body.length < 800) return body.trim();
  if (err.status > 0) return `HTTP ${err.status} ${err.statusText || ''}`.trim();
  return null;
}
