import { Injectable, computed, inject, signal } from '@angular/core';
import type { Employee, Role } from '../models';
import { PRIME_AUTHORIZED_ROLES } from '../models';
import {
  employeesForUiRole,
  employeeMatchesUiRole,
  pickDefaultEmployeeForRole,
  resolveEmployeeForRole,
} from '../lib/prime-demo-users';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { mapJwtRoleToPrimeRole } from '../../../core/session/kyntus-role-ui.config';
import { findEmployeeByLoginEmail } from '../lib/prime-demo-users';
import { primeApiGet } from '../services/prime-http';

const ROLE_STORAGE_KEY = 'prime.demoRole';
const USER_STORAGE_KEY = 'prime.demoUserId';

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly session = inject(KyntusSessionService);
  readonly currentRole = signal<Role>('Superviseur');
  readonly employees = signal<Employee[]>([]);
  /** Identité démo sélectionnée dans la barre (mode développeur). */
  readonly selectedUserId = signal<string | null>(RoleService.readStoredUserId());

  readonly employeesForCurrentRole = computed(() =>
    employeesForUiRole(this.employees(), this.currentRole()),
  );

  readonly currentUser = computed<Employee>(() => {
    const role = this.currentRole();
    const list = this.employees();
    return resolveEmployeeForRole(list, role, this.selectedUserId());
  });

  private employeesRequested = false;

  constructor() {
    this.currentRole.set(this.readInitialRole());
  }

  /** Charge l’annuaire Prime une seule fois, au premier accès au module (pas au démarrage du shell). */
  ensureEmployeesLoaded(): void {
    if (this.employeesRequested) return;
    this.employeesRequested = true;
    void primeApiGet<Employee[]>('/api/prime/employees').then((rows) => {
      this.employees.set(rows);
      this.ensureUserMatchesRole();
    });
  }

  setRole(role: Role): void {
    this.currentRole.set(role);
    try {
      sessionStorage.setItem(ROLE_STORAGE_KEY, role);
    } catch {
      /* ignore */
    }
    this.applyDefaultUserForRole(role);
  }

  setUserId(userId: string): void {
    const id = userId.trim();
    this.selectedUserId.set(id || null);
    try {
      if (id) sessionStorage.setItem(USER_STORAGE_KEY, id);
      else sessionStorage.removeItem(USER_STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }

  /** Rôle par défaut pour l’écran Organisation RH. */
  preferRhForOrgScreen(): void {
    if (this.currentRole() !== 'RH') this.setRole('RH');
  }

  private applyDefaultUserForRole(role: Role): void {
    const list = this.employees();
    if (list.length === 0) return;
    const picked = pickDefaultEmployeeForRole(list, role);
    if (picked) this.setUserId(picked.id);
  }

  private ensureUserMatchesRole(): void {
    const role = this.currentRole();
    const list = this.employees();
    if (list.length === 0) return;
    const email = this.session.getEmail();
    const byEmail = email ? findEmployeeByLoginEmail(list, email) : undefined;
    if (byEmail) {
      this.setUserId(byEmail.id);
      return;
    }
    const stored = this.selectedUserId();
    const resolved = resolveEmployeeForRole(list, role, stored);
    if (!stored || stored !== resolved.id) this.setUserId(resolved.id);
  }

  private readInitialRole(): Role {
    const fromJwt = mapJwtRoleToPrimeRole(this.session.getRole());
    if (fromJwt && PRIME_AUTHORIZED_ROLES.includes(fromJwt)) return fromJwt;
    try {
      const saved = sessionStorage.getItem(ROLE_STORAGE_KEY) as Role | null;
      if (saved && PRIME_AUTHORIZED_ROLES.includes(saved)) return saved;
    } catch {
      /* ignore */
    }
    return 'Superviseur';
  }

  private static readStoredUserId(): string | null {
    try {
      return sessionStorage.getItem(USER_STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
