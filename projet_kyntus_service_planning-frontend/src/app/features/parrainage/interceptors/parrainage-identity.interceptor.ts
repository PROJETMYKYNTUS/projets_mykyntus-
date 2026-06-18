import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ParrainageRoleService } from '../state/parrainage-role.service';

/** Envoie X-Parrainage-* pour le gateway / backend parrainage. */
export const parrainageIdentityInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/parrainage')) return next(req);
  const roleSvc = inject(ParrainageRoleService);
  const u = roleSvc.user();
  const headers: Record<string, string> = {
    'X-Parrainage-Role': String(u.role),
    'X-Parrainage-User-Id': String(u.id),
  };
  if (u.projectId) headers['X-Parrainage-Project-Id'] = String(u.projectId);
  return next(req.clone({ setHeaders: headers }));
};
