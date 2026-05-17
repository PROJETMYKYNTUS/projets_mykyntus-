import { Injectable, computed, signal } from '@angular/core';
import type { Employee, Role } from '../models';
import { primeApiGet } from '../services/prime-http';

/** Utilisé uniquement si la liste employés est vide ou sans correspondance de rôle (ex. chargement initial). */
const fallbackUser: Employee = {
  id: 'e-admin',
  firstName: 'Système',
  lastName: 'Admin',
  role: 'Admin',
  serviceId: 'c1',
  poleId: 'd1',
  celluleId: 'p1',
  email: 'admin@local',
};

@Injectable({ providedIn: 'root' })
export class RoleService {
  readonly currentRole = signal<Role>('Superviseur');
  readonly employees = signal<Employee[]>([]);

  readonly currentUser = computed<Employee>(() => {
    const role = this.currentRole();
    const list = this.employees();
    const exactMatch = list.find((employee) => employee.role === role);
    if (exactMatch) return exactMatch;
    if (role === 'Référent technique') {
      const coach = list.find((employee) => employee.role === 'Coach');
      if (coach) return coach;
    }
    if (role === 'Coach') {
      const referent = list.find((employee) => employee.role === 'Référent technique');
      if (referent) return referent;
    }
    if (role === 'Chef de projet') {
      const legacyRp = list.find((employee) => employee.role === 'RP');
      if (legacyRp) return legacyRp;
    }
    if (role === 'RP') {
      const chef = list.find((employee) => employee.role === 'Chef de projet');
      if (chef) return chef;
    }
    return list[0] ?? fallbackUser;
  });

  constructor() {
    void primeApiGet<Employee[]>('/api/prime/employees').then((rows) => this.employees.set(rows));
  }

  setRole(role: Role): void {
    this.currentRole.set(role);
  }
}
