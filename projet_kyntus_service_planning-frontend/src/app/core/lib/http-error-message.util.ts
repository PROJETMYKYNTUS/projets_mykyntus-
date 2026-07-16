import { HttpErrorResponse } from '@angular/common/http';

function collectAspNetValidationMessages(errors: unknown): string[] {
  if (!errors || typeof errors !== 'object') return [];
  const messages: string[] = [];
  for (const [key, value] of Object.entries(errors as Record<string, unknown>)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        if (typeof item === 'string' && item.trim()) {
          messages.push(key.startsWith('$') ? item.trim() : `${key}: ${item.trim()}`);
        }
      }
    } else if (typeof value === 'string' && value.trim()) {
      messages.push(value.trim());
    }
  }
  return messages;
}

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
      const validation = collectAspNetValidationMessages(record['errors']);
      if (validation.length > 0) {
        return validation.slice(0, 4).join(' · ');
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
      return "Conflit — cette opération n'a pas pu être appliquée.";
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
