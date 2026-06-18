import { Injectable, signal } from '@angular/core';
import type { Role } from '../models';
import { getRoleHomeTarget } from '../lib/prime-role-home';
import { isProjectLeadRole } from '../lib/projectLeadRole';

export type RpSection = 'dashboard' | 'performance' | 'validation' | 'suivi-projet' | 'notifications' | 'settings';
export type AdminSection =
  | 'dashboard'
  | 'access'
  | 'workflows'
  | 'logs'
  | 'anomalies'
  | 'notifications'
  | 'settings';
export type AuditSection =
  | 'dashboard'
  | 'journal'
  | 'anomalies'
  | 'reporting'
  | 'access-history'
  | 'notifications'
  | 'settings';

@Injectable({ providedIn: 'root' })
export class PrimeSectionService {
  readonly activeRpSection = signal<RpSection>('dashboard');
  readonly activeAdminSection = signal<AdminSection>('dashboard');
  readonly activeAuditSection = signal<AuditSection>('journal');

  setActiveRpSection(s: RpSection): void {
    this.activeRpSection.set(s);
  }

  setActiveAdminSection(s: AdminSection): void {
    this.activeAdminSection.set(s);
  }

  setActiveAuditSection(s: AuditSection): void {
    this.activeAuditSection.set(s);
  }

  /** Réinitialise les sections du shell Admin / RP / Audit pour l’accueil du rôle. */
  resetShellForRole(role: Role): void {
    const home = getRoleHomeTarget(role);
    if (role === 'Admin' && home.adminSection) {
      this.activeAdminSection.set(home.adminSection);
      return;
    }
    if (role === 'Audit' && home.auditSection) {
      this.activeAuditSection.set(home.auditSection);
      return;
    }
    if (isProjectLeadRole(role) && home.rpSection) {
      this.activeRpSection.set(home.rpSection);
    }
  }
}
