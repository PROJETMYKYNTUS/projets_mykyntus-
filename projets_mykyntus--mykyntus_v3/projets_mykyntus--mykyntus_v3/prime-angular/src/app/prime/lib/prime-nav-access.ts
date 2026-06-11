import type { Role } from '../models';

/** Garde client : chemins non autorisés pour certains rôles (le menu principal reste la source principale). */
export function isPrimePathAllowedForRole(path: string, role: Role): boolean {
  if (
    (path === '/validation' || path === '/validation-history') &&
    (role === 'Manager' || role === 'Comptabilité' || role === 'RH')
  )
    return false;
  if (path === '/global-pool' && !['Admin', 'RH', 'Manager', 'Comptabilité'].includes(role)) return false;
  if (path === '/synthesis-tracking' && !['Admin', 'RH', 'Manager'].includes(role))
    return false;
  if (path === '/rh/organisation' && role !== 'RH') return false;
  return true;
}
