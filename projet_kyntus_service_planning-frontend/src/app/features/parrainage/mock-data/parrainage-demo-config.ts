/**
 * Repli mock pour soutenance locale uniquement (calqué Prime).
 * Par défaut : désactivé — données PostgreSQL / API réelles.
 * Activer : `localStorage.setItem('parrainage.demoMockData', 'true')` puis recharger.
 */
const STORAGE_KEY = 'parrainage.demoMockData';

export function isParrainageDemoMockEnabled(): boolean {
  if (typeof localStorage === 'undefined') return false;
  const v = localStorage.getItem(STORAGE_KEY);
  return v === 'true' || v === '1';
}

export function setParrainageDemoMockEnabled(enabled: boolean): void {
  if (typeof localStorage === 'undefined') return;
  localStorage.setItem(STORAGE_KEY, enabled ? 'true' : 'false');
}
