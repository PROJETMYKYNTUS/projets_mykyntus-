import { Injectable, computed, inject, signal } from '@angular/core';
import { KyntusSessionService } from '../session/kyntus-session.service';
import {
  isDualHatRole,
  type WorkspaceHat,
} from './workspace-hat.util';

const STORAGE_PREFIX = 'kyntus.workspace-hat.';

@Injectable({ providedIn: 'root' })
export class WorkspaceHatService {
  private readonly session = inject(KyntusSessionService);

  private readonly hatState = signal<WorkspaceHat>('team');
  private readonly roleState = signal('');

  readonly hat = this.hatState.asReadonly();
  readonly canSwitch = computed(() => isDualHatRole(this.roleState()));
  readonly label = computed(() => (this.hatState() === 'self' ? 'Personnel' : 'Équipe'));

  bindRole(jwtRole: string): void {
    const next = (jwtRole ?? '').trim();
    this.roleState.set(next);
    if (!isDualHatRole(next)) {
      this.hatState.set('team');
      return;
    }
    this.hatState.set(this.readStored(next) ?? 'team');
  }

  setHat(hat: WorkspaceHat): void {
    if (!this.canSwitch()) return;
    this.hatState.set(hat);
    this.persist(hat);
  }

  private storageKey(role: string): string {
    const userId =
      this.session.getSubjectId() ||
      String(this.session.getAuthUserId() || this.session.getEmail() || 'anon');
    return `${STORAGE_PREFIX}${userId}.${canonicalizeKey(role)}`;
  }

  private readStored(role: string): WorkspaceHat | null {
    try {
      const raw = localStorage.getItem(this.storageKey(role));
      if (raw === 'self' || raw === 'team') return raw;
    } catch {
      /* ignore quota / private mode */
    }
    return null;
  }

  private persist(hat: WorkspaceHat): void {
    try {
      localStorage.setItem(this.storageKey(this.roleState()), hat);
    } catch {
      /* ignore */
    }
  }
}

function canonicalizeKey(role: string): string {
  return role.trim().toLowerCase().replace(/\s+/g, '-');
}
