import type { Employee, Role } from '../models';

/** Libellé select / liste : nom + rôle métier. */
export function employeeSelectOptionLabel(e: Employee): string {
  return `${e.firstName} ${e.lastName} — ${e.role}`;
}

/** Rôles exclus des affectations organisationnelles (RH gère l’écran, pas auto-affectation). */
export const ORG_ASSIGNMENT_EXCLUDED_ROLES: readonly Role[] = ['RH', 'Admin', 'Audit'];

export function sortEmployeesForSelect(list: Employee[]): Employee[] {
  return [...list].sort((a, b) => {
    const byRole = a.role.localeCompare(b.role, 'fr');
    if (byRole !== 0) return byRole;
    return `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`, 'fr');
  });
}

/** Tous les employés affectables, étiquetés par rôle (titulaire courant conservé). */
export function employeesForOrgAssignmentSelect(
  all: Employee[],
  selectedUserId?: string | null,
  excludedRoles: readonly Role[] = ORG_ASSIGNMENT_EXCLUDED_ROLES,
): Employee[] {
  const excluded = new Set<Role>(excludedRoles);
  const base = sortEmployeesForSelect(all.filter((e) => !excluded.has(e.role)));
  return employeesForSelect(base, selectedUserId);
}

/** Options de liste incluant le titulaire courant même s'il a un rôle « protégé » (chef de projet, etc.). */
export function employeesForSelect(all: Employee[], selectedUserId?: string | null): Employee[] {
  const base = [...all];
  if (!selectedUserId) return base;
  const holder = all.find((e) => e.id === selectedUserId);
  if (holder && !base.some((e) => e.id === holder.id)) base.unshift(holder);
  return base;
}

/** Valeur sûre pour [value] d'un select : vide si l'id n'est pas dans les options. */
export function selectValueOrEmpty(selectedId: string | null | undefined, optionIds: readonly string[]): string {
  const id = (selectedId ?? '').trim();
  if (!id) return '';
  return optionIds.includes(id) ? id : '';
}
