/** Niveau d’affectation Organisation RH requis selon le rôle métier. */
export type OrgRoleAssignmentDepth = 'none' | 'pole' | 'cellule' | 'service';

function normalizeRoleToken(name: string): string {
  return name
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[_\s-]+/g, '');
}

export function isChefDeProjetRole(roleName: string): boolean {
  const r = normalizeRoleToken(roleName);
  return r === 'rp' || r === 'chefdeprojet';
}

export function isSuperviseurRole(roleName: string): boolean {
  const r = normalizeRoleToken(roleName);
  return r === 'manager' || r === 'superviseur';
}

export function isReferentTechniqueRole(roleName: string): boolean {
  const r = normalizeRoleToken(roleName);
  return r === 'coach' || r === 'referentechnique';
}

export function isPiloteRole(roleName: string): boolean {
  const r = normalizeRoleToken(roleName);
  return r === 'employee' || r === 'pilote';
}

/** Profils sans périmètre organisationnel obligatoire. */
export function isOrgNeutralRole(roleName: string): boolean {
  const r = normalizeRoleToken(roleName);
  return (
    r === 'rh' ||
    r === 'admin' ||
    r === 'audit' ||
    r === 'equipeformation'
  );
}

export function orgRoleAssignmentDepth(roleName: string): OrgRoleAssignmentDepth {
  if (!roleName.trim() || isOrgNeutralRole(roleName)) return 'none';
  if (isChefDeProjetRole(roleName)) return 'pole';
  if (isSuperviseurRole(roleName)) return 'cellule';
  if (isReferentTechniqueRole(roleName) || isPiloteRole(roleName)) return 'service';
  return 'none';
}

export function orgAssignmentRequiresPole(depth: OrgRoleAssignmentDepth): boolean {
  return depth !== 'none';
}

export function orgAssignmentRequiresCellule(depth: OrgRoleAssignmentDepth): boolean {
  return depth === 'cellule' || depth === 'service';
}

export function orgAssignmentRequiresService(depth: OrgRoleAssignmentDepth): boolean {
  return depth === 'service';
}

export function orgAssignmentIsRequired(depth: OrgRoleAssignmentDepth): boolean {
  return depth !== 'none';
}

export function orgAssignmentHint(roleName: string, depth: OrgRoleAssignmentDepth): string {
  if (depth === 'pole') {
    return 'Chef de projet : sélectionnez le pôle supervisé.';
  }
  if (depth === 'cellule') {
    return 'Superviseur : sélectionnez le pôle puis la cellule supervisée.';
  }
  if (depth === 'service') {
    if (isReferentTechniqueRole(roleName)) {
      return 'Référent technique : choisissez le service encadré.';
    }
    return 'Pilote : sélectionnez le pôle, la cellule et le service d’affectation.';
  }
  return '';
}

/** Appel Prime structure/* après création employé (pas pour pilote — sync RabbitMQ). */
export function needsPrimeStructureAssignment(roleName: string): boolean {
  const depth = orgRoleAssignmentDepth(roleName);
  if (depth === 'none' || isPiloteRole(roleName)) return false;
  return true;
}
