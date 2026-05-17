import type { Role } from '../models';

/** Ancien rôle RP : shell menu sectionné (legacy). Chef de projet utilise le menu standard. */
export function isProjectLeadRole(r: Role): boolean {
  return r === 'RP';
}
