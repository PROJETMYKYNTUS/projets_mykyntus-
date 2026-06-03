import { HttpErrorResponse } from '@angular/common/http';

/** Message générique affiché si le chargement des fiches échoue. */
export const PRIME_USER_LOAD_ERROR =
  'Impossible de charger les données pour le moment. Réessayez ultérieurement.';

/** Extrait un message d’erreur lisible depuis une réponse HTTP (sans exposer codes techniques). */
export function primeHttpErrorDetail(err: unknown): string | null {
  if (!(err instanceof HttpErrorResponse)) return null;
  const body = err.error;
  if (body && typeof body === 'object' && 'error' in body && typeof (body as { error: unknown }).error === 'string') {
    const msg = (body as { error: string }).error.trim();
    if (msg.length > 0 && !msg.startsWith('HTTP ') && !msg.includes('/api/')) return msg;
  }
  if (typeof body === 'string' && body.trim().length > 0 && body.length < 800) {
    const msg = body.trim();
    if (!msg.startsWith('HTTP ') && !msg.includes('/api/')) return msg;
  }
  return null;
}
