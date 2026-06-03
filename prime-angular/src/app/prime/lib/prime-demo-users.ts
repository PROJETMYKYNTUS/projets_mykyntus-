import type { Employee, Role } from '../models';

/** Profils démo Kyntus Maroc — site Oujda (soutenance / mock data). */
export const PRIME_DEMO_SUPERVISOR = { firstName: 'Nadia', lastName: 'Benjelloun' } as const;
export const PRIME_DEMO_REFERENT_TECHNIQUE = { firstName: 'Youssef', lastName: 'Idrissi' } as const;
/** Profil RT alternatif (import RH) — visible en mode développeur si rôle Référent technique. */
export const PRIME_DEMO_REFERENT_KENZA = { firstName: 'Kenza', lastName: 'Alami' } as const;

function nameEq(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
}

export function employeeMatchesDemoProfile(
  e: Employee,
  profile: { firstName: string; lastName: string },
): boolean {
  return nameEq(e.firstName, profile.firstName) && nameEq(e.lastName, profile.lastName);
}

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

export function employeesForUiRole(list: Employee[], uiRole: Role): Employee[] {
  return list.filter((e) => employeeMatchesUiRole(e, uiRole));
}

export function findDemoSupervisor(list: Employee[]): Employee | undefined {
  return (
    list.find((e) => e.role === 'Superviseur' && employeeMatchesDemoProfile(e, PRIME_DEMO_SUPERVISOR)) ??
    list.find((e) => e.role === 'Superviseur')
  );
}

/** Référent rattaché au superviseur démo (Fantine → Abeline), sinon 1er RT de la liste. */
export function findDemoReferentTechnique(list: Employee[]): Employee | undefined {
  const supervisor = findDemoSupervisor(list);
  const kenzaRt = list.find(
    (e) =>
      employeeMatchesUiRole(e, 'Référent technique') &&
      employeeMatchesDemoProfile(e, PRIME_DEMO_REFERENT_KENZA),
  );
  if (kenzaRt) return kenzaRt;

  const fantine = list.find(
    (e) =>
      employeeMatchesUiRole(e, 'Référent technique') &&
      employeeMatchesDemoProfile(e, PRIME_DEMO_REFERENT_TECHNIQUE) &&
      (!supervisor?.id || e.parentId === supervisor.id),
  );
  if (fantine) return fantine;
  if (supervisor) {
    const underSup = list.find(
      (e) =>
        employeeMatchesUiRole(e, 'Référent technique') &&
        e.parentId === supervisor.id,
    );
    if (underSup) return underSup;
  }
  return list.find((e) => employeeMatchesUiRole(e, 'Référent technique'));
}

export function pickDefaultEmployeeForRole(list: Employee[], role: Role): Employee | undefined {
  if (list.length === 0) return undefined;
  switch (role) {
    case 'Superviseur':
      return findDemoSupervisor(list);
    case 'Référent technique':
    case 'Coach':
      return findDemoReferentTechnique(list);
    case 'Chef de projet':
    case 'RP':
      return list.find((e) => employeeMatchesUiRole(e, 'Chef de projet'));
    case 'Pilote':
      return list.find((e) => e.role === 'Pilote');
    case 'RH':
      return list.find((e) => e.role === 'RH');
    case 'Manager':
      return list.find((e) => e.role === 'Manager');
    case 'Comptabilité':
    case 'Comptable':
      return list.find((e) => employeeMatchesUiRole(e, 'Comptabilité'));
    case 'Audit':
      return list.find((e) => e.role === 'Audit');
    case 'Admin':
      return list.find((e) => e.role === 'Admin');
    default:
      return list.find((e) => employeeMatchesUiRole(e, role)) ?? list[0];
  }
}

export function resolveEmployeeForRole(
  list: Employee[],
  role: Role,
  preferredUserId: string | null,
): Employee {
  if (preferredUserId) {
    const picked = list.find((e) => e.id === preferredUserId);
    if (picked && employeeMatchesUiRole(picked, role)) return picked;
  }
  return pickDefaultEmployeeForRole(list, role) ?? list[0] ?? fallbackDevUser(role);
}

function fallbackDevUser(role: Role): Employee {
  return {
    id: 'e-admin',
    firstName: 'Yassine',
    lastName: 'Touimi',
    role: role === 'Admin' ? 'Admin' : 'Superviseur',
    serviceId: 'svc-crm-core',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    email: 'yassine.touimi@kyntus.ma',
  };
}
