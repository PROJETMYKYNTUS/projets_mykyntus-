/** Profondeur organisationnelle requise à l'import employés (sans synonyme Manager → Superviseur). */
export type EmployeeImportOrgDepth = 'none' | 'pole' | 'cellule' | 'service';

function normalizeToken(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[_\s-]+/g, '');
}

export function isImportForbiddenRole(roleName: string | null | undefined): boolean {
  if (!roleName?.trim()) return false;
  const n = normalizeToken(roleName);
  return n === 'admin' || n === 'manager';
}

export function employeeImportOrgDepth(roleName: string | null | undefined): EmployeeImportOrgDepth {
  if (!roleName?.trim() || isImportForbiddenRole(roleName)) return 'none';
  const n = normalizeToken(roleName);
  if (n === 'rp' || n === 'chefdeprojet' || roleName === 'Chef de projet') return 'pole';
  if (n === 'superviseur' || roleName === 'Superviseur') return 'cellule';
  if (
    n === 'pilote' ||
    n === 'employee' ||
    n === 'employe' ||
    n === 'coach' ||
    n === 'referentechnique' ||
    roleName === 'Référent technique' ||
    roleName === 'Pilote'
  ) {
    return 'service';
  }
  if (n === 'rh' || n === 'audit') return 'none';
  return 'none';
}

export function requiredOrgColumnsMessage(depth: EmployeeImportOrgDepth): string {
  switch (depth) {
    case 'pole':
      return 'Chef de projet : le Pôle est requis.';
    case 'cellule':
      return 'Superviseur : Pôle et Cellule sont requis.';
    case 'service':
      return 'Pilote / Référent technique : Pôle, Cellule et Service sont requis.';
    default:
      return '';
  }
}
