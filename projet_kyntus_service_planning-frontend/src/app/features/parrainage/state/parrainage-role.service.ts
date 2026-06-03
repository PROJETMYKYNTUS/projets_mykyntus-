import { Injectable, inject, signal } from '@angular/core';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { mapJwtRoleToParrainageRole } from '../../../core/session/kyntus-role-ui.config';
import type { ParrainageRole, ParrainageUser } from '../models/referral.model';

const DEMO_ORG = { departmentId: 'dept-1', poleId: 'pole-1', celluleId: 'cell-1' } as const;
const RH_ORG = { departmentId: 'dept-2', poleId: 'pole-rh', celluleId: 'cell-rh' } as const;

const DEMO_USERS: Record<ParrainageRole, ParrainageUser> = {
  PILOTE: { id: 'kyntus-employee', name: 'Employé Démo', email: 'employee@kyntus.ma', role: 'PILOTE', parentId: 'kyntus-coach', ...DEMO_ORG },
  COACH: { id: 'kyntus-coach', name: 'Coach Démo', email: 'coach@kyntus.ma', role: 'COACH', parentId: 'kyntus-manager', ...DEMO_ORG },
  MANAGER: { id: 'kyntus-manager', name: 'Manager Démo', email: 'manager@kyntus.ma', role: 'MANAGER', parentId: 'kyntus-rp', projectId: 'proj-1', ...DEMO_ORG },
  RP: { id: 'kyntus-rp', name: 'Rp Démo', email: 'rp@kyntus.ma', role: 'RP', ...DEMO_ORG },
  RH: { id: 'kyntus-rh', name: 'Rh Démo', email: 'rh@kyntus.ma', role: 'RH', ...RH_ORG },
  COMPTA: { id: 'compta-1', name: 'Sonia Benali', email: 'compta@mykyntus.com', role: 'COMPTA', ...RH_ORG },
  ADMIN: { id: 'kyntus-admin', name: 'Admin Démo', email: 'admin@kyntus.ma', role: 'ADMIN', ...DEMO_ORG },
  AUDIT: { id: 'kyntus-audit', name: 'Audit Démo', email: 'audit@kyntus.ma', role: 'AUDIT', ...DEMO_ORG },
};

@Injectable({ providedIn: 'root' })
export class ParrainageRoleService {
  private readonly session = inject(KyntusSessionService);
  readonly user = signal<ParrainageUser>(this.resolveUser());

  loginAsRole(role: ParrainageRole): void {
    this.user.set(DEMO_USERS[role]);
  }

  private resolveUser(): ParrainageUser {
    const jwtRole = this.session.getRole();
    const mapped = mapJwtRoleToParrainageRole(jwtRole);
    const base = DEMO_USERS[mapped];
    const storedUser = ParrainageRoleService.readStoredUser();
    const email = this.session.getEmail();
    if (storedUser || email) {
      return {
        ...base,
        ...storedUser,
        role: mapped,
        email: email || storedUser?.email || base.email,
        name: storedUser?.name || base.name,
        id: storedUser?.id || base.id,
      };
    }
    return base;
  }

  private static readStoredUser(): Partial<ParrainageUser> | null {
    try {
      const raw = localStorage.getItem('user');
      if (!raw) return null;
      const parsed = JSON.parse(raw) as { username?: string; email?: string; id?: string };
      if (!parsed?.username && !parsed?.email) return null;
      return {
        id: parsed.id,
        name: parsed.username || 'Utilisateur',
        email: parsed.email || '',
      };
    } catch {
      return null;
    }
  }
}
