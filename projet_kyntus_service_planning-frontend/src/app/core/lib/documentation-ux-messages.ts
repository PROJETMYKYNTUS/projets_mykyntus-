import { HttpErrorResponse } from '@angular/common/http';

function friendlyFallbackForStatus(status: number): string {
  switch (status) {
    case 0:
      return 'Connexion impossible pour le moment. Vérifiez votre réseau puis réessayez.';
    case 400:
      return 'Certaines informations sont invalides. Vérifiez les champs puis réessayez.';
    case 401:
    case 403:
      return 'Vous n’avez pas les droits nécessaires pour effectuer cette action.';
    case 404:
      return 'L’élément demandé est introuvable ou n’est plus disponible.';
    case 409:
      return 'Cette action ne peut pas être terminée pour le moment car les données sont déjà utilisées.';
    case 422:
      return 'Certaines informations doivent être corrigées avant de continuer.';
    case 500:
    case 502:
    case 503:
    case 504:
      return 'Le service rencontre un problème temporaire. Merci de réessayer dans un instant.';
    default:
      return 'Une erreur inattendue est survenue. Merci de réessayer.';
  }
}

function normalizeBackendMessage(message: string): string {
  const value = message.trim();
  const lower = value.toLowerCase();

  if (!value) return '';
  if (lower.includes('field_required') || lower.includes('required field')) {
    return 'Veuillez renseigner les champs obligatoires avant de continuer.';
  }
  if (lower.includes('tenant') && lower.includes('header')) {
    return 'Le contexte de votre espace n’a pas pu être identifié. Rechargez la page puis réessayez.';
  }
  if (lower.includes('identity') || lower.includes('user context')) {
    return 'Votre session est incomplète. Rechargez la page puis réessayez.';
  }
  if (lower.includes('timeout')) {
    return 'Le traitement a pris trop de temps. Merci de réessayer.';
  }
  if (lower.includes('network') || lower.includes('failed to fetch')) {
    return 'Connexion impossible pour le moment. Vérifiez votre réseau puis réessayez.';
  }
  if (lower.includes('duplicate') || lower.includes('already exists')) {
    return 'Cette information existe déjà. Utilisez une autre valeur puis réessayez.';
  }
  if (lower.includes('validation')) {
    return 'Certaines informations doivent être corrigées avant de continuer.';
  }
  if (lower.includes('minio')) {
    return 'Le stockage documentaire est momentanément indisponible. Merci de réessayer.';
  }

  return value
    .replace(/[_-]+/g, ' ')
    .replace(/\s+/g, ' ')
    .replace(/^./, (char) => char.toUpperCase());
}

export function formatDocumentationUxMessage(
  error: unknown,
  fallback?: string,
): string {
  if (error instanceof HttpErrorResponse) {
    const payload = error.error as { message?: unknown; detail?: unknown; title?: unknown } | null;
    const candidates = [payload?.message, payload?.detail, payload?.title, error.message];

    for (const candidate of candidates) {
      if (typeof candidate === 'string' && candidate.trim()) {
        return normalizeBackendMessage(candidate);
      }
    }

    return fallback?.trim() || friendlyFallbackForStatus(error.status);
  }

  if (error instanceof Error && error.message.trim()) {
    return normalizeBackendMessage(error.message);
  }

  return fallback?.trim() || 'Une erreur inattendue est survenue. Merci de réessayer.';
}
