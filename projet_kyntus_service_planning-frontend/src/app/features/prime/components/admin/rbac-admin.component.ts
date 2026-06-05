import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { PrimeCardComponent } from '../prime-card.component';
import {
  PrimeAdminService,
  type RbacCatalogDto,
  type RbacPermissionDto,
  type UpsertRbacPermissionRequest,
} from '../../services/prime-admin.service';
import { RoleService } from '../../state/role.service';
import { PrimeUiPermissionsService } from '../../services/prime-ui-permissions.service';

const DEFAULT_ACTIONS = ['Read', 'Edit', 'Validate', 'Configure'] as const;
const DEFAULT_SCOPES = ['Global', 'Pole', 'Cellule', 'Service', 'Self'] as const;

@Component({
  selector: 'app-rbac-admin',
  standalone: true,
  imports: [PrimeCardComponent],
  template: `
    <app-prime-card title="Gestion des accès (RBAC)">
      @if (loading()) {
        <div class="py-12 flex justify-center">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-cyan-500"></div>
        </div>
      } @else if (error()) {
        <p class="text-rose-400 text-sm py-4">{{ error() }}</p>
      } @else {
        <p class="text-muted text-sm mb-4">
          Choisissez un rôle puis modifiez la grille <span class="text-primary">Action × Périmètre</span>. Les
          changements sont enregistrés et appliqués aux écrans du module PRIME.
        </p>

        <label class="text-primary text-sm flex flex-wrap items-center gap-3 mb-4">
          Rôle
          <select
            class="rounded-lg border border-default bg-input px-3 py-2 text-primary text-sm min-w-[12rem]"
            [value]="selectedRole()"
            (change)="onRoleChange($any($event.target).value)"
          >
            @for (r of roleOptions(); track r) {
              <option [value]="r">{{ r }}</option>
            }
          </select>
        </label>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-3 mb-4">
          <div class="rounded-xl border border-default bg-input p-4">
            <p class="text-[11px] uppercase tracking-wider text-muted">Couverture RBAC</p>
            <p class="mt-1 text-2xl font-bold text-primary">{{ permissions.coverage().allowedRules }}/{{ permissions.coverage().totalRules }}</p>
            <p class="mt-1 text-xs text-muted">Règles autorisées chargées côté interface.</p>
          </div>
          <div class="rounded-xl border border-default bg-input p-4">
            <p class="text-[11px] uppercase tracking-wider text-muted">Périmètre principal</p>
            <p class="mt-1 text-2xl font-bold text-primary">{{ permissions.primaryScopeForRole(selectedRole()) }}</p>
            <p class="mt-1 text-xs text-muted">Utilisé par menus, pages et actions rapides.</p>
          </div>
          <div class="rounded-xl border border-default bg-input p-4">
            <p class="text-[11px] uppercase tracking-wider text-muted">Simulation utilisateur</p>
            <select
              class="mt-2 w-full rounded-lg border border-default bg-card px-3 py-2 text-primary text-sm"
              [value]="simulatedUserId()"
              (change)="simulatedUserId.set($any($event.target).value)"
            >
              <option value="">Rôle uniquement</option>
              @for (user of simulatedUsers(); track user.id) {
                <option [value]="user.id">{{ user.firstName }} {{ user.lastName }}</option>
              }
            </select>
          </div>
        </div>

        <div class="rounded-xl border border-default bg-input p-4 mb-4">
          <div class="flex flex-wrap items-center justify-between gap-3 mb-3">
            <div>
              <p class="text-sm font-semibold text-primary">Prévisualisation des actions</p>
              <p class="text-xs text-muted">Ce résumé vérifie les actions visibles avant d'entrer dans un écran métier.</p>
            </div>
            @if (simulatedUserId()) {
              <span class="text-xs px-2 py-1 rounded-full bg-card border border-default text-primary">Utilisateur: {{ simulatedUserId() }}</span>
            }
          </div>
          <div class="grid grid-cols-2 md:grid-cols-5 gap-2">
            @for (item of accessPreview(); track item.label) {
              <div
                class="rounded-lg border px-3 py-2 text-xs"
                [class]="item.allowed ? 'border-emerald-500/30 bg-emerald-500/10 text-primary' : 'border-rose-500/30 bg-rose-500/10 text-primary'"
              >
                <span class="block font-semibold">{{ item.allowed ? 'Autorisé' : 'Bloqué' }}</span>
                <span class="text-muted">{{ item.label }}</span>
              </div>
            }
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-sm border border-default rounded-lg overflow-hidden">
            <thead>
              <tr class="bg-input">
                <th class="text-left py-2 px-3 text-muted border-b border-default">Action \\ Périmètre</th>
                @for (sc of scopes(); track sc) {
                  <th class="text-center py-2 px-2 text-muted border-b border-default text-xs">{{ sc }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (ac of actions(); track ac) {
                <tr class="border-b border-default">
                  <td class="py-2 px-3 text-primary font-medium">{{ ac }}</td>
                  @for (sc of scopes(); track sc) {
                    <td class="py-2 px-2 text-center">
                      <button
                        type="button"
                        [disabled]="busyKey() === cellKey(ac, sc)"
                        (click)="toggleCell(ac, sc)"
                        class="px-2 py-1 rounded text-xs transition-colors min-w-[3rem] font-semibold"
                        [class]="isAllowed(ac, sc) ? 'bg-emerald-500/20 text-primary' : 'bg-rose-500/20 text-primary'"
                      >
                        {{ isAllowed(ac, sc) ? 'Oui' : 'Non' }}
                      </button>
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </app-prime-card>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RbacAdminComponent implements OnInit {
  private readonly admin = inject(PrimeAdminService);
  private readonly roleService = inject(RoleService);
  readonly permissions = inject(PrimeUiPermissionsService);

  readonly rbacCatalog = signal<RbacCatalogDto | null>(null);
  readonly actions = signal<string[]>([...DEFAULT_ACTIONS]);
  readonly scopes = signal<string[]>([...DEFAULT_SCOPES]);

  readonly rows = signal<RbacPermissionDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly busyKey = signal<string | null>(null);
  readonly selectedRole = signal<string>('Superviseur');
  readonly simulatedUserId = signal('');

  readonly roleOptions = computed(() => {
    const catRoles = this.rbacCatalog()?.roles ?? [];
    const fromRows = [...new Set(this.rows().map((r) => r.role))];
    return [...new Set([...catRoles, ...fromRows])].sort((a, b) => a.localeCompare(b, 'fr'));
  });

  readonly simulatedUsers = computed(() =>
    this.roleService.employees().filter((e) => e.role === this.selectedRole()),
  );

  readonly accessPreview = computed(() => {
    const role = this.selectedRole();
    const scope = this.permissions.primaryScopeForRole(role);
    return [
      { label: 'Voir résultats', allowed: this.permissions.can(role, 'Read', scope) },
      { label: 'Valider / rejeter', allowed: this.permissions.can(role, 'Validate', scope) },
      { label: 'Exporter', allowed: this.permissions.can(role, 'Export', scope) },
      { label: 'Configurer', allowed: this.permissions.can(role, 'Configure', 'Global') },
      { label: 'Accéder synthèse globale', allowed: this.permissions.can(role, 'Read', 'Global') },
    ];
  });

  ngOnInit(): void {
    this.reload();
  }

  onRoleChange(role: string): void {
    this.selectedRole.set(role);
  }

  cellKey(action: string, scope: string): string {
    return `${this.selectedRole()}|${action}|${scope}`;
  }

  isAllowed(action: string, scope: string): boolean {
    const row = this.rows().find(
      (r) => r.role === this.selectedRole() && r.action === action && r.scope === scope,
    );
    return row?.isAllowed ?? false;
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      list: this.admin.listRbac(),
      cat: this.admin.rbacCatalog(),
    }).subscribe({
      next: ({ list, cat }) => {
        this.rows.set(list);
        this.rbacCatalog.set(cat);
        this.actions.set(cat.actions?.length ? cat.actions : [...DEFAULT_ACTIONS]);
        this.scopes.set(cat.scopes?.length ? cat.scopes : [...DEFAULT_SCOPES]);
        if (!list.some((r) => r.role === this.selectedRole()) && list.length > 0) {
          this.selectedRole.set(list[0].role);
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.error ?? 'Impossible de charger la matrice RBAC.');
        this.rows.set([]);
        this.loading.set(false);
      },
    });
  }

  toggleCell(action: string, scope: string): void {
    const role = this.selectedRole();
    const cur = this.isAllowed(action, scope);
    const body: UpsertRbacPermissionRequest = {
      role,
      action,
      scope,
      isAllowed: !cur,
    };
    this.busyKey.set(this.cellKey(action, scope));
    this.admin.upsertRbac(body).subscribe({
      next: (updated) => {
        this.permissions.applyPermission(updated);
        this.rows.update((list) => {
          const idx = list.findIndex((r) => r.role === role && r.action === action && r.scope === scope);
          if (idx >= 0) {
            const copy = [...list];
            copy[idx] = updated;
            return copy;
          }
          return [...list, updated];
        });
        this.busyKey.set(null);
      },
      error: (err) => {
        this.error.set(err?.error?.error ?? 'Mise à jour impossible.');
        this.busyKey.set(null);
      },
    });
  }
}
