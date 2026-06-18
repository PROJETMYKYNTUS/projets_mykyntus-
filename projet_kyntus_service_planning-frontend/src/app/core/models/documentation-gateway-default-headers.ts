import { DocumentationHeaders } from './documentation-headers';

/**
 * Valeurs de repli pour l’API Gateway en local / Docker lorsque les en-têtes ne sont pas posés en amont.
 * L’API documentation exige un UUID pour X-User-Id (pas une adresse e-mail) et X-User-Role.
 * UUID = utilisatrice démo « Yasmine » (profil pilote), aligné sur environment.documentationUserContextHeaders et l’annuaire seed.
 */
export const DocumentationGatewayDefaultHeaders = {
  [DocumentationHeaders.tenantId]: 'atlas-tech-demo',
  [DocumentationHeaders.userId]: '11111111-1111-4111-8111-111111111101',
  [DocumentationHeaders.userRole]: 'pilote',
} as const;
