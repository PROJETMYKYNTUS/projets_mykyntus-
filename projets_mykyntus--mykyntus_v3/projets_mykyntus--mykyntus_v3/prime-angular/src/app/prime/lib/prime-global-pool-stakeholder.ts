import type { Role } from '../models';

/** Rôles qui ne peuvent pas utiliser l’API fiches `/api/prime/validation` (synthèse globale uniquement côté backend). */
export function isPrimeGlobalPoolStakeholderRole(role: Role): boolean {
  return role === 'RH' || role === 'Manager' || role === 'Comptabilité' || role === 'Comptable';
}
