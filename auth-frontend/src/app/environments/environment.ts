/*export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api'  // ← :5220 remplacé par :5000 (Gateway)
};*/
export const environment = {
  production: true,
  // ✅ Vide car Nginx proxifie /api/ vers le backend
  apiUrl: '/api'
};