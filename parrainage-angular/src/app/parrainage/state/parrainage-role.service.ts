import { Injectable, signal } from '@angular/core';
import type { ParrainageRole, ParrainageUser } from '../models/referral.model';

const DEMO_ORG = { departmentId: 'dept-1', poleId: 'pole-1', celluleId: 'cell-1' } as const;
const RH_ORG = { departmentId: 'dept-2', poleId: 'pole-rh', celluleId: 'cell-rh' } as const;

const DEMO_USERS: Record<ParrainageRole, ParrainageUser> = {
  PILOTE: { id: 'emp-1', name: 'Jean Dupont', email: 'jean.dupont@mykyntus.com', role: 'PILOTE', parentId: 'coach-1', ...DEMO_ORG },
  COACH: { id: 'coach-1', name: 'Marc Lefèvre', email: 'coach@mykyntus.com', role: 'COACH', parentId: 'mgr-1', ...DEMO_ORG },
  MANAGER: { id: 'mgr-1', name: 'Charlie Durand', email: 'manager@mykyntus.com', role: 'MANAGER', parentId: 'rp-1', projectId: 'proj-1', ...DEMO_ORG },
  RP: { id: 'rp-1', name: 'Rachid El Amrani', email: 'rp@mykyntus.com', role: 'RP', ...DEMO_ORG },
  RH: { id: 'rh-1', name: 'Camille Rousseau', email: 'rh@mykyntus.com', role: 'RH', ...RH_ORG },
  COMPTA: { id: 'compta-1', name: 'Sonia Benali', email: 'compta@mykyntus.com', role: 'COMPTA', ...RH_ORG },
  ADMIN: { id: 'admin-1', name: 'Administrateur démo', email: 'admin@mykyntus.com', role: 'ADMIN', ...DEMO_ORG },
  AUDIT: { id: 'audit-1', name: 'Auditeur', email: 'audit@mykyntus.com', role: 'AUDIT', ...DEMO_ORG },
};

@Injectable({ providedIn: 'root' })
export class ParrainageRoleService {
  readonly user = signal<ParrainageUser>(DEMO_USERS.PILOTE);

  loginAsRole(role: ParrainageRole): void {
    this.user.set(DEMO_USERS[role]);
  }
}
