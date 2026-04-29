/*export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api'  // ← :5220 remplacé par :5000 (Gateway)
};*/
export const environment = {
  production: true,
  // API principale du frontend unifié.
  apiUrl: '/api',
  // API utilisée par le module Documentation intégré.
  apiBaseUrl: '',
  authLoginUrl: 'http://localhost:4201/login',
  documentationRealtimeEnabled: true,
  documentationRealtimeHubUrl: '/hubs/documentation',
  documentationDevToolsEnabled: true,
  documentationUserContextHeaders: {
    'X-User-Id': '11111111-1111-4111-8111-111111111101',
    'X-User-Role': 'pilote',
    'X-Tenant-Id': 'atlas-tech-demo',
  } as Record<string, string>,
};