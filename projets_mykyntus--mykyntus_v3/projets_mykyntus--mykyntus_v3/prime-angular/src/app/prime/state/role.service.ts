import { Injectable, computed, signal } from '@angular/core';
import type { Employee, Role } from '../models';
import { PRIME_AUTHORIZED_ROLES } from '../models';
import {
  employeesForUiRole,
  mapEmployeeRoleToUiRole,
  pickEmployeeForRolePreferringCellule,
  resolveEmployeeForRole,
} from '../lib/prime-demo-users';
import { primeApiGet } from '../services/prime-http';

const ROLE_STORAGE_KEY = 'prime.demoRole';
const USER_STORAGE_KEY = 'prime.demoUserId';

@Injectable({ providedIn: 'root' })
export class RoleService {
  readonly currentRole = signal<Role>(RoleService.readStoredRole());
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

  constructor() {
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
    const emp = this.employees().find((e) => e.id === id);
    if (emp) {
      const uiRole = mapEmployeeRoleToUiRole(emp.role);
      if (PRIME_AUTHORIZED_ROLES.includes(uiRole) && this.currentRole() !== uiRole) {
        this.currentRole.set(uiRole);
        try {
          sessionStorage.setItem(ROLE_STORAGE_KEY, uiRole);
        } catch {
          /* ignore */
        }
      }
    }
  }

  /** Rôle par défaut pour l’écran Organisation RH. */
  preferRhForOrgScreen(): void {
    if (this.currentRole() !== 'RH') this.setRole('RH');
  }

  private applyDefaultUserForRole(role: Role): void {
    const list = this.employees();
    if (list.length === 0) return;
    const celluleId = this.currentUser().celluleId;
    const picked = pickEmployeeForRolePreferringCellule(list, role, celluleId);
    if (picked) this.setUserId(picked.id);
  }

  private ensureUserMatchesRole(): void {
    const role = this.currentRole();
    const list = this.employees();
    if (list.length === 0) return;
    const stored = this.selectedUserId();
    const resolved = resolveEmployeeForRole(list, role, stored);
    if (!stored || stored !== resolved.id) this.setUserId(resolved.id);
  }

  private static readStoredRole(): Role {
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
