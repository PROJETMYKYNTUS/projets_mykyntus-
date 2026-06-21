import type { DepartmentContextService } from '../services/allowance-api.service';
import type { PrimeNavRequestService } from '../services/prime-nav-request.service';
import { resolveManagerHomePath } from './prime-nav-access';
import { buildPrimeDepartmentManagerNav } from './prime-manager-nav';
import { resolveManagerPrimeTrack } from './prime-manager-track';
import type { Role } from '../models';

/** Redirige un manager opérationnel hors des écrans Primes Support. */
export function redirectManagerFromAllowancesIfNeeded(
  role: Role | string,
  dept: DepartmentContextService,
  nav: PrimeNavRequestService,
): boolean {
  if (!dept.loaded()) return false;
  const managerNav = buildPrimeDepartmentManagerNav(dept);
  const track = resolveManagerPrimeTrack(role, managerNav);
  if (track !== 'operational') return false;
  nav.requestView(resolveManagerHomePath('Manager', managerNav));
  return true;
}

/** Redirige un manager Support vers le track Allowances s'il ouvre un écran opérationnel. */
export function redirectSupportManagerToAllowancesIfNeeded(
  role: Role | string,
  dept: DepartmentContextService,
  nav: PrimeNavRequestService,
  currentPath: string,
): boolean {
  if (!dept.loaded()) return false;
  const managerNav = buildPrimeDepartmentManagerNav(dept);
  if (resolveManagerPrimeTrack(role, managerNav) !== 'support') return false;
  if (currentPath.startsWith('/allowances')) return false;
  nav.requestView('/allowances');
  return true;
}
