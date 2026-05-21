import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { PrimeCardComponent } from '../prime-card.component';
import {
  PrimeAdminService,
  type RbacCatalogDto,
  type RbacPermissionDto,
  type UpsertRbacPermissionRequest,
} from '../../services/prime-admin.service';

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
        <p class="text-slate-400 text-sm mb-4">
          Choisissez un rôle puis modifiez la grille <span class="text-slate-300">Action × Périmètre</span>. Les
          changements sont enregistrés et appliqués aux écrans du module PRIME.
        </p>

        <label class="text-slate-300 text-sm flex flex-wrap items-center gap-3 mb-4">
          Rôle
          <select
            class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-slate-200 text-sm min-w-[12rem]"
            [value]="selectedRole()"
            (change)="onRoleChange($any($event.target).value)"
          >
            @for (r of roleOptions(); track r) {
              <option [value]="r">{{ r }}</option>
            }
          </select>
        </label>

        <div class="overflow-x-auto">
          <table class="w-full text-sm border border-navy-800 rounded-lg overflow-hidden">
            <thead>
              <tr class="bg-navy-900/80">
                <th class="text-left py-2 px-3 text-slate-400 border-b border-navy-800">Action \\ Périmètre</th>
                @for (sc of scopes(); track sc) {
                  <th class="text-center py-2 px-2 text-slate-400 border-b border-navy-800 text-xs">{{ sc }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (ac of actions(); track ac) {
                <tr class="border-b border-navy-800/80">
                  <td class="py-2 px-3 text-slate-200 font-medium">{{ ac }}</td>
                  @for (sc of scopes(); track sc) {
                    <td class="py-2 px-2 text-center">
                      <button
                        type="button"
                        [disabled]="busyKey() === cellKey(ac, sc)"
                        (click)="toggleCell(ac, sc)"
                        class="px-2 py-1 rounded text-xs transition-colors min-w-[3rem]"
                        [class]="isAllowed(ac, sc) ? 'bg-emerald-500/20 text-emerald-300' : 'bg-rose-500/20 text-rose-300'"
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

  readonly rbacCatalog = signal<RbacCatalogDto | null>(null);
  readonly actions = signal<string[]>([...DEFAULT_ACTIONS]);
  readonly scopes = signal<string[]>([...DEFAULT_SCOPES]);

  readonly rows = signal<RbacPermissionDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly busyKey = signal<string | null>(null);
  readonly selectedRole = signal<string>('Superviseur');

  readonly roleOptions = computed(() => {
    const catRoles = this.rbacCatalog()?.roles ?? [];
    const fromRows = [...new Set(this.rows().map((r) => r.role))];
    return [...new Set([...catRoles, ...fromRows])].sort((a, b) => a.localeCompare(b, 'fr'));
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
