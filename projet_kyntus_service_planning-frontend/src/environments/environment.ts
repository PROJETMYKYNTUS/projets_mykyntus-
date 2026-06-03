import { getRuntimeApiBaseUrl } from './runtime-environment';

/**
 * Trafic métier (planning + documentation) : le navigateur appelle uniquement des URLs relatives
 * `/api/...` et `/hubs/...` sur l’origine du frontend → **nginx du conteneur** → **api-gateway** → backends.
 * Ne jamais exposer au front l’URL directe du DocumentationBackend (ex. port 5230).
 */
export const environment = {
  production: true,
  /** Planning : préfixe REST via nginx → api-gateway */
  apiUrl: '/api',
  /** Documentation : même règle — base vide = chemins relatifs `/api/documentation/...` via la gateway. */
  apiBaseUrl: getRuntimeApiBaseUrl(''),
  /** Outils dev documentation (sélecteur utilisateur) — désactivé si production. */
  documentationDevToolsEnabled: false,
  documentationUserContextHeaders: {} as Record<string, string>,
  /**
   * La documentation est intégrée sous `/documentation` dans cette même SPA (port 8200).
   */
  documentationAppBaseUrl: '',
};
