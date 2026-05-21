/** Active le repli mock pour la démo / soutenance (désactiver : localStorage `prime.demoMockData` = `false`). */
const STORAGE_KEY = 'prime.demoMockData';

export function isPrimeDemoMockEnabled(): boolean {
  if (typeof localStorage === 'undefined') return true;
  const v = localStorage.getItem(STORAGE_KEY);
  if (v === 'false' || v === '0') return false;
  return true;
}

export function setPrimeDemoMockEnabled(enabled: boolean): void {
  if (typeof localStorage === 'undefined') return;
  localStorage.setItem(STORAGE_KEY, enabled ? 'true' : 'false');
}

/** Période courante affichée dans les écrans de démo. */
export const PRIME_DEMO_PERIOD = '2026-05';
