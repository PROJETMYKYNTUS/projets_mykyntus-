import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Award, CalendarDays, GraduationCap, History, Search, Tag } from 'lucide';
import { PrimeCardComponent } from '../components/prime-card.component';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { BodyPortalDirective } from '../../../shared/directives/body-portal.directive';
import {
  dedupeEmployeesByEmail,
  employeesInSuperviseurCellule,
  resolvePlatformOrgLabels,
  resolveSuperviseurCelluleIds,
} from '../../../core/org/platform-org-perimeter';
import { resolveUserGuid } from '../../../core/lib/user-guid.util';
import { UserService } from '../../users/services/user.service';
import type { User } from '../../users/users-module';
import {
  PlanningService,
  type AgentHistoryPeriod,
  type AgentPlanningWeek,
  type SaturdayEmployeeMode,
} from '../../planning/services/planning.service';
import { AgentPlanningWeeksComponent } from '../../planning/components/agent-planning-weeks/agent-planning-weeks.component';
import { PrimeOrgApiService, type OrgAssignmentsOverview } from '../services/prime-org-api.service';
import { PrimeService } from '../services/prime.service';
import { RoleService } from '../state/role.service';
import { KyntusSessionService } from '../../../core/session/kyntus-session.service';
import { HttpErrorResponse } from '@angular/common/http';
import type { OperationalDepartmentNode, OrgPoleNode } from '../models/org-tree.types';

interface ScopeRow {
  id: string;
  fullName: string;
  role: string;
  operationalDepartment: string;
  service: string;
  cellule: string;
  pole: string;
  planningUserId: number | null;
  subServiceId: number | null;
  level: number | null;
  saturdayWorkMode: number | null;
  effectiveMode: number | null;
  groupNumber: number;
  isSpecialCase: boolean;
  specialCaseDescription: string | null;
  isPlateauTraining: boolean;
}

interface CelluleOption {
  id: string;
  label: string;
}

type ModeDraft = 'default' | 'every4h' | 'alternate8h';

function celluleStorageKey(userId: string): string {
  return `kyntus.scope.cellule.${userId.trim().toLowerCase()}`;
}

function resolveCelluleLabel(
  celluleId: string,
  overview: OrgAssignmentsOverview | null,
  operationalDepartments: OperationalDepartmentNode[],
  unassignedPoles: OrgPoleNode[],
): string {
  for (const md of operationalDepartments) {
    for (const pole of md.poles ?? []) {
      const cell = (pole.cellules ?? []).find((c) => c.id === celluleId);
      if (cell?.name) return `${pole.name} — ${cell.name}`;
    }
  }
  for (const pole of unassignedPoles) {
    const cell = (pole.cellules ?? []).find((c) => c.id === celluleId);
    if (cell?.name) return `${pole.name} — ${cell.name}`;
  }
  const svc = overview?.services?.find((s) => s.id === celluleId);
  return svc?.name?.trim() || celluleId;
}

@Component({
  selector: 'app-superviseur-scope-page',
  standalone: true,
  imports: [PrimeCardComponent, LucideIconComponent, BodyPortalDirective, AgentPlanningWeeksComponent],
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

        @if (celluleOptions().length > 1) {
          <label class="scope-cellule-picker">
            <span>Cellule active</span>
            <select
              class="scope-cellule-select"
              [value]="selectedCelluleId()"
              (change)="onCelluleChange($event)"
            >
              @for (opt of celluleOptions(); track opt.id) {
                <option [value]="opt.id">{{ opt.label }}</option>
              }
            </select>
          </label>
        }

        @if (balanceAlert(); as alert) {
          <div class="sat-imbalance-banner" role="alert">
            <strong>Déséquilibre des effectifs du samedi</strong>
            <p>
              Effectif périmètre :
              <strong>{{ alert.totalCount }}</strong>
              = {{ alert.alwaysOnCount }} (tous les samedis 4h)
              + {{ alert.group1Count }} (alternance G1)
              + {{ alert.group2Count }} (alternance G2)
              @if (alert.ungroupedCount > 0) {
                + {{ alert.ungroupedCount }} (alternance sans groupe)
              }
            </p>
            <p>
              Présents un samedi donné :
              <strong>{{ alert.projectedSaturdayGroup1 }}</strong> (semaine G1 : {{ alert.alwaysOnCount }}+{{ alert.group1Count }})
              vs
              <strong>{{ alert.projectedSaturdayGroup2 }}</strong> (semaine G2 : {{ alert.alwaysOnCount }}+{{ alert.group2Count }})
              — écart de {{ alert.imbalanceDelta }} sur l’alternance.
              Rééquilibrez les groupes 1 et 2.
            </p>
            @if (alert.ungroupedCount > 0) {
              <p>
                {{ alert.ungroupedCount }} collaborateur(s) en alternance sans groupe assigné.
              </p>
            }
            <p>
              <button type="button" class="sat-renfort-link" (click)="goToReinforcement()">
                Créer une demande de renfort
              </button>
            </p>
          </div>
        } @else if (balanceSummary(); as summary) {
          <div class="sat-balance-ok">
            Effectif :
            {{ summary.totalCount }}
            = {{ summary.alwaysOnCount }} (tous sam. 4h)
            + {{ summary.group1Count }} (G1)
            + {{ summary.group2Count }} (G2)
            · Présents samedi : {{ summary.projectedSaturdayGroup1 }} / {{ summary.projectedSaturdayGroup2 }}
          </div>
        }

        <div class="scope-search">
          <app-lucide-icon [icon]="icons.search" className="w-4 h-4 scope-search-icon" />
          <input
            type="search"
            class="scope-search-input"
            placeholder="Rechercher : nom, rôle, cellule, service, pôle…"
            [value]="searchQuery()"
            (input)="onSearchInput($event)"
            aria-label="Rechercher dans le périmètre"
          />
          @if (searchQuery()) {
            <button type="button" class="scope-search-clear" (click)="clearSearch()">Effacer</button>
          }
          <span class="scope-search-count">
            {{ filteredRows().length }} / {{ rows().length }}
          </span>
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
                  <th>Mode samedi</th>
                  <th class="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                @if (filteredRows().length === 0) {
                  <tr>
                    <td colspan="8" class="text-center prime-cell-muted py-8">
                      {{ rows().length === 0 ? 'Aucune donnée.' : 'Aucun résultat pour cette recherche.' }}
                    </td>
                  </tr>
                } @else {
                  @for (item of filteredRows(); track item.id) {
                    <tr>
                      <td><span class="prime-cell-strong">{{ item.fullName }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.role }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.operationalDepartment }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.pole }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.cellule }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.service }}</span></td>
                      <td>
                        <span class="sat-mode-chip" [attr.data-mode]="item.effectiveMode">
                          {{ modeLabel(item) }}
                        </span>
                        @if (item.isSpecialCase) {
                          <span
                            class="special-case-chip"
                            [title]="item.specialCaseDescription || 'Cas particulier'"
                          >
                            Cas particulier
                          </span>
                        }
                        @if (item.isPlateauTraining) {
                          <span class="plateau-training-chip" title="Jamais ouverture / fermeture">
                            Formation plateau
                          </span>
                        }
                      </td>
                      <td class="text-right scope-actions">
                        <button
                          type="button"
                          class="scope-level-btn"
                          title="Modifier le niveau contractuel"
                          (click)="openContractLevelModal(item)"
                        >
                          <app-lucide-icon [icon]="icons.level" className="w-3.5 h-3.5" />
                          Niveau
                        </button>
                        <button
                          type="button"
                          class="scope-sat-btn"
                          title="Configurer le mode samedi"
                          [disabled]="item.planningUserId == null"
                          (click)="openSaturdayModeModal(item)"
                        >
                          <app-lucide-icon [icon]="icons.saturday" className="w-3.5 h-3.5" />
                          Samedi
                        </button>
                        <button
                          type="button"
                          class="scope-formation-btn"
                          title="En cours de formation plateau (pas d'ouverture/fermeture)"
                          [disabled]="item.planningUserId == null"
                          (click)="openPlateauTrainingModal(item)"
                        >
                          <app-lucide-icon [icon]="icons.formation" className="w-3.5 h-3.5" />
                          Formation
                        </button>
                        <button
                          type="button"
                          class="scope-special-btn"
                          title="Cas particulier (pas de pause +3h/+5h)"
                          [disabled]="item.planningUserId == null"
                          (click)="openSpecialCaseModal(item)"
                        >
                          <app-lucide-icon [icon]="icons.special" className="w-3.5 h-3.5" />
                          Cas partic.
                        </button>
                        <button
                          type="button"
                          class="scope-hist-btn"
                          title="Historique des plannings"
                          [disabled]="item.planningUserId == null"
                          (click)="openHistoryModal(item)"
                        >
                          <app-lucide-icon [icon]="icons.history" className="w-3.5 h-3.5" />
                          Historique
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
      <div class="level-modal-backdrop" appBodyPortal (click)="closeContractLevelModal()">
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

    @if (satModalOpen() && satModalRow(); as row) {
      <div class="level-modal-backdrop" appBodyPortal (click)="closeSaturdayModeModal()">
        <div class="level-modal sat-modal" (click)="$event.stopPropagation()">
          <h3>Mode samedi</h3>
          <p class="level-modal-sub">{{ row.fullName }}</p>
          <div class="level-modal-choices">
            <button
              type="button"
              class="level-choice"
              [class.active]="satModeDraft() === 'default'"
              (click)="satModeDraft.set('default')"
            >
              <strong>Par défaut (Niveau)</strong>
              <span>{{ defaultModeHint(row) }}</span>
            </button>
            <button
              type="button"
              class="level-choice"
              [class.active]="satModeDraft() === 'every4h'"
              (click)="satModeDraft.set('every4h')"
            >
              <strong>Tous les samedis · 4h</strong>
              <span>Demi-journée chaque samedi</span>
            </button>
            <button
              type="button"
              class="level-choice"
              [class.active]="satModeDraft() === 'alternate8h'"
              (click)="satModeDraft.set('alternate8h')"
            >
              <strong>Alternance · 8h</strong>
              <span>Un samedi sur deux, journée complète</span>
            </button>
          </div>

          @if (satModeDraft() === 'alternate8h' || (satModeDraft() === 'default' && (row.level ?? 1) !== 1)) {
            <div class="sat-group-picker">
              <span>Groupe d’alternance</span>
              <div class="sat-group-btns">
                <button
                  type="button"
                  class="sat-group-btn"
                  [class.active]="satGroupDraft() === 1"
                  (click)="satGroupDraft.set(1)"
                >
                  Groupe 1
                </button>
                <button
                  type="button"
                  class="sat-group-btn"
                  [class.active]="satGroupDraft() === 2"
                  (click)="satGroupDraft.set(2)"
                >
                  Groupe 2
                </button>
              </div>
            </div>
          }

          @if (satError()) {
            <p class="level-modal-error">{{ satError() }}</p>
          }
          <div class="level-modal-actions">
            <button type="button" class="ky-btn-secondary" (click)="closeSaturdayModeModal()">Annuler</button>
            <button
              type="button"
              class="ky-btn-primary"
              [disabled]="satSaving() || row.planningUserId == null"
              (click)="saveSaturdayMode()"
            >
              {{ satSaving() ? 'Enregistrement…' : 'Enregistrer' }}
            </button>
          </div>
        </div>
      </div>
    }

    @if (specialModalOpen() && specialModalRow(); as row) {
      <div class="level-modal-backdrop" appBodyPortal (click)="closeSpecialCaseModal()">
        <div class="level-modal sat-modal" (click)="$event.stopPropagation()">
          <h3>Cas particulier</h3>
          <p class="level-modal-sub">{{ row.fullName }}</p>
          <p class="level-modal-hint">
            Les cas particuliers n’ont pas de pause aux extrêmes +3h / +5h sur les cellules critiques.
          </p>
          <label class="special-toggle">
            <input
              type="checkbox"
              [checked]="specialCaseDraft()"
              (change)="onSpecialCaseToggle($event)"
            />
            Marquer comme cas particulier
          </label>
          @if (specialCaseDraft()) {
            <label class="special-desc-label">
              Description
              <textarea
                class="special-desc-input"
                rows="3"
                maxlength="500"
                placeholder="Ex. diabétique, expatrié…"
                [value]="specialDescDraft()"
                (input)="onSpecialDescInput($event)"
              ></textarea>
            </label>
          }
          @if (specialError()) {
            <p class="level-modal-error">{{ specialError() }}</p>
          }
          <div class="level-modal-actions">
            <button type="button" class="ky-btn-secondary" (click)="closeSpecialCaseModal()">Annuler</button>
            <button
              type="button"
              class="ky-btn-primary"
              [disabled]="specialSaving() || row.planningUserId == null"
              (click)="saveSpecialCase()"
            >
              {{ specialSaving() ? 'Enregistrement…' : 'Enregistrer' }}
            </button>
          </div>
        </div>
      </div>
    }

    @if (plateauModalOpen() && plateauModalRow(); as row) {
      <div class="level-modal-backdrop" appBodyPortal (click)="closePlateauTrainingModal()">
        <div class="level-modal sat-modal" (click)="$event.stopPropagation()">
          <h3>Formation plateau</h3>
          <p class="level-modal-sub">{{ row.fullName }}</p>
          <p class="level-modal-hint">
            En cours de formation plateau : jamais en ouverture ni fermeture, même avec un Confirmé/Expert.
          </p>
          <label class="special-toggle">
            <input
              type="checkbox"
              [checked]="plateauDraft()"
              (change)="onPlateauToggle($event)"
            />
            En cours de formation plateau
          </label>
          @if (plateauError()) {
            <p class="level-modal-error">{{ plateauError() }}</p>
          }
          <div class="level-modal-actions">
            <button type="button" class="ky-btn-secondary" (click)="closePlateauTrainingModal()">Annuler</button>
            <button
              type="button"
              class="ky-btn-primary"
              [disabled]="plateauSaving() || row.planningUserId == null"
              (click)="savePlateauTraining()"
            >
              {{ plateauSaving() ? 'Enregistrement…' : 'Enregistrer' }}
            </button>
          </div>
        </div>
      </div>
    }

    @if (histModalOpen() && histModalRow(); as row) {
      <div class="level-modal-backdrop" appBodyPortal (click)="closeHistoryModal()">
        <div class="level-modal hist-modal hist-modal-wide" (click)="$event.stopPropagation()" role="dialog" aria-modal="true">
          <div class="hist-modal-head">
            <div>
              <h3>Plannings — {{ row.fullName }}</h3>
              <p class="level-modal-hint">
                Vue employé (jours / shifts) filtrée par période.
              </p>
            </div>
            <button type="button" class="hist-close" (click)="closeHistoryModal()" aria-label="Fermer">×</button>
          </div>

          <label class="hist-period-label">
            Période
            <select
              class="hist-period-select"
              [value]="histPeriod()"
              (change)="onHistoryPeriodChange($event)"
            >
              @for (opt of histPeriodOptions; track opt.value) {
                <option [value]="opt.value">{{ opt.label }}</option>
              }
            </select>
          </label>

          @if (histLoading()) {
            <p class="hist-empty">Chargement…</p>
          } @else if (histError()) {
            <p class="level-modal-error">{{ histError() }}</p>
          } @else {
            <div class="hist-weeks-scroll">
              <app-agent-planning-weeks [plannings]="histWeeks()" />
            </div>
          }

          <div class="level-modal-actions">
            <button type="button" class="ky-btn-secondary" (click)="closeHistoryModal()">Fermer</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .scope-actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.4rem;
        flex-wrap: wrap;
      }

      .scope-level-btn,
      .scope-sat-btn,
      .scope-special-btn,
      .scope-formation-btn,
      .scope-hist-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        border-radius: 0.5rem;
        padding: 0.35rem 0.65rem;
        font-size: 0.72rem;
        font-weight: 700;
        cursor: pointer;
      }

      .scope-level-btn {
        background: color-mix(in srgb, var(--warning, #d97706) 12%, var(--bg-card, #fff));
        color: #b45309;
        border: 1px solid color-mix(in srgb, var(--warning, #d97706) 35%, var(--border-color));
      }

      .scope-sat-btn {
        background: color-mix(in srgb, #0369a1 10%, var(--bg-card, #fff));
        color: #0369a1;
        border: 1px solid color-mix(in srgb, #0369a1 30%, var(--border-color));
      }

      .scope-special-btn {
        background: color-mix(in srgb, #7c3aed 10%, var(--bg-card, #fff));
        color: #6d28d9;
        border: 1px solid color-mix(in srgb, #7c3aed 30%, var(--border-color));
      }

      .scope-formation-btn {
        background: color-mix(in srgb, #0f766e 10%, var(--bg-card, #fff));
        color: #0f766e;
        border: 1px solid color-mix(in srgb, #0f766e 30%, var(--border-color));
      }

      .scope-hist-btn {
        background: color-mix(in srgb, #475569 8%, var(--bg-card, #fff));
        color: #334155;
        border: 1px solid color-mix(in srgb, #64748b 28%, var(--border-color));
      }

      .scope-sat-btn:disabled,
      .scope-special-btn:disabled,
      .scope-formation-btn:disabled,
      .scope-hist-btn:disabled {
        opacity: 0.45;
        cursor: not-allowed;
      }

      .scope-level-btn:hover,
      .scope-sat-btn:hover:not(:disabled),
      .scope-special-btn:hover:not(:disabled),
      .scope-formation-btn:hover:not(:disabled),
      .scope-hist-btn:hover:not(:disabled) {
        opacity: 0.9;
      }

      .special-case-chip {
        display: inline-block;
        margin-left: 0.35rem;
        font-size: 0.68rem;
        font-weight: 700;
        padding: 0.15rem 0.45rem;
        border-radius: 0.35rem;
        background: #f3e8ff;
        color: #6d28d9;
        vertical-align: middle;
      }

      .plateau-training-chip {
        display: inline-block;
        margin-left: 0.35rem;
        font-size: 0.68rem;
        font-weight: 700;
        padding: 0.15rem 0.45rem;
        border-radius: 0.35rem;
        background: #ccfbf1;
        color: #0f766e;
        vertical-align: middle;
      }

      .special-toggle {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.88rem;
        font-weight: 600;
        margin: 0.75rem 0 0.5rem;
        cursor: pointer;
      }

      .special-desc-label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--text-secondary, #64748b);
        margin-top: 0.5rem;
      }

      .special-desc-input {
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 0.5rem;
        padding: 0.5rem 0.65rem;
        font-size: 0.875rem;
        font-family: inherit;
        resize: vertical;
        min-height: 4rem;
      }

      .level-modal-hint {
        margin: 0 0 0.5rem;
        font-size: 0.8rem;
        color: var(--text-muted, #64748b);
        line-height: 1.4;
      }

      .hist-modal-head {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 0.75rem;
      }

      .hist-modal-head h3 {
        margin: 0;
      }

      .hist-close {
        border: none;
        background: transparent;
        font-size: 1.5rem;
        line-height: 1;
        cursor: pointer;
        color: #64748b;
        padding: 0 0.25rem;
      }

      .hist-weeks-scroll {
        overflow-y: auto;
        flex: 1 1 auto;
        min-height: 280px;
        margin: 0.5rem 0 0.25rem;
        padding-right: 0.35rem;
      }

      .hist-period-label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--text-secondary, #64748b);
        margin: 0.75rem 0 0.5rem;
      }

      .hist-period-select {
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 0.5rem;
        padding: 0.45rem 0.65rem;
        font-size: 0.875rem;
        background: var(--bg-card, #fff);
        color: var(--text-primary, #0f172a);
        max-width: 220px;
      }

      .hist-empty {
        margin: 1rem 0;
        font-size: 0.875rem;
        color: var(--text-secondary, #64748b);
        text-align: center;
      }

      .sat-mode-chip {
        display: inline-block;
        font-size: 0.72rem;
        font-weight: 600;
        padding: 0.2rem 0.5rem;
        border-radius: 0.4rem;
        background: #f1f5f9;
        color: #334155;
      }

      .sat-mode-chip[data-mode='1'] {
        background: #ecfdf5;
        color: #047857;
      }

      .sat-mode-chip[data-mode='2'] {
        background: #eff6ff;
        color: #1d4ed8;
      }

      .sat-imbalance-banner {
        margin-bottom: 1rem;
        padding: 0.9rem 1.1rem;
        border-radius: 0.75rem;
        border: 1px solid #f59e0b;
        background: #fffbeb;
        color: #92400e;
      }

      .sat-imbalance-banner strong {
        display: block;
        margin-bottom: 0.25rem;
      }

      .sat-imbalance-banner p {
        margin: 0.15rem 0 0;
        font-size: 0.88rem;
      }

      .sat-renfort-link {
        margin-top: 0.25rem;
        border: none;
        background: transparent;
        color: #92400e;
        font-weight: 700;
        font-size: 0.85rem;
        text-decoration: underline;
        cursor: pointer;
        padding: 0;
      }

      .sat-imbalance-meta,
      .sat-balance-ok {
        font-size: 0.8rem;
        color: var(--text-muted);
      }

      .sat-balance-ok {
        margin-bottom: 1rem;
        padding: 0.65rem 1rem;
        border-radius: 0.65rem;
        background: #f8fafc;
        border: 1px solid var(--border-color);
      }

      .scope-search {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 1rem;
        padding: 0.5rem 0.75rem;
        border: 1px solid var(--border-color);
        border-radius: 0.65rem;
        background: var(--bg-card, #fff);
      }

      .scope-search-icon {
        color: var(--text-muted);
        flex-shrink: 0;
      }

      .scope-search-input {
        flex: 1;
        border: none;
        outline: none;
        background: transparent;
        font-size: 0.9rem;
        color: var(--text-primary, #0f172a);
        min-width: 0;
      }

      .scope-search-clear {
        border: none;
        background: transparent;
        color: var(--text-muted);
        font-size: 0.78rem;
        font-weight: 600;
        cursor: pointer;
        padding: 0.2rem 0.4rem;
      }

      .scope-search-clear:hover {
        color: var(--text-primary, #0f172a);
      }

      .scope-search-count {
        font-size: 0.75rem;
        color: var(--text-muted);
        white-space: nowrap;
      }

      .level-modal-backdrop {
        position: fixed;
        inset: 0;
        z-index: 10000;
        background: rgba(15, 23, 42, 0.45);
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 1rem;
        box-sizing: border-box;
      }

      .level-modal {
        background: var(--bg-card, #fff);
        border-radius: 12px;
        padding: 1.25rem 1.5rem;
        width: min(420px, 100%);
        max-height: min(90vh, 640px);
        overflow-y: auto;
        box-shadow: 0 20px 40px rgba(0, 0, 0, 0.15);
        margin: auto;
      }

      /* Après .level-modal : sinon width 420px écrase hist-modal-wide */
      .level-modal.hist-modal,
      .level-modal.hist-modal-wide {
        width: min(1100px, 96vw);
        max-height: min(92vh, 900px);
        overflow: hidden;
        display: flex;
        flex-direction: column;
        padding: 1.35rem 1.65rem;
      }

      .sat-modal {
        width: min(460px, 100%);
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

      .sat-group-picker {
        margin-top: 12px;
        display: flex;
        flex-direction: column;
        gap: 8px;
        font-size: 0.82rem;
        font-weight: 600;
      }

      .sat-group-btns {
        display: flex;
        gap: 8px;
      }

      .sat-group-btn {
        flex: 1;
        border: 1px solid var(--border-color);
        border-radius: 8px;
        padding: 8px;
        background: #fff;
        cursor: pointer;
        font-weight: 600;
        font-size: 0.8rem;
      }

      .sat-group-btn.active {
        border-color: #0369a1;
        background: #e0f2fe;
        color: #0369a1;
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

      .scope-cellule-picker {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        margin-bottom: 1rem;
        max-width: 28rem;
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--text-secondary, #64748b);
      }

      .scope-cellule-select {
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 0.5rem;
        padding: 0.5rem 0.75rem;
        font-size: 0.9rem;
        font-weight: 500;
        background: var(--bg-card, #fff);
        color: var(--text-primary, #0f172a);
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuperviseurScopePageComponent {
  private readonly roleService = inject(RoleService);
  private readonly orgApi = inject(PrimeOrgApiService);
  private readonly userService = inject(UserService);
  private readonly planningService = inject(PlanningService);
  private readonly session = inject(KyntusSessionService);
  private readonly router = inject(Router);

  readonly icons = { level: Award, saturday: CalendarDays, search: Search, history: History, special: Tag, formation: GraduationCap };

  readonly histPeriodOptions: { value: AgentHistoryPeriod; label: string }[] = [
    { value: 'thisMonth', label: 'Ce mois' },
    { value: 'lastMonth', label: 'Mois dernier' },
    { value: 'last3Months', label: '3 derniers mois' },
    { value: 'thisYear', label: 'Cette année' },
    { value: 'all', label: 'Tout' },
  ];

  readonly rows = signal<ScopeRow[]>([]);
  readonly loading = signal(true);
  readonly searchQuery = signal('');
  readonly celluleOptions = signal<CelluleOption[]>([]);
  readonly selectedCelluleId = signal('');

  /** Bilan calé sur tout le périmètre (indépendant de la recherche). */
  readonly scopedBalance = computed(() => {
    const rows = this.rows().filter((r) => r.planningUserId != null);
    if (!rows.length) return null;

    let alwaysOn = 0;
    let g1 = 0;
    let g2 = 0;
    let ungrouped = 0;

    for (const r of rows) {
      const mode = r.effectiveMode ?? this.resolveEffectiveMode(r.saturdayWorkMode, r.level);
      if (mode === 1) {
        alwaysOn++;
        continue;
      }
      if (r.groupNumber === 1) g1++;
      else if (r.groupNumber === 2) g2++;
      else ungrouped++;
    }

    const projected1 = alwaysOn + g1;
    const projected2 = alwaysOn + g2;
    const groupDelta = Math.abs(g1 - g2);
    const isImbalanced = groupDelta >= 2 || ungrouped > 0;
    const totalCount = alwaysOn + g1 + g2 + ungrouped;

    return {
      totalCount,
      alwaysOnCount: alwaysOn,
      group1Count: g1,
      group2Count: g2,
      ungroupedCount: ungrouped,
      projectedSaturdayGroup1: projected1,
      projectedSaturdayGroup2: projected2,
      isImbalanced,
      imbalanceDelta: groupDelta,
    };
  });

  readonly filteredRows = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const list = this.rows();
    if (!q) return list;
    return list.filter((r) => this.rowMatchesSearch(r, q));
  });

  readonly balanceAlert = computed(() => {
    const scoped = this.scopedBalance();
    return scoped?.isImbalanced ? scoped : null;
  });
  readonly balanceSummary = computed(() => {
    const scoped = this.scopedBalance();
    if (!scoped || scoped.isImbalanced) return null;
    return scoped;
  });

  readonly levelModalOpen = signal(false);
  readonly levelModalRow = signal<ScopeRow | null>(null);
  readonly levelModalPlanningUserId = signal<number | null>(null);
  readonly levelDraft = signal<1 | 2 | 3>(1);
  readonly levelSaving = signal(false);
  readonly levelError = signal('');

  readonly satModalOpen = signal(false);
  readonly satModalRow = signal<ScopeRow | null>(null);
  readonly satModeDraft = signal<ModeDraft>('default');
  readonly satGroupDraft = signal<1 | 2>(1);
  readonly satSaving = signal(false);
  readonly satError = signal('');

  readonly specialModalOpen = signal(false);
  readonly specialModalRow = signal<ScopeRow | null>(null);
  readonly specialCaseDraft = signal(false);
  readonly specialDescDraft = signal('');
  readonly specialSaving = signal(false);
  readonly specialError = signal('');

  readonly plateauModalOpen = signal(false);
  readonly plateauModalRow = signal<ScopeRow | null>(null);
  readonly plateauDraft = signal(false);
  readonly plateauSaving = signal(false);
  readonly plateauError = signal('');

  readonly histModalOpen = signal(false);
  readonly histModalRow = signal<ScopeRow | null>(null);
  readonly histPeriod = signal<AgentHistoryPeriod>('thisMonth');
  readonly histWeeks = signal<AgentPlanningWeek[]>([]);
  readonly histLoading = signal(false);
  readonly histError = signal('');

  private planningUsersByGuid = new Map<string, User>();

  constructor() {
    effect(() => {
      void this.roleService.currentUser().id;
      this.fetch();
    });
  }

  modeLabel(row: ScopeRow): string {
    const mode = row.effectiveMode ?? this.resolveEffectiveMode(row.saturdayWorkMode, row.level);
    if (mode === 1) {
      return row.saturdayWorkMode == null ? 'Tous sam. 4h (défaut)' : 'Tous sam. 4h';
    }
    if (mode === 2) {
      const g =
        row.groupNumber === 1 || row.groupNumber === 2
          ? ` · G${row.groupNumber}`
          : ' · sans groupe';
      return row.saturdayWorkMode == null ? `Alternance 8h (défaut)${g}` : `Alternance 8h${g}`;
    }
    return '—';
  }

  goToReinforcement(): void {
    const subId = this.rows().find((r) => r.subServiceId != null)?.subServiceId ?? null;
    void this.router.navigate(['/planning/demandes-renfort'], {
      queryParams: subId != null ? { subServiceId: subId } : {},
    });
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.searchQuery.set(value);
  }

  clearSearch(): void {
    this.searchQuery.set('');
  }

  private rowMatchesSearch(row: ScopeRow, q: string): boolean {
    const haystack = [
      row.fullName,
      row.role,
      row.operationalDepartment,
      row.pole,
      row.cellule,
      row.service,
      this.modeLabel(row),
    ]
      .join(' ')
      .toLowerCase();
    return haystack.includes(q);
  }

  private resolveEffectiveMode(
    saturdayWorkMode: number | null | undefined,
    level: number | null | undefined,
  ): number {
    if (saturdayWorkMode === 1 || saturdayWorkMode === 2) return saturdayWorkMode;
    return (level ?? 1) === 1 ? 1 : 2;
  }

  defaultModeHint(row: ScopeRow): string {
    const level = row.level ?? 1;
    return level === 1
      ? 'Niveau 1 → tous les samedis 4h'
      : 'Niveau 2/3 → alternance 8h';
  }

  openContractLevelModal(row: ScopeRow): void {
    this.levelModalRow.set(row);
    this.levelModalPlanningUserId.set(row.planningUserId);
    this.levelDraft.set((row.level === 2 || row.level === 3 ? row.level : 1) as 1 | 2 | 3);
    this.levelError.set('');
    this.levelModalOpen.set(true);
    if (row.planningUserId != null) return;

    this.levelSaving.set(true);
    this.userService.getAllUsers().subscribe({
      next: (users) => {
        this.planningUsersByGuid = new Map(
          users
            .map((u) => [resolveUserGuid(u).toLowerCase(), u] as const)
            .filter(([g]) => !!g),
        );
        const user = this.planningUsersByGuid.get(row.id.toLowerCase());
        this.levelSaving.set(false);
        if (!user) {
          this.levelError.set('Employé introuvable dans le référentiel planning.');
          return;
        }
        this.levelModalPlanningUserId.set(user.id);
        this.levelDraft.set((user.level === 2 || user.level === 3 ? user.level : 1) as 1 | 2 | 3);
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
        void this.refreshSaturdayMeta();
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

  openSaturdayModeModal(row: ScopeRow): void {
    if (row.planningUserId == null) return;
    this.satModalRow.set(row);
    if (row.saturdayWorkMode === 1) this.satModeDraft.set('every4h');
    else if (row.saturdayWorkMode === 2) this.satModeDraft.set('alternate8h');
    else this.satModeDraft.set('default');
    this.satGroupDraft.set(row.groupNumber === 2 ? 2 : 1);
    this.satError.set('');
    this.satModalOpen.set(true);
  }

  closeSaturdayModeModal(): void {
    this.satModalOpen.set(false);
    this.satModalRow.set(null);
    this.satError.set('');
    this.satSaving.set(false);
  }

  openSpecialCaseModal(row: ScopeRow): void {
    if (row.planningUserId == null) return;
    this.specialModalRow.set(row);
    this.specialCaseDraft.set(!!row.isSpecialCase);
    this.specialDescDraft.set(row.specialCaseDescription ?? '');
    this.specialError.set('');
    this.specialModalOpen.set(true);
  }

  closeSpecialCaseModal(): void {
    this.specialModalOpen.set(false);
    this.specialModalRow.set(null);
    this.specialError.set('');
    this.specialSaving.set(false);
  }

  onSpecialCaseToggle(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.specialCaseDraft.set(checked);
    if (!checked) this.specialDescDraft.set('');
  }

  onSpecialDescInput(event: Event): void {
    this.specialDescDraft.set((event.target as HTMLTextAreaElement).value);
  }

  saveSpecialCase(): void {
    const row = this.specialModalRow();
    if (!row?.planningUserId) return;
    const enabled = this.specialCaseDraft();
    const desc = this.specialDescDraft().trim();
    if (enabled && desc.length < 3) {
      this.specialError.set('Description obligatoire (ex. diabétique, expatrié).');
      return;
    }
    this.specialSaving.set(true);
    this.specialError.set('');
    this.planningService
      .setEmployeeSpecialCase({
        userId: row.planningUserId,
        isSpecialCase: enabled,
        description: enabled ? desc : null,
      })
      .subscribe({
        next: () => {
          this.specialSaving.set(false);
          this.rows.update((list) =>
            list.map((r) =>
              r.id === row.id
                ? {
                    ...r,
                    isSpecialCase: enabled,
                    specialCaseDescription: enabled ? desc : null,
                  }
                : r,
            ),
          );
          const pu = this.planningUsersByGuid.get(row.id.toLowerCase());
          if (pu) {
            pu.isSpecialCase = enabled;
            pu.specialCaseDescription = enabled ? desc : null;
          }
          this.closeSpecialCaseModal();
        },
        error: (err: unknown) => {
          this.specialSaving.set(false);
          const msg =
            err instanceof HttpErrorResponse
              ? (err.error as { message?: string } | null)?.message
              : undefined;
          this.specialError.set(msg ?? 'Échec de l’enregistrement.');
        },
      });
  }

  openPlateauTrainingModal(row: ScopeRow): void {
    if (row.planningUserId == null) return;
    this.plateauModalRow.set(row);
    this.plateauDraft.set(!!row.isPlateauTraining);
    this.plateauError.set('');
    this.plateauModalOpen.set(true);
  }

  closePlateauTrainingModal(): void {
    this.plateauModalOpen.set(false);
    this.plateauModalRow.set(null);
    this.plateauError.set('');
    this.plateauSaving.set(false);
  }

  onPlateauToggle(event: Event): void {
    this.plateauDraft.set((event.target as HTMLInputElement).checked);
  }

  savePlateauTraining(): void {
    const row = this.plateauModalRow();
    if (!row?.planningUserId) return;
    const enabled = this.plateauDraft();
    this.plateauSaving.set(true);
    this.plateauError.set('');
    this.planningService
      .setEmployeePlateauTraining({
        userId: row.planningUserId,
        isPlateauTraining: enabled,
      })
      .subscribe({
        next: () => {
          this.plateauSaving.set(false);
          this.rows.update((list) =>
            list.map((r) => (r.id === row.id ? { ...r, isPlateauTraining: enabled } : r)),
          );
          const pu = this.planningUsersByGuid.get(row.id.toLowerCase());
          if (pu) pu.isPlateauTraining = enabled;
          this.closePlateauTrainingModal();
        },
        error: (err: unknown) => {
          this.plateauSaving.set(false);
          const msg =
            err instanceof HttpErrorResponse
              ? (err.error as { message?: string } | null)?.message
              : undefined;
          this.plateauError.set(msg ?? 'Échec de l’enregistrement.');
        },
      });
  }


  openHistoryModal(row: ScopeRow): void {
    if (row.planningUserId == null) return;
    this.histModalRow.set(row);
    this.histPeriod.set('thisMonth');
    this.histWeeks.set([]);
    this.histError.set('');
    this.histModalOpen.set(true);
    this.loadHistory();
  }

  closeHistoryModal(): void {
    this.histModalOpen.set(false);
    this.histModalRow.set(null);
    this.histWeeks.set([]);
    this.histError.set('');
    this.histLoading.set(false);
  }

  onHistoryPeriodChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as AgentHistoryPeriod;
    this.histPeriod.set(value);
    this.loadHistory();
  }

  private loadHistory(): void {
    const row = this.histModalRow();
    if (!row?.planningUserId) return;

    this.histLoading.set(true);
    this.histError.set('');
    this.planningService.getAgentPlanningHistory(row.planningUserId, this.histPeriod()).subscribe({
      next: (items) => {
        this.histWeeks.set(items ?? []);
        this.histLoading.set(false);
      },
      error: (err: unknown) => {
        this.histLoading.set(false);
        this.histWeeks.set([]);
        const msg =
          err instanceof HttpErrorResponse
            ? (err.error as { message?: string } | null)?.message
            : undefined;
        this.histError.set(msg ?? 'Impossible de charger l’historique.');
      },
    });
  }

  saveSaturdayMode(): void {
    const row = this.satModalRow();
    if (!row?.planningUserId) return;

    const draft = this.satModeDraft();
    const saturdayWorkMode =
      draft === 'every4h' ? 1 : draft === 'alternate8h' ? 2 : null;
    const needsGroup =
      draft === 'alternate8h' || (draft === 'default' && (row.level ?? 1) !== 1);

    this.satSaving.set(true);
    this.satError.set('');
    this.planningService
      .setSaturdayWorkMode({
        userId: row.planningUserId,
        saturdayWorkMode,
        groupNumber: needsGroup ? this.satGroupDraft() : null,
        authUserId: this.session.getAuthUserId() || null,
      })
      .subscribe({
        next: () => {
          this.satSaving.set(false);
          this.closeSaturdayModeModal();
          void this.refreshSaturdayMeta();
        },
        error: (err: unknown) => {
          this.satSaving.set(false);
          const msg =
            err instanceof HttpErrorResponse
              ? (err.error as { message?: string } | null)?.message
              : undefined;
          this.satError.set(msg ?? 'Échec de la mise à jour du mode samedi.');
        },
      });
  }

  private async refreshSaturdayMeta(): Promise<void> {
    const currentRows = this.rows();
    const subIds = [
      ...new Set(
        currentRows
          .map((r) => r.subServiceId)
          .filter((id): id is number => id != null && id > 0),
      ),
    ];

    const modeByGuid = new Map<string, SaturdayEmployeeMode>();
    const authUserId = this.session.getAuthUserId();

    await Promise.all(
      subIds.map(async (subId) => {
        try {
          const bal = await firstValueFrom(this.planningService.getSaturdayBalance(subId));
          for (const emp of bal.employees ?? []) {
            if (emp.guid) modeByGuid.set(emp.guid.toLowerCase(), emp);
          }
          if (bal.isImbalanced && authUserId > 0) {
            try {
              await firstValueFrom(this.planningService.notifySaturdayImbalance(subId, authUserId));
            } catch {
              /* notification best-effort */
            }
          }
        } catch {
          /* ignore missing balance for a cellule */
        }
      }),
    );

    this.rows.set(
      currentRows.map((r) => {
        const m = modeByGuid.get(r.id.toLowerCase());
        if (!m) return r;
        return {
          ...r,
          planningUserId: m.userId,
          level: m.level,
          saturdayWorkMode: m.saturdayWorkMode,
          effectiveMode: m.effectiveMode,
          groupNumber: m.groupNumber,
          isSpecialCase: m.isSpecialCase ?? r.isSpecialCase,
          specialCaseDescription: m.specialCaseDescription ?? r.specialCaseDescription,
          isPlateauTraining: m.isPlateauTraining ?? r.isPlateauTraining,
        };
      }),
    );
  }

  onCelluleChange(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    this.selectedCelluleId.set(id);
    const userId = this.roleService.currentUser().id;
    try {
      localStorage.setItem(celluleStorageKey(userId), id);
    } catch {
      /* ignore */
    }
    this.fetch(true);
  }

  private pickActiveCelluleId(userId: string, ids: string[]): string {
    if (ids.length === 0) return '';
    let stored = '';
    try {
      stored = (localStorage.getItem(celluleStorageKey(userId)) ?? '').trim();
    } catch {
      stored = '';
    }
    if (stored && ids.includes(stored)) return stored;
    return ids[0];
  }

  private fetch(keepSelection = false): void {
    this.loading.set(true);
    const current = this.roleService.currentUser();
    void Promise.all([
      PrimeService.getEmployees(),
      firstValueFrom(this.orgApi.loadOverview()),
      PrimeService.getOperationalOrgTree(),
      firstValueFrom(this.userService.getAllUsers()).catch(() => [] as User[]),
    ]).then(async ([employees, overview, orgTree, planningUsers]) => {
      const useLegacyFallback =
        (orgTree.operationalDepartments?.length ?? 0) === 0 &&
        (orgTree.unassignedPoles?.length ?? 0) === 0;
      const legacyDepartments = useLegacyFallback ? await PrimeService.getDepartments() : [];
      const celluleIds = resolveSuperviseurCelluleIds(current.id, current, overview ?? null);
      const options = celluleIds.map((id) => ({
        id,
        label: resolveCelluleLabel(
          id,
          overview ?? null,
          orgTree.operationalDepartments ?? [],
          orgTree.unassignedPoles ?? [],
        ),
      }));
      this.celluleOptions.set(options);
      const celluleId = keepSelection
        ? this.selectedCelluleId() || this.pickActiveCelluleId(current.id, celluleIds)
        : this.pickActiveCelluleId(current.id, celluleIds);
      this.selectedCelluleId.set(celluleId);
      const scopeEmployees = dedupeEmployeesByEmail(
        employeesInSuperviseurCellule(employees, celluleId),
      );

      this.planningUsersByGuid = new Map(
        planningUsers
          .map((u) => [resolveUserGuid(u).toLowerCase(), u] as const)
          .filter(([g]) => !!g),
      );

      const mapped: ScopeRow[] = scopeEmployees.map((e) => {
        const labels = resolvePlatformOrgLabels(e, legacyDepartments, overview ?? null);
        const planningUser = this.planningUsersByGuid.get(e.id.toLowerCase());
        const saturdayWorkMode = planningUser?.saturdayWorkMode ?? null;
        const level = planningUser?.level ?? null;
        return {
          id: e.id,
          fullName: `${e.firstName} ${e.lastName}`,
          role: e.role,
          operationalDepartment: labels.operationalDepartment,
          pole: labels.pole,
          cellule: labels.cellule,
          service: labels.service,
          planningUserId: planningUser?.id ?? null,
          subServiceId: planningUser?.subServiceId ?? null,
          level,
          saturdayWorkMode,
          effectiveMode: planningUser
            ? this.resolveEffectiveMode(saturdayWorkMode, level)
            : null,
          groupNumber: 0,
          isSpecialCase: !!planningUser?.isSpecialCase,
          specialCaseDescription: planningUser?.specialCaseDescription ?? null,
          isPlateauTraining: !!planningUser?.isPlateauTraining,
        };
      });

      mapped.sort((a, b) => a.fullName.localeCompare(b.fullName));
      this.rows.set(mapped);
      this.loading.set(false);
      await this.refreshSaturdayMeta();
    });
  }
}
