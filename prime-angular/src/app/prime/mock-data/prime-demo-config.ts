/**
 * Repli mock pour soutenance locale uniquement.
 * Par défaut : désactivé (données PostgreSQL / API réelles).
 * Activer : `localStorage.setItem('prime.demoMockData', 'true')` puis recharger la page.
 */
const STORAGE_KEY = 'prime.demoMockData';

export function isPrimeDemoMockEnabled(): boolean {
  if (typeof localStorage === 'undefined') return false;
  const v = localStorage.getItem(STORAGE_KEY);
  return v === 'true' || v === '1';
}

export function setPrimeDemoMockEnabled(enabled: boolean): void {
  if (typeof localStorage === 'undefined') return;
  localStorage.setItem(STORAGE_KEY, enabled ? 'true' : 'false');
}

/** Période courante affichée dans les écrans de démo. */
export const PRIME_DEMO_PERIOD = '2026-05';
