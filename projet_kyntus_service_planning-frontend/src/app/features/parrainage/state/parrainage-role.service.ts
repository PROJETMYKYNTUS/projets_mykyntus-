import { Injectable, inject, signal } from '@angular/core';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { mapJwtRoleToParrainageRole } from '../../../core/session/kyntus-role-ui.config';
import type { ParrainageRole, ParrainageUser } from '../models/referral.model';

@Injectable({ providedIn: 'root' })
export class ParrainageRoleService {
  private readonly session = inject(KyntusSessionService);
  readonly user = signal<ParrainageUser>(this.resolveUser());

  private resolveUser(): ParrainageUser {
    const jwtRole = this.session.getRole();
    const mapped = mapJwtRoleToParrainageRole(jwtRole);
    const storedUser = ParrainageRoleService.readStoredUser();
    const email = this.session.getEmail();
    const subjectId = this.session.getSubjectId();
    return {
      id: subjectId || storedUser?.id || 'unknown',
      name: storedUser?.name || email?.split('@')[0] || 'Utilisateur',
      email: email || storedUser?.email || '',
      role: mapped,
      ...(storedUser?.projectId ? { projectId: storedUser.projectId } : {}),
      ...(storedUser?.parentId ? { parentId: storedUser.parentId } : {}),
      ...(storedUser?.departmentId ? { departmentId: storedUser.departmentId } : {}),
      ...(storedUser?.poleId ? { poleId: storedUser.poleId } : {}),
      ...(storedUser?.celluleId ? { celluleId: storedUser.celluleId } : {}),
    };
  }

  private static readStoredUser(): Partial<ParrainageUser> | null {
    try {
      const raw = localStorage.getItem('user');
      if (!raw) return null;
      const parsed = JSON.parse(raw) as {
        username?: string;
        email?: string;
        id?: string | number;
        projectId?: string;
        parentId?: string;
        departmentId?: string;
        poleId?: string;
        celluleId?: string;
      };
      if (!parsed?.username && !parsed?.email) return null;
      return {
        id: parsed.id != null && parsed.id !== '' ? String(parsed.id) : undefined,
        name: parsed.username || 'Utilisateur',
        email: parsed.email || '',
        projectId: parsed.projectId,
        parentId: parsed.parentId,
        departmentId: parsed.departmentId,
        poleId: parsed.poleId,
        celluleId: parsed.celluleId,
      };
    } catch {
      return null;
    }
  }
}
