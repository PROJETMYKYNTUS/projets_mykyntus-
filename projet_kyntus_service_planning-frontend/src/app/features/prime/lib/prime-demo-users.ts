import type { Employee, Role } from '../models';

export function employeeMatchesUiRole(employee: Employee, uiRole: Role): boolean {
  if (employee.role === uiRole) return true;
  if (uiRole === 'Référent technique' && employee.role === 'Coach') return true;
  if (uiRole === 'Coach' && employee.role === 'Référent technique') return true;
  if (uiRole === 'RP' && employee.role === 'Chef de projet') return true;
  if (uiRole === 'Chef de projet' && employee.role === 'RP') return true;
  if (uiRole === 'Comptabilité' && employee.role === 'Comptable') return true;
  if (uiRole === 'Comptable' && employee.role === 'Comptabilité') return true;
  return false;
}

export function findEmployeeByLoginEmail(list: Employee[], email: string): Employee | undefined {
  const needle = email.trim().toLowerCase();
  if (!needle) return undefined;
  return list.find((e) => (e.email ?? '').trim().toLowerCase() === needle);
}

export function employeesForUiRole(list: Employee[], uiRole: Role): Employee[] {
  return list.filter((e) => employeeMatchesUiRole(e, uiRole));
}

export function pickDefaultEmployeeForRole(list: Employee[], role: Role): Employee | undefined {
  if (list.length === 0) return undefined;
  return list.find((e) => employeeMatchesUiRole(e, role)) ?? list[0];
}

export function resolveEmployeeForRole(
  list: Employee[],
  role: Role,
  preferredUserId: string | null,
  loginEmail?: string | null,
): Employee {
  if (loginEmail) {
    const byEmail = findEmployeeByLoginEmail(list, loginEmail);
    if (byEmail) return byEmail;
  }
  if (preferredUserId) {
    const picked = list.find((e) => e.id === preferredUserId);
    if (picked && employeeMatchesUiRole(picked, role)) return picked;
  }
  return (
    pickDefaultEmployeeForRole(list, role) ?? {
      id: '',
      firstName: '',
      lastName: '',
      role,
      serviceId: '',
      poleId: '',
      celluleId: '',
      email: loginEmail?.trim() || '',
    }
  );
}
