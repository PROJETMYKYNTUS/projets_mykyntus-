import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { employeeMatchesUiRole } from './prime/lib/prime-demo-users';
import { toPrimeRoleHeader } from './prime/lib/prime-role-header';
import { RoleService } from './prime/state/role.service';

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
