import type { DocumentationRole } from '../../features/documentation/interfaces/documentation-role';
import type { Role as PrimeRole } from '../../features/prime/models';
import type { ParrainageRole } from '../../features/parrainage/models/referral.model';

function normalizeJwtRole(jwtRole: string): string {
  return jwtRole
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '');
}

/** Rôle JWT Auth (claim) → rôles UI des microservices intégrés. */
export function mapJwtRoleToDocumentationRole(jwtRole: string): DocumentationRole {
  const r = normalizeJwtRole(jwtRole);
  const map: Record<string, DocumentationRole> = {
    admin: 'Admin',
    rh: 'RH',
    manager: 'Manager',
    superviseur: 'Manager',
    coach: 'Coach',
    referent_technique: 'Coach',
    'referent technique': 'Coach',
    rp: 'RP',
    chef_de_projet: 'RP',
    'chef de projet': 'RP',
    pilote: 'Pilote',
    employee: 'Pilote',
    audit: 'Audit',
    equipe_formation: 'RH',
    'equipe formation': 'RH',
  };
  return map[r] ?? 'Pilote';
}

export function mapJwtRoleToPrimeRole(jwtRole: string): PrimeRole | null {
  const r = normalizeJwtRole(jwtRole);
  if (!r) return null;
  const map: Record<string, PrimeRole> = {
    admin: 'Admin',
    rh: 'RH',
    manager: 'Manager',
    superviseur: 'Superviseur',
    coach: 'Référent technique',
    referent_technique: 'Référent technique',
    'referent technique': 'Référent technique',
    rp: 'Chef de projet',
    chef_de_projet: 'Chef de projet',
    'chef de projet': 'Chef de projet',
    pilote: 'Pilote',
    employee: 'Pilote',
    audit: 'Audit',
    'equipe formation': 'RH',
    equipe_formation: 'RH',
  };
  return map[r] ?? null;
}

export function mapJwtRoleToParrainageRole(jwtRole: string): ParrainageRole {
  const r = normalizeJwtRole(jwtRole);
  const map: Record<string, ParrainageRole> = {
    admin: 'ADMIN',
    rh: 'RH',
    manager: 'MANAGER',
    superviseur: 'MANAGER',
    coach: 'COACH',
    referent_technique: 'COACH',
    'referent technique': 'COACH',
    rp: 'RP',
    chef_de_projet: 'RP',
    'chef de projet': 'RP',
    pilote: 'PILOTE',
    employee: 'PILOTE',
    audit: 'AUDIT',
    equipe_formation: 'RH',
    'equipe formation': 'RH',
  };
  return map[r] ?? 'PILOTE';
}
