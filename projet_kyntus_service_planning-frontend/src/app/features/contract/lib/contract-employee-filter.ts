import type { User } from '../../users/users-module';
import type { UserOrgPerimeterView } from '../../../core/org/user-org-perimeter';

export type EmployeePickerRow = {
  user: User;
  perimeter: UserOrgPerimeterView;
  displayName: string;
  searchText: string;
};

export function buildEmployeePickerRows(
  users: User[],
  perimeterById: Map<number, UserOrgPerimeterView>,
): EmployeePickerRow[] {
  return users
    .map((u) => {
      const perimeter = perimeterById.get(u.id) ?? { operationalDepartment: null, pole: null, cellule: null, service: null };
      const displayName = `${u.lastName} ${u.firstName}`.trim();
      const searchText = [
        displayName,
        u.email,
        u.roleName,
        perimeter.pole,
        perimeter.cellule,
        perimeter.service,
        perimeter.supportDepartmentName,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return { user: u, perimeter, displayName, searchText };
    })
    .sort((a, b) => a.displayName.localeCompare(b.displayName, 'fr'));
}

export type EmployeePickerFilters = {
  search?: string;
  pole?: string;
  cellule?: string;
  service?: string;
};

export function filterEmployeePickerRows(
  rows: EmployeePickerRow[],
  opts: EmployeePickerFilters,
  limit = 40,
): { visible: EmployeePickerRow[]; totalMatches: number } {
  let matched = rows;
  const q = opts.search?.trim().toLowerCase();
  if (q) matched = matched.filter((row) => row.searchText.includes(q));
  if (opts.pole) matched = matched.filter((row) => row.perimeter.pole === opts.pole);
  if (opts.cellule) matched = matched.filter((row) => row.perimeter.cellule === opts.cellule);
  if (opts.service) matched = matched.filter((row) => row.perimeter.service === opts.service);
  return { visible: matched.slice(0, limit), totalMatches: matched.length };
}

export function uniqueOrgValues(
  rows: EmployeePickerRow[],
  key: 'pole' | 'cellule' | 'service' | 'supportDepartmentName',
): string[] {
  const set = new Set<string>();
  for (const row of rows) {
    const v = row.perimeter[key]?.trim();
    if (v) set.add(v);
  }
  return [...set].sort((a, b) => a.localeCompare(b, 'fr'));
}
