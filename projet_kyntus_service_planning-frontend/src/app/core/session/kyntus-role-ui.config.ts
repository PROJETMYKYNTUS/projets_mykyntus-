import type { DocumentationRole } from '../../features/documentation/interfaces/documentation-role';
import type { Role as PrimeRole } from '../../features/prime/models';
import type { ParrainageRole } from '../../features/parrainage/models/referral.model';

/** Rôle JWT Auth (claim) → rôles UI des microservices intégrés. */
export function mapJwtRoleToDocumentationRole(jwtRole: string): DocumentationRole {
  const r = jwtRole.trim().toLowerCase();
  const map: Record<string, DocumentationRole> = {
    admin: 'Admin',
    rh: 'RH',
    manager: 'Manager',
    coach: 'Coach',
    rp: 'RP',
    pilote: 'Pilote',
    audit: 'Audit',
    employee: 'Pilote',
    equipe_formation: 'RH',
    'equipe formation': 'RH',
    superviseur: 'Manager',
  };
  return map[r] ?? 'Pilote';
}

export function mapJwtRoleToPrimeRole(jwtRole: string): PrimeRole | null {
  const r = jwtRole.trim().toLowerCase();
  if (!r) return null;
  const map: Record<string, PrimeRole> = {
    admin: 'Admin',
    rh: 'RH',
    manager: 'Manager',
    coach: 'Référent technique',
    rp: 'Chef de projet',
    pilote: 'Pilote',
    audit: 'Audit',
    employee: 'Pilote',
    'equipe formation': 'RH',
    equipe_formation: 'RH',
    superviseur: 'Superviseur',
  };
  return map[r] ?? null;
}

export function mapJwtRoleToParrainageRole(jwtRole: string): ParrainageRole {
  const r = jwtRole.trim().toLowerCase();
  const map: Record<string, ParrainageRole> = {
    admin: 'ADMIN',
    rh: 'RH',
    manager: 'MANAGER',
    coach: 'COACH',
    rp: 'RP',
    pilote: 'PILOTE',
    audit: 'AUDIT',
    employee: 'PILOTE',
    superviseur: 'COACH',
  };
  return map[r] ?? 'PILOTE';
}
