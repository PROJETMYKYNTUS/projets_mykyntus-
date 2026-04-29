import { DocumentationHeaders } from '../app/core/constants/documentation-headers';
import { getRuntimeApiBaseUrl } from './runtime-environment';

/**
 * Build production : pas d’outils dev.
 * apiBaseUrl = origine (souvent vide) : les services font `${apiBaseUrl}/api/...` → `/api/...`
 * (évite `/api/api/...` derrière Nginx ou la gateway).
 */
export const environment = {
  production: true,
  // En prod/intégration, ne pas forcer un contexte dev local (sinon risque de profil "pilote" persistant).
  // Le backend Documentation + headers gateway doivent piloter le rôle réel (RH/Manager/Pilote...).
  documentationDevToolsEnabled: false,
  apiBaseUrl: getRuntimeApiBaseUrl(''),
  authLoginUrl: 'http://localhost:4201/login',
  // Contexte par défaut en environnement docker local tant que la gateway
  // ne relaie pas explicitement X-User-Id / X-User-Role / X-Tenant-Id.
  documentationUserContextHeaders: {
    [DocumentationHeaders.userId]: '11111111-1111-4111-8111-111111111101',
    [DocumentationHeaders.userRole]: 'pilote',
    [DocumentationHeaders.tenantId]: 'atlas-tech-demo',
  } as Record<string, string>,
};
