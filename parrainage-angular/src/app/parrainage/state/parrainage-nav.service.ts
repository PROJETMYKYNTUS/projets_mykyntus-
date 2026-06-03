import { Injectable, inject, signal } from '@angular/core';
import type { ParrainageRole } from '../models/referral.model';
import { ParrainageRoleService } from './parrainage-role.service';

export type ParrainageView =
  | 'pilote-dashboard'
  | 'pilote-submit'
  | 'pilote-referrals'
  | 'pilote-bonus'
  | 'rh-dashboard'
  | 'rh-management'
  | 'rh-details'
  | 'rh-rules'
  | 'rh-history'
  | 'compta-payments'
  | 'admin-dashboard'
  | 'admin-tools'
  | 'admin-workflow'
  | 'admin-config'
  | 'admin-payments'
  | 'admin-audit'
  | 'pm-dashboard'
  | 'pm-team'
  | 'pm-referrals'
  | 'pm-performance'
  | 'notifications'
  | 'settings';

const defaultViewByRole: Record<ParrainageRole, ParrainageView> = {
  PILOTE: 'pilote-dashboard',
  RH: 'rh-dashboard',
  ADMIN: 'admin-dashboard',
  MANAGER: 'pm-dashboard',
  COACH: 'pm-dashboard',
  RP: 'pm-dashboard',
  AUDIT: 'admin-audit',
  COMPTA: 'compta-payments',
};

@Injectable({ providedIn: 'root' })
export class ParrainageNavService {
  private readonly roleService = inject(ParrainageRoleService);

  readonly currentView = signal<ParrainageView>('pilote-dashboard');
  readonly selectedReferralId = signal<string | null>(null);

  setView(view: ParrainageView): void {
    this.currentView.set(view);
    if (view !== 'rh-details') this.selectedReferralId.set(null);
  }

  openReferralDetails(id: string): void {
    this.selectedReferralId.set(id);
    this.currentView.set('rh-details');
  }

  resetForRole(role: ParrainageRole): void {
    this.currentView.set(defaultViewByRole[role]);
    this.selectedReferralId.set(null);
  }

  onRoleChanged(): void {
    const role = this.roleService.user().role;
    this.resetForRole(role);
  }
}
