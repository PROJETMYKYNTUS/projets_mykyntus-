import { HttpErrorResponse } from '@angular/common/http';

export function formatHttpErrorMessage(
  err: unknown,
  fallback = 'Une erreur est survenue. Réessayez ultérieurement.',
): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body && typeof body === 'object') {
      const record = body as Record<string, unknown>;
      const message = record['message'];
      if (typeof message === 'string' && message.trim()) {
        return message.trim();
      }
      const error = record['error'];
      if (typeof error === 'string' && error.trim()) {
        return error.trim();
      }
      const title = record['title'];
      if (typeof title === 'string' && title.trim()) {
        return title.trim();
      }
    }
    if (typeof body === 'string' && body.trim()) {
      return body.trim();
    }
    if (err.status === 0) {
      return 'Impossible de contacter le serveur.';
    }
    if (err.status === 409) {
      return 'Conflit — cette opération n\'a pas pu être appliquée.';
    }
    if (err.status === 404) {
      return 'Ressource introuvable.';
    }
    if (err.statusText?.trim()) {
      return err.status ? `${err.status} — ${err.statusText}` : err.statusText;
    }
    return fallback;
  }

  if (err instanceof Error && err.message.trim()) {
    return err.message.trim();
  }

  return fallback;
}
