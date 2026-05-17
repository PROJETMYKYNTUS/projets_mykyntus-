import { Injectable, signal } from '@angular/core';

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
}
