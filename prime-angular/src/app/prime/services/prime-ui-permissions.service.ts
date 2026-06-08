import { computed, inject, Injectable, signal } from '@angular/core';
import type { Role } from '../models';
import {
  PrimeAdminService,
  type RbacAction,
  type RbacPermissionDto,
  type RbacScope,
} from './prime-admin.service';

type UiPermissionAction = RbacAction | 'Export' | 'Navigate';

const ROLE_SCOPE: Record<string, RbacScope> = {
  Admin: 'Global',
  RH: 'Global',
  Audit: 'Global',
  Manager: 'Pole',
  Comptabilité: 'Global',
  Comptable: 'Global',
  'Chef de projet': 'Pole',
  RP: 'Pole',
  Superviseur: 'Cellule',
  'Référent technique': 'Service',
  Coach: 'Service',
  Pilote: 'Self',
};

const PATH_REQUIREMENTS: Record<string, { action: UiPermissionAction; scope?: RbacScope }> = {
  '/types': { action: 'Configure', scope: 'Global' },
  '/rules': { action: 'Configure', scope: 'Global' },
  '/configuration': { action: 'Configure', scope: 'Global' },
  '/rh/organisation': { action: 'Read', scope: 'Global' },
  '/validation': { action: 'Validate' },
  '/validation-history': { action: 'Read' },
  '/results': { action: 'Read' },
  '/global-pool': { action: 'Read', scope: 'Global' },
  '/synthesis-tracking': { action: 'Read', scope: 'Global' },
  '/team-performance': { action: 'Read' },
  '/superviseur/scope': { action: 'Read', scope: 'Cellule' },
  '/chef-projet/scope': { action: 'Read', scope: 'Pole' },
  '/prime-cellule-indicateurs': { action: 'Read', scope: 'Cellule' },
  '/prime-saisie': { action: 'Edit', scope: 'Cellule' },
  '/prime-fiches-pilotes': { action: 'Edit', scope: 'Cellule' },
  '/prime-saisie-cellule': { action: 'Edit', scope: 'Cellule' },
  '/prime-import-fiche': { action: 'Edit', scope: 'Cellule' },
  '/template-manager': { action: 'Edit', scope: 'Cellule' },
  '/employee/primes': { action: 'Read', scope: 'Self' },
  '/employee/performance': { action: 'Read', scope: 'Self' },
};

const FALLBACK_ALLOW: Record<string, Partial<Record<UiPermissionAction, RbacScope[]>>> = {
  Admin: {
    Read: ['Global'],
    Edit: ['Global', 'Pole', 'Cellule', 'Service', 'Self'],
    Validate: ['Global', 'Pole', 'Cellule', 'Service'],
    Configure: ['Global'],
    Export: ['Global'],
    Navigate: ['Global'],
  },
  RH: { Read: ['Global'], Edit: ['Global'], Validate: ['Global'], Configure: ['Global'], Export: ['Global'] },
  Audit: { Read: ['Global'], Export: ['Global'] },
  Manager: { Read: ['Global', 'Pole'], Export: ['Pole'] },
  Comptabilité: { Read: ['Global'], Export: ['Global'] },
  Comptable: { Read: ['Global'], Export: ['Global'] },
  'Chef de projet': { Read: ['Pole'], Validate: ['Pole'], Export: ['Pole'] },
  RP: { Read: ['Pole'], Validate: ['Pole'], Export: ['Pole'] },
  Superviseur: { Read: ['Cellule'], Edit: ['Cellule'], Validate: ['Cellule'], Export: ['Cellule'] },
  'Référent technique': { Read: ['Service'], Validate: ['Service'], Export: ['Service'] },
  Coach: { Read: ['Service'], Validate: ['Service'], Export: ['Service'] },
  Pilote: { Read: ['Self'], Edit: ['Self'] },
};

@Injectable({ providedIn: 'root' })
export class PrimeUiPermissionsService {
  private readonly admin = inject(PrimeAdminService);
  private readonly rows = signal<RbacPermissionDto[]>([]);
  private readonly loaded = signal(false);

  readonly isLoaded = this.loaded.asReadonly();
  readonly matrix = this.rows.asReadonly();
  readonly coverage = computed(() => {
    const rows = this.rows();
    return {
      totalRules: rows.length,
      allowedRules: rows.filter((r) => r.isAllowed).length,
      deniedRules: rows.filter((r) => !r.isAllowed).length,
    };
  });

  ensureLoaded(): void {
    if (this.loaded()) return;
    this.admin.listRbac().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.loaded.set(true);
      },
      error: () => {
        this.rows.set([]);
        this.loaded.set(true);
      },
    });
  }

  applyPermission(updated: RbacPermissionDto): void {
    this.rows.update((rows) => {
      const idx = rows.findIndex((r) => r.id === updated.id);
      if (idx >= 0) {
        const copy = [...rows];
        copy[idx] = updated;
        return copy;
      }
      return [...rows, updated];
    });
    this.loaded.set(true);
  }

  primaryScopeForRole(role: Role | string): RbacScope {
    return ROLE_SCOPE[role] ?? 'Self';
  }

  can(role: Role | string, action: UiPermissionAction, scope = this.primaryScopeForRole(role)): boolean {
    this.ensureLoaded();
    const rows = this.rows();
    const rbacAction = action === 'Export' || action === 'Navigate' ? 'Read' : action;
    const exact = rows.find((r) => r.role === role && r.action === rbacAction && r.scope === scope);
    if (exact) return exact.isAllowed;
    const global = rows.find((r) => r.role === role && r.action === rbacAction && r.scope === 'Global');
    if (global?.isAllowed) return true;
    return FALLBACK_ALLOW[role]?.[action]?.includes(scope as RbacScope) === true
      || FALLBACK_ALLOW[role]?.[rbacAction as UiPermissionAction]?.includes(scope as RbacScope) === true;
  }

  canViewPath(role: Role, path: string): boolean {
    const req = PATH_REQUIREMENTS[path];
    if (!req) return this.can(role, 'Read');
    return this.can(role, req.action, req.scope ?? this.primaryScopeForRole(role));
  }

  actionLabel(action: UiPermissionAction): string {
    if (action === 'Read') return 'Consulter';
    if (action === 'Edit') return 'Modifier';
    if (action === 'Validate') return 'Valider / rejeter';
    if (action === 'Configure') return 'Configurer';
    if (action === 'Export') return 'Exporter';
    return 'Naviguer';
  }
}
