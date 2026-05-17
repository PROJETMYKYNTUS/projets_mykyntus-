import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import type { Employee, Role } from './prime/models';
import { toPrimeRoleHeader } from './prime/lib/prime-role-header';
import { RoleService } from './prime/state/role.service';

function employeeMatchesUiRole(employee: Employee, uiRole: Role): boolean {
  if (employee.role === uiRole) return true;
  if (uiRole === 'Référent technique' && employee.role === 'Coach') return true;
  if (uiRole === 'Coach' && employee.role === 'Référent technique') return true;
  if (uiRole === 'RP' && employee.role === 'Chef de projet') return true;
  if (uiRole === 'Chef de projet' && employee.role === 'RP') return true;
  if (uiRole === 'Comptabilité' && employee.role === 'Comptable') return true;
  if (uiRole === 'Comptable' && employee.role === 'Comptabilité') return true;
  return false;
}

/** Envoie X-Prime-User-Id / X-Prime-Role (ASCII) pour résolution serveur (extension JWT prévue). */
export const primeIdentityInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/prime')) return next(req);
  const roles = inject(RoleService);
  const u = roles.currentUser();
  const r = roles.currentRole();
  const headers: Record<string, string> = {};
  if (r) headers['X-Prime-Role'] = toPrimeRoleHeader(r);
  if (u?.id && employeeMatchesUiRole(u, r)) headers['X-Prime-User-Id'] = u.id;
  return next(req.clone({ setHeaders: headers }));
};
