import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { employeeMatchesUiRole } from '../lib/prime-employee-utils';
import { toPrimeRoleHeader } from '../lib/prime-role-header';
import { RoleService } from '../state/role.service';

/** Envoie X-Prime-User-Id / X-Prime-Role pour résolution serveur via le gateway. */
export const primeIdentityInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/prime') && !req.url.includes('/api/rp')) return next(req);
  const roles = inject(RoleService);
  const u = roles.currentUser();
  const r = roles.currentRole();
  const headers: Record<string, string> = {};
  if (r) headers['X-Prime-Role'] = toPrimeRoleHeader(r);
  if (u?.id && employeeMatchesUiRole(u, r)) headers['X-Prime-User-Id'] = u.id;
  return next(req.clone({ setHeaders: headers }));
};
