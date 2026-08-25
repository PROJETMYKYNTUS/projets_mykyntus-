import { HttpErrorResponse } from '@angular/common/http';

/** True if the string looks like a human-facing message (not code / ProblemDetails). */
function looksLikeUserMessage(text: string): boolean {
  const t = text.trim();
  if (!t || t.length > 280) return false;
  // HTTP codes, stack-ish, GUIDs, ASP.NET property keys, JSON blobs
  if (/^\d{3}\b/.test(t)) return false;
  if (/^[A-Z][a-zA-Z0-9_]*Exception\b/.test(t)) return false;
  if (/^[A-Z][a-zA-Z0-9_]+:\s/.test(t) && !/\s/.test(t.slice(0, t.indexOf(':')))) return false;
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-/i.test(t)) return false;
  if (t.startsWith('{') || t.startsWith('[')) return false;
  if (/\bat\s+\w+\.|Stack trace|System\./i.test(t)) return false;
  // English ProblemDetails titles
  if (/^(One or more validation errors occurred|An error occurred|Bad Request|Not Found|Unauthorized|Forbidden|Internal Server Error)\.?$/i.test(t)) {
    return false;
  }
  return true;
}

function collectUserValidationMessages(errors: unknown): string[] {
  if (!errors || typeof errors !== 'object') return [];
  const messages: string[] = [];
  for (const [, value] of Object.entries(errors as Record<string, unknown>)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        if (typeof item === 'string' && looksLikeUserMessage(item)) {
          messages.push(item.trim());
        }
      }
    } else if (typeof value === 'string' && looksLikeUserMessage(value)) {
      messages.push(value.trim());
    }
  }
  return messages;
}

function statusFallback(status: number, fallback: string): string {
  switch (status) {
    case 0:
      return 'Impossible de contacter le serveur. Vérifiez votre connexion.';
    case 401:
      return 'Votre session a expiré. Veuillez vous reconnecter.';
    case 403:
      return "Vous n'avez pas l'autorisation d'effectuer cette action.";
    case 404:
      return 'Élément introuvable.';
    case 409:
      return "Cette opération n'a pas pu être appliquée (conflit).";
    default:
      if (status >= 500) {
        return 'Le service est temporairement indisponible. Réessayez plus tard.';
      }
      return fallback;
  }
}

/**
 * Message d'erreur affichable à l'utilisateur (FR métier).
 * Ne renvoie jamais de codes HTTP, titres ProblemDetails anglais, ni `PropertyName: …`.
 */
export function formatHttpErrorMessage(
  err: unknown,
  fallback = 'Une erreur est survenue. Réessayez ultérieurement.',
): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body && typeof body === 'object') {
      const record = body as Record<string, unknown>;
      const message = record['message'];
      if (typeof message === 'string' && looksLikeUserMessage(message)) {
        return message.trim();
      }
      const error = record['error'];
      if (typeof error === 'string' && looksLikeUserMessage(error)) {
        return error.trim();
      }
      const validation = collectUserValidationMessages(record['errors']);
      if (validation.length > 0) {
        return validation.slice(0, 3).join(' · ');
      }
      // Ignore English ProblemDetails `title`
    }
    if (typeof body === 'string' && looksLikeUserMessage(body)) {
      return body.trim();
    }
    return statusFallback(err.status, fallback);
  }

  if (err instanceof Error && looksLikeUserMessage(err.message)) {
    return err.message.trim();
  }

  return fallback;
}
