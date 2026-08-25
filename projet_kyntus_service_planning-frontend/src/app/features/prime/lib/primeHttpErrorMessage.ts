import { HttpErrorResponse } from '@angular/common/http';

/** Message générique affiché si le chargement des fiches échoue. */
export const PRIME_USER_LOAD_ERROR =
  'Impossible de charger les données pour le moment. Réessayez ultérieurement.';

/** Extrait un message d’erreur lisible depuis une réponse HTTP (sans exposer codes techniques). */
export function primeHttpErrorDetail(err: unknown): string | null {
  if (!(err instanceof HttpErrorResponse)) return null;
  const body = err.error;
  if (body && typeof body === 'object') {
    const o = body as { error?: unknown; detail?: unknown; title?: unknown; message?: unknown };
    for (const key of ['error', 'detail', 'title', 'message'] as const) {
      const v = o[key];
      if (typeof v === 'string') {
        const msg = v.trim();
        if (msg.length > 0 && !msg.startsWith('HTTP ') && !msg.includes('/api/')) return msg;
      }
    }
  }
  if (typeof body === 'string' && body.trim().length > 0 && body.length < 800) {
    const msg = body.trim();
    if (!msg.startsWith('HTTP ') && !msg.includes('/api/')) return msg;
  }
  return null;
}

/** Message d’erreur utilisateur : détail API, sinon message HTTP/Error, sinon fallback. */
export function primeHttpErrorMessage(err: unknown, fallback = 'Erreur'): string {
  const detail = primeHttpErrorDetail(err);
  if (detail) return detail;
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Impossible de joindre le serveur. Vérifiez votre connexion.';
    if (err.status >= 500) {
      return `Erreur serveur (${err.status}). Réessayez ou contactez l’administrateur.`;
    }
    if (err.message.trim()) return err.message;
  }
  if (err instanceof Error && err.message.trim()) return err.message;
  return fallback;
}
