import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Award } from 'lucide';
import { PrimeCardComponent } from '../components/prime-card.component';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import {
  dedupeEmployeesByEmail,
  employeesInSuperviseurCellule,
  resolvePlatformOrgLabels,
  resolveSuperviseurCelluleId,
} from '../../../core/org/platform-org-perimeter';
import { resolveUserGuid } from '../../../core/lib/user-guid.util';
import { UserService } from '../../users/services/user.service';
import type { User } from '../../users/users-module';
import { PrimeOrgApiService } from '../services/prime-org-api.service';
import { PrimeService } from '../services/prime.service';
import { RoleService } from '../state/role.service';

interface ScopeRow {
  id: string;
  fullName: string;
  role: string;
  operationalDepartment: string;
  service: string;
  cellule: string;
  pole: string;
}

@Component({
  selector: 'app-superviseur-scope-page',
  standalone: true,
  imports: [PrimeCardComponent, LucideIconComponent],
  template: `
    @if (loading()) {
      <div class="p-8 flex justify-center">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
      </div>
    } @else {
      <div class="prime-page-shell">
        <div>
          <h1 class="prime-page-title">Périmètre Superviseur</h1>
          <p class="prime-page-subtitle">
            Collaborateurs rattachés à la cellule que vous supervisez (référents techniques, pilotes).
          </p>
        </div>

        <app-prime-card title="Équipe supervisée" className="p-0">
          <div class="overflow-x-auto">
            <table class="prime-table">
              <thead>
                <tr>
                  <th>Collaborateur</th>
                  <th>Rôle</th>
                  <th>Département</th>
                  <th>Pôle</th>
                  <th>Cellule</th>
                  <th>Service</th>
                  <th class="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                @if (rows().length === 0) {
                  <tr>
                    <td colspan="7" class="text-center prime-cell-muted py-8">Aucune donnée.</td>
                  </tr>
                } @else {
                  @for (item of rows(); track item.id) {
                    <tr>
                      <td><span class="prime-cell-strong">{{ item.fullName }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.role }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.operationalDepartment }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.pole }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.cellule }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.service }}</span></td>
                      <td class="text-right">
                        <button
                          type="button"
                          class="scope-level-btn"
                          title="Modifier le niveau contractuel"
                          (click)="openContractLevelModal(item)"
                        >
                          <app-lucide-icon [icon]="icons.level" className="w-3.5 h-3.5" />
                          Niveau
                        </button>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </app-prime-card>
      </div>
    }

    @if (levelModalOpen() && levelModalRow(); as row) {
      <div class="level-modal-backdrop" (click)="closeContractLevelModal()">
        <div class="level-modal" (click)="$event.stopPropagation()">
          <h3>Niveau contractuel</h3>
          <p class="level-modal-sub">{{ row.fullName }}</p>
          <div class="level-modal-choices">
            <button
              type="button"
              class="level-choice"
              [class.active]="levelDraft() === 1"
              (click)="levelDraft.set(1)"
            >
              <strong>Débutant</strong>
              <span>Prise de poste / montée en compétence</span>
            </button>
            <button
              type="button"
              class="level-choice"
              [class.active]="levelDraft() === 2"
              (click)="levelDraft.set(2)"
            >
              <strong>Confirmé</strong>
              <span>Autonome sur les tâches courantes</span>
            </button>
            <button
              type="button"
              class="level-choice"
              [class.active]="levelDraft() === 3"
              (click)="levelDraft.set(3)"
            >
              <strong>Expert</strong>
              <span>Référent métier ou opérationnel</span>
            </button>
          </div>
          @if (levelError()) {
            <p class="level-modal-error">{{ levelError() }}</p>
          }
          <div class="level-modal-actions">
            <button type="button" class="ky-btn-secondary" (click)="closeContractLevelModal()">Annuler</button>
            <button
              type="button"
              class="ky-btn-primary"
              [disabled]="levelSaving() || levelModalPlanningUserId() == null"
              (click)="saveContractLevel()"
            >
              {{ levelSaving() ? 'Enregistrement…' : 'Enregistrer' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .scope-level-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        border-radius: 0.5rem;
        padding: 0.35rem 0.65rem;
        font-size: 0.72rem;
        font-weight: 700;
        background: color-mix(in srgb, var(--warning, #d97706) 12%, var(--bg-card, #fff));
        color: #b45309;
        border: 1px solid color-mix(in srgb, var(--warning, #d97706) 35%, var(--border-color));
        cursor: pointer;
      }

      .scope-level-btn:hover {
        opacity: 0.9;
      }

      .level-modal-backdrop {
        position: fixed;
        inset: 0;
        z-index: 60;
        background: rgba(15, 23, 42, 0.45);
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 1rem;
      }

      .level-modal {
        background: var(--bg-card, #fff);
        border-radius: 12px;
        padding: 1.25rem 1.5rem;
        width: min(420px, 100%);
        box-shadow: 0 20px 40px rgba(0, 0, 0, 0.15);
      }

      .level-modal h3 {
        margin: 0 0 4px;
        font-size: 1.05rem;
      }

      .level-modal-sub {
        margin: 0 0 14px;
        font-size: 0.85rem;
        color: var(--text-muted);
      }

      .level-modal-choices {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .level-choice {
        text-align: left;
        border: 1px solid var(--border-color);
        border-radius: 10px;
        padding: 10px 12px;
        background: var(--bg-input, #f8fafc);
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 2px;
      }

      .level-choice strong {
        font-size: 0.9rem;
      }

      .level-choice span {
        font-size: 0.75rem;
        color: var(--text-muted);
      }

      .level-choice.active {
        border-color: #0f172a;
        background: color-mix(in srgb, #0f172a 6%, #fff);
        box-shadow: inset 0 0 0 1px #0f172a;
      }

      .level-modal-error {
        margin: 10px 0 0;
        color: #b91c1c;
        font-size: 0.82rem;
      }

      .level-modal-actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
        margin-top: 16px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuperviseurScopePageComponent {
  private readonly roleService = inject(RoleService);
  private readonly orgApi = inject(PrimeOrgApiService);
  private readonly userService = inject(UserService);

  readonly icons = { level: Award };

  readonly rows = signal<ScopeRow[]>([]);
  readonly loading = signal(true);

  readonly levelModalOpen = signal(false);
  readonly levelModalRow = signal<ScopeRow | null>(null);
  readonly levelModalPlanningUserId = signal<number | null>(null);
  readonly levelDraft = signal<1 | 2 | 3>(1);
  readonly levelSaving = signal(false);
  readonly levelError = signal('');
  private planningUsersByGuid = new Map<string, User>();

  constructor() {
    effect(() => {
      void this.roleService.currentUser().id;
      this.fetch();
    });
  }

  openContractLevelModal(row: ScopeRow): void {
    this.levelModalRow.set(row);
    this.levelModalPlanningUserId.set(null);
    this.levelDraft.set(1);
    this.levelError.set('');
    this.levelModalOpen.set(true);
    this.levelSaving.set(true);

    const applyUser = (user: User | undefined) => {
      this.levelSaving.set(false);
      if (!user) {
        this.levelError.set('Employé introuvable dans le référentiel planning.');
        return;
      }
      this.levelModalPlanningUserId.set(user.id);
      this.levelDraft.set((user.level === 2 || user.level === 3 ? user.level : 1) as 1 | 2 | 3);
    };

    const guid = row.id?.trim() ?? '';
    const cached = guid ? this.planningUsersByGuid.get(guid) : undefined;
    if (cached) {
      applyUser(cached);
      return;
    }

    this.userService.getAllUsers().subscribe({
      next: (users) => {
        this.planningUsersByGuid = new Map(
          users.map((u) => [resolveUserGuid(u), u] as const).filter(([g]) => !!g),
        );
        applyUser(guid ? this.planningUsersByGuid.get(guid) : undefined);
      },
      error: () => {
        this.levelSaving.set(false);
        this.levelError.set('Impossible de charger le niveau contractuel.');
      },
    });
  }

  closeContractLevelModal(): void {
    this.levelModalOpen.set(false);
    this.levelModalRow.set(null);
    this.levelModalPlanningUserId.set(null);
    this.levelError.set('');
    this.levelSaving.set(false);
  }

  saveContractLevel(): void {
    const planningId = this.levelModalPlanningUserId();
    if (planningId == null) return;
    this.levelSaving.set(true);
    this.levelError.set('');
    this.userService.patchContractualLevel(planningId, this.levelDraft()).subscribe({
      next: (updated) => {
        const guid = resolveUserGuid(updated);
        if (guid) this.planningUsersByGuid.set(guid, updated);
        this.levelSaving.set(false);
        this.closeContractLevelModal();
      },
      error: (err: unknown) => {
        this.levelSaving.set(false);
        const msg =
          err instanceof HttpErrorResponse
            ? (err.error as { message?: string } | null)?.message
            : undefined;
        this.levelError.set(msg ?? 'Échec de la mise à jour du niveau.');
      },
    });
  }

  private fetch(): void {
    this.loading.set(true);
    const current = this.roleService.currentUser();
    void Promise.all([
      PrimeService.getEmployees(),
      firstValueFrom(this.orgApi.loadOverview()),
      PrimeService.getOperationalOrgTree(),
    ]).then(async ([employees, overview, orgTree]) => {
      const useLegacyFallback =
        (orgTree.operationalDepartments?.length ?? 0) === 0 &&
        (orgTree.unassignedPoles?.length ?? 0) === 0;
      const legacyDepartments = useLegacyFallback ? await PrimeService.getDepartments() : [];
      const celluleId = resolveSuperviseurCelluleId(current.id, current, overview ?? null);
      const scopeEmployees = dedupeEmployeesByEmail(
        employeesInSuperviseurCellule(employees, celluleId),
      );

      const mapped: ScopeRow[] = scopeEmployees.map((e) => {
        const labels = resolvePlatformOrgLabels(e, legacyDepartments, overview ?? null);
        return {
          id: e.id,
          fullName: `${e.firstName} ${e.lastName}`,
          role: e.role,
          operationalDepartment: labels.operationalDepartment,
          pole: labels.pole,
          cellule: labels.cellule,
          service: labels.service,
        };
      });

      mapped.sort((a, b) => a.fullName.localeCompare(b.fullName));
      this.rows.set(mapped);
      this.loading.set(false);
    });
  }
}
