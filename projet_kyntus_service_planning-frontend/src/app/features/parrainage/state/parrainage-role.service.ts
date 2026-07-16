import { Injectable, inject, signal } from '@angular/core';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { mapJwtRoleToParrainageRole } from '../../../core/session/kyntus-role-ui.config';
import type { ParrainageRole, ParrainageUser } from '../models/referral.model';

/** Orga contact centre (Prime d1 / p1) — miroir init/contactcentre/roster.json */
const CC_ORG = { departmentId: 'd1', poleId: 'd1', celluleId: 'p1' } as const;
const RH_ORG = { departmentId: 'd2', poleId: 'd2', celluleId: 'p3' } as const;

/** IDs = Auth SubjectId (alignés seeds Parrainage / Planning / Formation). */
const DEMO_USERS: Record<ParrainageRole, ParrainageUser> = {
  PILOTE: {
    id: '11111111-1111-4111-8111-111111111103',
    name: 'Yasmine El Idrissi',
    email: 'employee@kyntus.ma',
    role: 'PILOTE',
    parentId: '11111111-1111-4111-8111-111111111106',
    ...CC_ORG,
  },
  COACH: {
    id: '11111111-1111-4111-8111-111111111106',
    name: 'Omar Tazi',
    email: 'coach@kyntus.ma',
    role: 'COACH',
    parentId: '11111111-1111-4111-8111-111111111105',
    ...CC_ORG,
  },
  MANAGER: {
    id: '11111111-1111-4111-8111-111111111105',
    name: 'Nadia Benchrif',
    email: 'manager@kyntus.ma',
    role: 'MANAGER',
    parentId: '11111111-1111-4111-8111-111111111107',
    projectId: 'proj-inbound',
    ...CC_ORG,
  },
  RP: {
    id: '11111111-1111-4111-8111-111111111107',
    name: 'Ghita Benkirane',
    email: 'rp@kyntus.ma',
    role: 'RP',
    ...CC_ORG,
  },
  RH: {
    id: '11111111-1111-4111-8111-111111111104',
    name: 'Latifa Mansouri',
    email: 'rh@kyntus.ma',
    role: 'RH',
    ...RH_ORG,
  },
  COMPTA: {
    id: '22222222-2222-4222-8222-222222222011',
    name: 'Karim Oufkir',
    email: 'karim.oufkir@contactcentre.ma',
    role: 'COMPTA',
    ...RH_ORG,
  },
  ADMIN: {
    id: '11111111-1111-4111-8111-111111111108',
    name: 'Système Admin',
    email: 'admin@kyntus.ma',
    role: 'ADMIN',
    ...CC_ORG,
  },
  AUDIT: {
    id: '11111111-1111-4111-8111-111111111109',
    name: 'Laila Zahidi',
    email: 'audit@kyntus.ma',
    role: 'AUDIT',
    ...CC_ORG,
  },
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
        id: this.resolvePortalUserId(base.id, storedUser?.id),
      };
    }
    return base;
  }

  /** En-têtes dev : préfère le sub JWT (GUID portail), sinon évite l'id auth numérique. */
  private resolvePortalUserId(fallbackId: string, storedId?: string): string {
    const subjectId = this.session.getSubjectId();
    if (subjectId) return subjectId;
    if (storedId && !/^\d+$/.test(storedId)) return storedId;
    return fallbackId;
  }

  private static readStoredUser(): Partial<ParrainageUser> | null {
    try {
      const raw = localStorage.getItem('user');
      if (!raw) return null;
      const parsed = JSON.parse(raw) as { username?: string; email?: string; id?: string | number };
      if (!parsed?.username && !parsed?.email) return null;
      return {
        id: parsed.id != null && parsed.id !== '' ? String(parsed.id) : undefined,
        name: parsed.username || 'Utilisateur',
        email: parsed.email || '',
      };
    } catch {
      return null;
    }
  }
}
