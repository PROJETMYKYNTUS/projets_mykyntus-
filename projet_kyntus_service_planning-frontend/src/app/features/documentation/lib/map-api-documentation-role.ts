import type { DocumentationRole } from '../interfaces/documentation-role';

/** CÃ´tÃ© API les rÃ´les sont en minuscules (enum PostgreSQL / en-tÃªtes). */
export function mapApiRoleToDocumentationRole(api: string): DocumentationRole {
  const key = api.trim().toLowerCase();
  const map: Record<string, DocumentationRole> = {
    pilote: 'Pilote',
    coach: 'Coach',
    manager: 'Manager',
    rp: 'RP',
    rh: 'RH',
    admin: 'Admin',
    audit: 'Audit',
  };
  const role = map[key];
  if (!role) {
    throw new Error(`RÃ´le API inconnu : ${api}`);
  }
  return role;
}
