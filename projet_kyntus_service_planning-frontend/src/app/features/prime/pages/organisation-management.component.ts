import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { finalize, type Observable } from 'rxjs';
import {
  Activity,
  Building2,
  Check,
  ChevronDown,
  ChevronRight,
  Plus,
  RefreshCw,
  Trash2,
} from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { PrimeCardComponent } from '../components/prime-card.component';
import {
  PrimeOrgApiService,
  type OrgAssignmentsOverview,
} from '../services/prime-org-api.service';
import type { Department, Employee, LegacyCellule as Cellule, LegacyPole as Pole, Role, Team } from '../models';
import {
  employeeSelectOptionLabel,
  employeesForOrgAssignmentSelect,
  selectValueOrEmpty,
} from '../lib/prime-select-options';
import { RoleService } from '../state/role.service';

function matchAssignmentUserId(
  assignments: { userId: string; etageId?: string; serviceId?: string; celluleId?: string; sousServiceId?: string }[],
  keys: string[],
): string | undefined {
  const keySet = new Set(keys.filter(Boolean));
  if (keySet.size === 0) return undefined;
  for (const a of assignments) {
    const candidates = [a.etageId, a.serviceId, a.celluleId, a.sousServiceId].filter(Boolean) as string[];
    if (candidates.some((c) => keySet.has(c))) return a.userId;
  }
  return undefined;
}

export type OrgTreeSelection =
  | { kind: 'department'; id: string; name: string }
  | { kind: 'pole'; id: string; name: string; departmentId: string }
  | { kind: 'cellule'; id: string; name: string; poleId: string; departmentId: string };

export type FlatPoleRow = {
  departmentId: string;
  departmentName: string;
  poleId: string;
  poleName: string;
};

export type FlatCelluleRow = FlatPoleRow & {
  celluleId: string;
  celluleName: string;
};

export type OrgMainTab = 'departments' | 'poles' | 'cellules' | 'structure';

export type StructureLogEntry = { at: string; message: string };

function httpErrMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as { error?: string } | string | null;
    if (body && typeof body === 'object' && typeof body.error === 'string') return body.error;
    if (typeof body === 'string' && body.length) return body;
    return 'Une erreur est survenue. Réessayez ultérieurement.';
  }
  return err instanceof Error ? err.message : 'Erreur inconnue';
}

@Component({
  selector: 'app-organisation-management',
  standalone: true,
  imports: [LucideIconComponent, PrimeCardComponent],
  template: `
    @if (loading()) {
      <div class="p-8 flex justify-center">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
      </div>
    } @else {
      <div class="p-6 lg:p-8 space-y-6 min-h-full bg-app">
        <div class="flex flex-wrap justify-between items-start gap-4">
          <div>
            <h1 class="text-2xl sm:text-3xl font-bold text-slate-100 tracking-tight">Organisation RH</h1>
            <div class="mt-3 max-w-3xl space-y-2 text-sm leading-relaxed text-slate-400">
              <p>
                <span class="font-medium text-slate-300">Gestion par listes</span> — tous les pôles, cellules et
                services, avec affectation ligne par ligne. Les rôles et la hiérarchie sont alignés automatiquement
                lors de chaque enregistrement.
              </p>
            </div>
          </div>
          <button
            type="button"
            (click)="load(false)"
            [disabled]="saving()"
            class="shrink-0 inline-flex items-center gap-2 rounded-lg border border-navy-700 bg-navy-900 px-4 py-2 text-sm font-medium text-slate-200 hover:bg-navy-800 disabled:opacity-50"
          >
            <app-lucide-icon [icon]="icons.refresh" className="w-4 h-4" />
            Actualiser
          </button>
        </div>

        <div class="flex flex-wrap gap-3 items-center">
          <label class="text-sm text-slate-400 flex items-center gap-2">
            Rechercher
            <input
              type="search"
              class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 w-64"
              placeholder="Filtrer les lignes du tableau actif…"
              [value]="search()"
              (input)="search.set($any($event.target).value)"
            />
          </label>
        </div>

        @if (error()) {
          <div
            class="rounded-lg border border-red-500/40 bg-red-950/40 px-4 py-3 text-sm text-red-200"
            role="alert"
          >
            {{ error() }}
          </div>
        }

        <div class="flex flex-wrap gap-2 border-b border-navy-800 pb-2" role="tablist">
          @for (t of mainTabs; track t.id) {
            <button
              type="button"
              role="tab"
              [attr.aria-selected]="mainTab() === t.id"
              (click)="mainTab.set(t.id)"
              class="rounded-lg px-4 py-2 text-sm font-medium transition-colors"
              [class.bg-indigo-600]="mainTab() === t.id"
              [class.text-white]="mainTab() === t.id"
              [class.bg-navy-900]="mainTab() !== t.id"
              [class.text-slate-300]="mainTab() !== t.id"
              [class.ring-1]="mainTab() === t.id"
              [class.ring-indigo-400]="mainTab() === t.id"
            >
              {{ t.label }}
            </button>
          }
        </div>

        @switch (mainTab()) {
          @case ('departments') {
            <app-prime-card className="p-0" title="Gestion des pôles" description="Un chef de projet par pôle.">
              <div
                class="px-4 py-3 sm:px-6 border-b border-navy-800 bg-navy-950/40 flex flex-col sm:flex-row sm:flex-wrap gap-3 sm:items-end"
              >
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[12rem] flex-1 max-w-md">
                  <span>Nouveau pôle</span>
                  <input
                    type="text"
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 placeholder:text-slate-500"
                    placeholder="Nom du pôle"
                    [value]="newDepartmentName()"
                    (input)="newDepartmentName.set($any($event.target).value)"
                  />
                </label>
                <button
                  type="button"
                  (click)="submitNewDepartment()"
                  [disabled]="saving() || !newDepartmentName().trim()"
                  class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50 shrink-0"
                >
                  <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                  Créer
                </button>
              </div>
              <p class="px-4 sm:px-6 pb-3 text-xs text-slate-500 border-b border-navy-800 bg-navy-950/40">
                Un pôle sans structure ne permet pas encore d’affecter un chef de projet : ajoutez au moins une cellule puis un service
                depuis les onglets dédiés.
              </p>
              <p class="px-4 sm:px-6 py-2 text-xs text-slate-400 border-b border-navy-800">
                {{ filteredDepartmentsForTable().length }} pôle(s) affiché(s)
              </p>
              <div class="overflow-x-auto">
                <table class="prime-table prime-table--dense w-full text-sm text-left">
                  <thead>
                    <tr>
                      <th class="font-medium max-w-[min(100%,20rem)]">Pôle</th>
                      <th class="font-medium">Chef de projet actuel</th>
                      <th class="font-medium min-w-[220px]">Nouveau chef de projet</th>
                      <th class="font-medium w-40 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (dept of filteredDepartmentsForTable(); track dept.id) {
                      <tr>
                        <td class="px-4 py-2.5"><span class="prime-cell-strong">{{ dept.name }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ managerLabel(dept.id) }}</span></td>
                        <td class="px-4 py-2.5">
                          <select
                            class="w-full min-w-[12rem] rounded-lg border border-navy-700 bg-navy-950 px-2 py-1.5 text-slate-200 text-sm"
                            [value]="selectManagerValue(dept.id)"
                            (change)="patchDraftManager(dept.id, selectVal($event))"
                          >
                            <option value="">— Sélectionner —</option>
                            @for (e of employeesForManagerRow(dept.id); track e.id) {
                              <option [value]="e.id">{{ employeeOptionLabel(e) }}</option>
                            }
                          </select>
                        </td>
                        <td class="px-4 py-2.5 text-right">
                          <div class="inline-flex flex-wrap justify-end gap-2">
                            <button
                              type="button"
                              (click)="saveDepartmentManagerRow(dept.id)"
                              [disabled]="saving() || !draftManagerDept(dept.id)"
                              class="rounded-md bg-indigo-600 px-2.5 py-1.5 text-xs font-medium text-white disabled:opacity-50"
                            >
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearDepartmentManagerRow(dept.id)"
                              [disabled]="saving() || !managerUserId(dept.id)"
                              class="rounded-md border border-navy-600 px-2.5 py-1.5 text-xs text-red-300 hover:bg-navy-800 disabled:opacity-50"
                            >
                              Retirer
                            </button>
                          </div>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="4" class="px-4 py-10 text-center text-slate-500 text-sm">
                          Aucun pôle à afficher. Créez un pôle ou modifiez la recherche.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </app-prime-card>
          }
          @case ('poles') {
            <app-prime-card className="p-0" title="Gestion des cellules" description="Un superviseur par cellule.">
              <div
                class="px-4 py-3 sm:px-6 border-b border-navy-800 bg-navy-950/40 flex flex-col lg:flex-row lg:flex-wrap gap-3 lg:items-end"
              >
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Pôle</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [value]="newPoleDeptId()"
                    (change)="newPoleDeptId.set(selectVal($event))"
                  >
                    @for (d of data()?.departments ?? []; track d.id) {
                      <option [value]="d.id">{{ d.name }}</option>
                    }
                  </select>
                </label>
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[12rem] flex-1 max-w-md">
                  <span>Nom de la cellule</span>
                  <input
                    type="text"
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 placeholder:text-slate-500"
                    placeholder="Ex. Cellule relation client"
                    [value]="newPoleName()"
                    (input)="newPoleName.set($any($event.target).value)"
                  />
                </label>
                <button
                  type="button"
                  (click)="submitNewPole()"
                  [disabled]="saving() || !newPoleDeptId() || !newPoleName().trim()"
                  class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50 shrink-0"
                >
                  <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                  Créer la cellule
                </button>
              </div>
              <p class="px-4 sm:px-6 pb-3 text-xs text-slate-500 border-b border-navy-800 bg-navy-950/40">
                Vous pouvez affecter un superviseur dès qu’une cellule existe ; les services peuvent être ajoutés ensuite.
              </p>
              <p class="px-4 sm:px-6 py-2 text-xs text-slate-400 border-b border-navy-800">
                {{ filteredPolesForTable().length }} cellule(s) affichée(s)
              </p>
              <div class="overflow-x-auto">
                <table class="prime-table prime-table--dense w-full text-sm text-left">
                  <thead>
                    <tr>
                      <th class="font-medium">Pôle</th>
                      <th class="font-medium">Cellule</th>
                      <th class="px-4 py-2.5 font-medium">Superviseur actuel</th>
                      <th class="px-4 py-2.5 font-medium min-w-[220px]">Nouveau superviseur</th>
                      <th class="px-4 py-2.5 font-medium w-40 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of filteredPolesForTable(); track row.poleId) {
                      <tr>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.departmentName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-strong">{{ row.poleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ supervisorLabel(row.poleId) }}</span></td>
                        <td class="px-4 py-2.5">
                          <select
                            class="w-full min-w-[12rem] rounded-lg border border-navy-700 bg-navy-950 px-2 py-1.5 text-slate-200 text-sm"
                            [value]="selectSupervisorValue(row.poleId)"
                            (change)="patchDraftSupervisor(row.poleId, selectVal($event))"
                          >
                            <option value="">— Sélectionner —</option>
                            @for (e of employeesForSupervisorRow(row.poleId); track e.id) {
                              <option [value]="e.id">{{ employeeOptionLabel(e) }}</option>
                            }
                          </select>
                        </td>
                        <td class="px-4 py-2.5 text-right">
                          <div class="inline-flex flex-wrap justify-end gap-2">
                            <button
                              type="button"
                              (click)="savePoleSupervisorRow(row.poleId)"
                              [disabled]="saving() || !draftSupervisorPole(row.poleId)"
                              class="rounded-md bg-indigo-600 px-2.5 py-1.5 text-xs font-medium text-white disabled:opacity-50"
                            >
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearPoleSupervisorRow(row.poleId)"
                              [disabled]="saving() || !supervisorUserId(row.poleId)"
                              class="rounded-md border border-navy-600 px-2.5 py-1.5 text-xs text-red-300 hover:bg-navy-800 disabled:opacity-50"
                            >
                              Retirer
                            </button>
                          </div>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="5" class="px-4 py-10 text-center text-slate-500 text-sm">
                          Aucune cellule à afficher. Créez une cellule ou modifiez la recherche.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </app-prime-card>
          }
          @case ('cellules') {
            <app-prime-card
              className="p-0"
              title="Gestion des services"
              description="Référent technique par service ; pilotes rattachés listés par ligne."
            >
              <div
                class="px-4 py-3 sm:px-6 border-b border-navy-800 bg-navy-950/40 flex flex-col xl:flex-row xl:flex-wrap gap-3 xl:items-end"
              >
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Pôle</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [value]="newCellDeptId()"
                    (change)="patchNewCellDept(selectVal($event))"
                  >
                    @for (d of data()?.departments ?? []; track d.id) {
                      <option [value]="d.id">{{ d.name }}</option>
                    }
                  </select>
                </label>
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Cellule</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [value]="newCellPoleId()"
                    (change)="newCellPoleId.set(selectVal($event))"
                  >
                    @for (p of polesForNewCellForm(); track p.id) {
                      <option [value]="p.id">{{ p.name }}</option>
                    }
                  </select>
                </label>
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[12rem] flex-1 max-w-md">
                  <span>Nom du service</span>
                  <input
                    type="text"
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 placeholder:text-slate-500"
                    placeholder="Ex. Support N1"
                    [value]="newCellName()"
                    (input)="newCellName.set($any($event.target).value)"
                  />
                </label>
                <button
                  type="button"
                  (click)="submitNewCellule()"
                  [disabled]="saving() || !newCellPoleId() || !newCellName().trim()"
                  class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50 shrink-0"
                >
                  <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                  Créer le service
                </button>
              </div>
              @if (polesForNewCellForm().length === 0) {
                <p class="px-4 sm:px-6 py-2 text-xs text-amber-400/90">
                  Ce pôle n’a pas encore de cellule : créez-en une depuis l’onglet « Gestion des cellules », puis revenez
                  ici pour ajouter un service.
                </p>
              }
              <p class="px-4 sm:px-6 py-2 text-xs text-slate-400 border-b border-navy-800">
                {{ filteredCellulesForTable().length }} service(s) affiché(s)
              </p>
              <div class="overflow-x-auto">
                <table class="prime-table prime-table--dense w-full text-sm text-left">
                  <thead>
                    <tr>
                      <th class="font-medium w-10"></th>
                      <th class="px-4 py-2.5 font-medium">Pôle</th>
                      <th class="px-4 py-2.5 font-medium">Cellule</th>
                      <th class="px-4 py-2.5 font-medium">Service</th>
                      <th class="px-4 py-2.5 font-medium">Réf. technique actuel</th>
                      <th class="px-4 py-2.5 font-medium min-w-[220px]">Nouveau référent</th>
                      <th class="px-4 py-2.5 font-medium w-40 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of filteredCellulesForTable(); track row.celluleId) {
                      <tr class="align-top">
                        <td class="px-2 py-2.5">
                          <button
                            type="button"
                            class="p-1 rounded text-slate-400 hover:bg-navy-800"
                            (click)="toggleCellPilots(row.celluleId)"
                            [attr.aria-expanded]="cellPilotsExpanded(row.celluleId)"
                            title="Pilotes"
                          >
                            <app-lucide-icon
                              [icon]="cellPilotsExpanded(row.celluleId) ? icons.chevDown : icons.chevRight"
                              className="w-4 h-4"
                            />
                          </button>
                        </td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.departmentName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.poleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-strong">{{ row.celluleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ coachLabel(row.celluleId) }}</span></td>
                        <td class="px-4 py-2.5">
                          <select
                            class="w-full min-w-[12rem] rounded-lg border border-navy-700 bg-navy-950 px-2 py-1.5 text-slate-200 text-sm"
                            [value]="selectCoachValue(row.celluleId)"
                            (change)="patchDraftCoach(row.celluleId, selectVal($event))"
                          >
                            <option value="">— Sélectionner —</option>
                            @for (e of employeesForCoachRow(row.celluleId); track e.id) {
                              <option [value]="e.id">{{ employeeOptionLabel(e) }}</option>
                            }
                          </select>
                        </td>
                        <td class="px-4 py-2.5 text-right">
                          <div class="inline-flex flex-wrap justify-end gap-2">
                            <button
                              type="button"
                              (click)="saveCellCoachRow(row.celluleId)"
                              [disabled]="saving() || !draftCoachCell(row.celluleId)"
                              class="rounded-md bg-indigo-600 px-2.5 py-1.5 text-xs font-medium text-white disabled:opacity-50"
                            >
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearCellCoachRow(row.celluleId)"
                              [disabled]="saving() || !coachUserId(row.celluleId)"
                              class="rounded-md border border-navy-600 px-2.5 py-1.5 text-xs text-red-300 hover:bg-navy-800 disabled:opacity-50"
                            >
                              Retirer
                            </button>
                          </div>
                        </td>
                      </tr>
                      @if (cellPilotsExpanded(row.celluleId)) {
                        <tr class="bg-navy-950/80">
                          <td colspan="7" class="px-6 py-4 border-t border-navy-800">
                            <div class="text-xs text-slate-500 mb-2">Pilotes — {{ row.celluleName }}</div>
                            <ul class="rounded border border-navy-800 divide-y divide-navy-800 max-h-36 overflow-y-auto mb-3">
                              @for (p of pilotsInCell(row.celluleId); track p.id) {
                                <li class="flex justify-between items-center px-3 py-2 text-sm text-slate-200">
                                  <span>{{ p.firstName }} {{ p.lastName }}</span>
                                  <button
                                    type="button"
                                    (click)="removePilot(row.celluleId, p.id)"
                                    [disabled]="saving()"
                                    class="text-xs text-red-400 hover:text-red-300 disabled:opacity-50"
                                  >
                                    Retirer
                                  </button>
                                </li>
                              } @empty {
                                <li class="px-3 py-3 text-sm text-slate-500">Aucun pilote</li>
                              }
                            </ul>
                            @if (!coachUserId(row.celluleId)) {
                              <p class="text-xs text-amber-400/90 mb-2">Affectez un référent technique pour ajouter des pilotes.</p>
                            }
                            <div class="flex flex-wrap gap-2 items-end max-w-lg">
                              <select
                                class="flex-1 min-w-[160px] rounded-lg border border-navy-700 bg-navy-900 px-2 py-2 text-sm text-slate-200"
                                [value]="selectPilotValue(row.celluleId)"
                                (change)="patchDraftPilotCell(row.celluleId, selectVal($event))"
                                [disabled]="!coachUserId(row.celluleId)"
                              >
                                <option value="">— Pilote —</option>
                                @for (e of employeesForPilotRow(row.celluleId); track e.id) {
                                  <option [value]="e.id">{{ employeeOptionLabel(e) }}</option>
                                }
                              </select>
                              @if (teamsForCell(row.celluleId).length > 1) {
                                <select
                                  class="min-w-[120px] rounded-lg border border-navy-700 bg-navy-900 px-2 py-2 text-sm text-slate-200"
                                  [value]="draftPilotTeamCell(row.celluleId)"
                                  (change)="patchDraftPilotTeamCell(row.celluleId, selectVal($event))"
                                >
                                  @for (t of teamsForCell(row.celluleId); track t.id) {
                                    <option [value]="t.id">{{ t.name }}</option>
                                  }
                                </select>
                              }
                              <button
                                type="button"
                                (click)="addPilotRow(row.celluleId)"
                                [disabled]="saving() || !coachUserId(row.celluleId) || !draftPilotCell(row.celluleId)"
                                class="rounded-lg bg-slate-700 px-3 py-2 text-sm text-white disabled:opacity-50"
                              >
                                Ajouter pilote
                              </button>
                            </div>
                          </td>
                        </tr>
                      }
                    } @empty {
                      <tr>
                        <td colspan="7" class="px-4 py-10 text-center text-slate-500 text-sm">
                          Aucun service à afficher. Créez un service ou modifiez la recherche.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </app-prime-card>
          }
          @case ('structure') {
            <div
              class="w-full grid grid-cols-1 xl:grid-cols-3 gap-6 xl:gap-8 items-stretch xl:items-start"
            >
              <app-prime-card
                className="min-w-0 p-0 flex flex-col xl:min-h-[28rem] shadow-md shadow-black/20"
                title="Vue structure"
                description="Arbre hiérarchique et détail du nœud sélectionné."
                [hasAction]="false"
              >
                <div
                  class="-m-6 flex-1 max-h-[min(70vh,36rem)] overflow-y-auto overscroll-y-contain p-4 md:p-5 space-y-4 bg-navy-950/40"
                >
                  @for (dept of filteredDepartments(); track dept.id) {
                    <div
                      class="rounded-xl border border-navy-800/70 bg-navy-900/45 overflow-hidden shadow-sm shadow-black/10"
                    >
                      <button
                        type="button"
                        [class]="deptTreeRowClass(dept.id)"
                        (click)="toggleDept(dept.id); selectDepartment(dept)"
                      >
                        <span class="flex h-full items-center justify-center shrink-0">
                          <app-lucide-icon
                            [icon]="deptExpanded(dept.id) ? icons.chevDown : icons.chevRight"
                            className="w-4 h-4 text-slate-500"
                          />
                        </span>
                        <span class="min-w-0 flex items-center gap-2 font-semibold leading-snug text-slate-100">
                          <span
                            class="shrink-0 rounded px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide bg-sky-500/20 text-sky-100 ring-1 ring-sky-500/35"
                            >Pôle</span
                          >
                          <span class="truncate">{{ dept.name }}</span>
                        </span>
                        <span
                          class="min-w-0 text-xs leading-snug text-slate-400 text-right tabular-nums truncate"
                          [attr.title]="structureBadgeTitle(managerBadge(dept.id), 'Chef de projet')"
                          >{{ managerBadge(dept.id) || '—' }}</span
                        >
                      </button>
                      @if (deptExpanded(dept.id)) {
                        <div class="border-t border-navy-800/50 pl-3 ml-2 mr-1 space-y-2.5 pb-3 pt-1">
                          @for (pole of dept.poles; track pole.id) {
                            <div
                              class="rounded-lg border border-navy-800/55 bg-navy-950/55 overflow-hidden"
                            >
                              <button
                                type="button"
                                [class]="poleTreeRowClass(pole.id)"
                                (click)="togglePole(pole.id); selectPole(dept, pole)"
                              >
                                <span class="flex items-center justify-center shrink-0">
                                  <app-lucide-icon
                                    [icon]="poleExpanded(pole.id) ? icons.chevDown : icons.chevRight"
                                    className="w-3.5 h-3.5 text-slate-500"
                                  />
                                </span>
                                <span class="min-w-0 flex items-center gap-2 text-sm font-medium leading-snug text-slate-200">
                                  <span
                                    class="shrink-0 rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide bg-emerald-500/20 text-emerald-100 ring-1 ring-emerald-500/35"
                                    >Cell.</span
                                  >
                                  <span class="truncate">{{ pole.name }}</span>
                                </span>
                                <span
                                  class="min-w-0 text-xs leading-snug text-slate-400 text-right truncate"
                                  [attr.title]="structureBadgeTitle(supervisorBadge(pole.id), 'Superviseur')"
                                  >{{ supervisorBadge(pole.id) || '—' }}</span
                                >
                              </button>
                              @if (poleExpanded(pole.id)) {
                                <div class="border-t border-navy-800/45 pl-2.5 ml-1.5 space-y-1 pb-2.5 pt-0.5">
                                  @for (cell of pole.cells; track cell.id) {
                                    <button
                                      type="button"
                                      [class]="cellButtonClass(cell.id)"
                                      (click)="selectCellule(dept, pole, cell)"
                                    >
                                      <span class="w-3 shrink-0 block" aria-hidden="true"></span>
                                      <span class="min-w-0 flex items-center gap-2 truncate">
                                        <span
                                          class="shrink-0 rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide bg-violet-500/20 text-violet-100 ring-1 ring-violet-500/35"
                                          >Svc.</span
                                        >
                                        <span class="truncate">{{ cell.name }}</span>
                                      </span>
                                      <span
                                        class="min-w-0 text-xs text-slate-400 text-right truncate"
                                        [attr.title]="structureBadgeTitle(coachBadge(cell.id), 'Référent technique')"
                                        >{{ coachBadge(cell.id) || '—' }}</span
                                      >
                                    </button>
                                  }
                                </div>
                              }
                            </div>
                          }
                        </div>
                      }
                    </div>
                  }
                </div>
              </app-prime-card>

              <app-prime-card
                className="min-w-0 flex flex-col xl:min-h-[28rem] shadow-md shadow-black/20"
                title="Détail du nœud"
                description="Choisissez un employé puis enregistrez."
                [hasAction]="false"
              >
                <div class="flex min-h-[20rem] flex-1 flex-col -mx-1 bg-navy-950/25">
                  @if (selection(); as sel) {
                    <div class="flex w-full flex-1 flex-col space-y-6 pt-1">
                      <header class="space-y-1 border-b border-navy-800/80 pb-4">
                        <p class="text-xs font-semibold uppercase tracking-wider text-slate-500">
                          @switch (sel.kind) {
                            @case ('department') {
                              Pôle
                            }
                            @case ('pole') {
                              Cellule
                            }
                            @case ('cellule') {
                              Service
                            }
                          }
                        </p>
                        <h2 class="text-2xl font-semibold tracking-tight text-slate-50">{{ sel.name }}</h2>
                      </header>

                      @if (sel.kind === 'department') {
                        <div class="space-y-3">
                          <label class="text-sm font-medium text-slate-300 block">Chef de projet</label>
                          @if (draftEmployeeId()) {
                            <div
                              class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-navy-700 bg-navy-950/50 px-3 py-2.5"
                            >
                              <span class="text-sm text-slate-200">
                                <span class="text-slate-500">Sélection :</span>
                                <strong class="ml-1">{{ employeeLabel(draftEmployeeId()) }}</strong>
                              </span>
                              <button
                                type="button"
                                (click)="beginRepickDetailEmployee()"
                                class="text-xs font-medium text-indigo-400 hover:text-indigo-300"
                              >
                                Changer
                              </button>
                            </div>
                          }
                          <input
                            type="search"
                            class="w-full rounded-lg border border-navy-700 bg-navy-900 px-3 py-2.5 text-sm text-slate-200 placeholder:text-slate-500"
                            placeholder="Rechercher un employé (nom, rôle, e-mail)…"
                            [value]="structureDetailEmpSearch()"
                            (input)="setDetailEmpSearch($event)"
                          />
                          <ul
                            class="max-h-56 overflow-y-auto rounded-lg border border-navy-800 bg-navy-950/40 divide-y divide-navy-800"
                          >
                            @for (e of filteredDetailAssignables(); track e.id) {
                              <li>
                                <button
                                  type="button"
                                  (click)="pickDetailEmployee(e.id)"
                                  class="w-full flex items-center gap-3 px-3 py-2.5 text-left text-sm hover:bg-navy-800/60 transition-colors"
                                >
                                  <span
                                    class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-sky-500/20 text-xs font-semibold text-sky-100"
                                    >{{ employeeInitials(e) }}</span
                                  >
                                  <span class="min-w-0">
                                    <span class="block font-medium text-slate-100 truncate"
                                      >{{ e.firstName }} {{ e.lastName }}</span
                                    >
                                    <span class="block text-xs text-slate-500 truncate"
                                      >{{ e.role }} · {{ e.email }}</span
                                    >
                                  </span>
                                </button>
                              </li>
                            } @empty {
                              <li class="px-3 py-4 text-sm text-slate-500">Aucun résultat</li>
                            }
                          </ul>
                          <div class="flex flex-wrap gap-3 pt-2">
                            <button
                              type="button"
                              (click)="saveDepartmentManager(sel.id)"
                              [disabled]="saving() || !draftEmployeeId()"
                              class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50"
                            >
                              <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearDepartmentManager(sel.id)"
                              [disabled]="saving() || !managerUserId(sel.id)"
                              class="inline-flex items-center justify-center gap-2 rounded-lg border border-navy-600 px-4 py-2.5 text-sm text-slate-300 hover:bg-navy-800 disabled:opacity-50"
                            >
                              <app-lucide-icon [icon]="icons.trash" className="w-4 h-4" />
                              Retirer le chef de projet
                            </button>
                          </div>
                        </div>
                      }

                      @if (sel.kind === 'pole') {
                        <div class="space-y-3">
                          <label class="text-sm font-medium text-slate-300 block">Responsable (Superviseur)</label>
                          @if (draftEmployeeId()) {
                            <div
                              class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-navy-700 bg-navy-950/50 px-3 py-2.5"
                            >
                              <span class="text-sm text-slate-200">
                                <span class="text-slate-500">Sélection :</span>
                                <strong class="ml-1">{{ employeeLabel(draftEmployeeId()) }}</strong>
                              </span>
                              <button
                                type="button"
                                (click)="beginRepickDetailEmployee()"
                                class="text-xs font-medium text-indigo-400 hover:text-indigo-300"
                              >
                                Changer
                              </button>
                            </div>
                          }
                          <input
                            type="search"
                            class="w-full rounded-lg border border-navy-700 bg-navy-900 px-3 py-2.5 text-sm text-slate-200 placeholder:text-slate-500"
                            placeholder="Rechercher un employé (nom, rôle, e-mail)…"
                            [value]="structureDetailEmpSearch()"
                            (input)="setDetailEmpSearch($event)"
                          />
                          <ul
                            class="max-h-56 overflow-y-auto rounded-lg border border-navy-800 bg-navy-950/40 divide-y divide-navy-800"
                          >
                            @for (e of filteredDetailAssignables(); track e.id) {
                              <li>
                                <button
                                  type="button"
                                  (click)="pickDetailEmployee(e.id)"
                                  class="w-full flex items-center gap-3 px-3 py-2.5 text-left text-sm hover:bg-navy-800/60 transition-colors"
                                >
                                  <span
                                    class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-emerald-500/20 text-xs font-semibold text-emerald-100"
                                    >{{ employeeInitials(e) }}</span
                                  >
                                  <span class="min-w-0">
                                    <span class="block font-medium text-slate-100 truncate"
                                      >{{ e.firstName }} {{ e.lastName }}</span
                                    >
                                    <span class="block text-xs text-slate-500 truncate"
                                      >{{ e.role }} · {{ e.email }}</span
                                    >
                                  </span>
                                </button>
                              </li>
                            } @empty {
                              <li class="px-3 py-4 text-sm text-slate-500">Aucun résultat</li>
                            }
                          </ul>
                          <div class="flex flex-wrap gap-3 pt-2">
                            <button
                              type="button"
                              (click)="savePoleSupervisor(sel.id)"
                              [disabled]="saving() || !draftEmployeeId()"
                              class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50"
                            >
                              <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearPoleSupervisor(sel.id)"
                              [disabled]="saving() || !supervisorUserId(sel.id)"
                              class="inline-flex items-center justify-center gap-2 rounded-lg border border-navy-600 px-4 py-2.5 text-sm text-slate-300 hover:bg-navy-800 disabled:opacity-50"
                            >
                              <app-lucide-icon [icon]="icons.trash" className="w-4 h-4" />
                              Retirer le superviseur
                            </button>
                          </div>
                        </div>
                      }

                      @if (sel.kind === 'cellule') {
                        <div class="space-y-5 border-t border-navy-800 pt-5">
                          <div class="space-y-3">
                            <label class="text-sm font-medium text-slate-300 block">Référent technique</label>
                            @if (draftEmployeeId()) {
                              <div
                                class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-navy-700 bg-navy-950/50 px-3 py-2.5"
                              >
                                <span class="text-sm text-slate-200">
                                  <span class="text-slate-500">Sélection :</span>
                                  <strong class="ml-1">{{ employeeLabel(draftEmployeeId()) }}</strong>
                                </span>
                                <button
                                  type="button"
                                  (click)="beginRepickDetailEmployee()"
                                  class="text-xs font-medium text-indigo-400 hover:text-indigo-300"
                                >
                                  Changer
                                </button>
                              </div>
                            }
                            <input
                              type="search"
                              class="w-full rounded-lg border border-navy-700 bg-navy-900 px-3 py-2.5 text-sm text-slate-200 placeholder:text-slate-500"
                              placeholder="Rechercher un employé (nom, rôle, e-mail)…"
                              [value]="structureDetailEmpSearch()"
                              (input)="setDetailEmpSearch($event)"
                            />
                            <ul
                              class="max-h-48 overflow-y-auto rounded-lg border border-navy-800 bg-navy-950/40 divide-y divide-navy-800"
                            >
                              @for (e of filteredDetailAssignables(); track e.id) {
                                <li>
                                  <button
                                    type="button"
                                    (click)="pickDetailEmployee(e.id)"
                                    class="w-full flex items-center gap-3 px-3 py-2.5 text-left text-sm hover:bg-navy-800/60 transition-colors"
                                  >
                                    <span
                                      class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-violet-500/20 text-xs font-semibold text-violet-100"
                                      >{{ employeeInitials(e) }}</span
                                    >
                                    <span class="min-w-0">
                                      <span class="block font-medium text-slate-100 truncate"
                                        >{{ e.firstName }} {{ e.lastName }}</span
                                      >
                                      <span class="block text-xs text-slate-500 truncate"
                                        >{{ e.role }} · {{ e.email }}</span
                                      >
                                    </span>
                                  </button>
                                </li>
                              } @empty {
                                <li class="px-3 py-4 text-sm text-slate-500">Aucun résultat</li>
                              }
                            </ul>
                            <div class="flex flex-wrap gap-3">
                              <button
                                type="button"
                                (click)="saveCellCoach(sel.id)"
                                [disabled]="saving() || !draftEmployeeId()"
                                class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50"
                              >
                                <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                                Enregistrer le référent technique
                              </button>
                              <button
                                type="button"
                                (click)="clearCellCoach(sel.id)"
                                [disabled]="saving() || !coachUserId(sel.id)"
                                class="inline-flex items-center justify-center gap-2 rounded-lg border border-navy-600 px-4 py-2.5 text-sm text-slate-300 hover:bg-navy-800 disabled:opacity-50"
                              >
                                <app-lucide-icon [icon]="icons.trash" className="w-4 h-4" />
                                Retirer le référent technique
                              </button>
                            </div>
                          </div>

                          <div class="space-y-3">
                            <label class="text-sm font-medium text-slate-300 block">Pilotes</label>
                            @if (!coachUserId(sel.id)) {
                              <p class="text-sm text-amber-400/90">Affectez d’abord un référent technique.</p>
                            }
                            <ul
                              class="rounded-lg border border-navy-800 divide-y divide-navy-800 bg-navy-950/40 max-h-48 overflow-y-auto"
                            >
                              @for (p of pilotsInCell(sel.id); track p.id) {
                                <li class="flex items-center justify-between gap-2 px-3 py-2.5 text-sm text-slate-200">
                                  <span class="min-w-0 truncate">{{ p.firstName }} {{ p.lastName }}</span>
                                  <button
                                    type="button"
                                    (click)="removePilot(sel.id, p.id)"
                                    [disabled]="saving()"
                                    class="shrink-0 inline-flex items-center gap-1 text-xs text-red-400 hover:text-red-300 disabled:opacity-50"
                                  >
                                    <app-lucide-icon [icon]="icons.trash" className="w-3.5 h-3.5" />
                                    Retirer
                                  </button>
                                </li>
                              } @empty {
                                <li class="px-3 py-4 text-sm text-slate-500">Aucun pilote</li>
                              }
                            </ul>
                            <div class="space-y-3">
                              <label class="text-sm text-slate-400">Ajouter un pilote</label>
                              @if (draftPilotId()) {
                                <div
                                  class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-navy-700 bg-navy-950/50 px-3 py-2.5"
                                >
                                  <span class="text-sm text-slate-200">
                                    <span class="text-slate-500">Sélection :</span>
                                    <strong class="ml-1">{{ employeeLabel(draftPilotId()) }}</strong>
                                  </span>
                                  <button
                                    type="button"
                                    (click)="beginRepickPilotEmployee()"
                                    class="text-xs font-medium text-indigo-400 hover:text-indigo-300"
                                  >
                                    Changer
                                  </button>
                                </div>
                              }
                              <input
                                type="search"
                                class="w-full rounded-lg border border-navy-700 bg-navy-900 px-3 py-2.5 text-sm text-slate-200 placeholder:text-slate-500"
                                placeholder="Rechercher un employé…"
                                [value]="structurePilotEmpSearch()"
                                (input)="setPilotEmpSearch($event)"
                                [disabled]="!coachUserId(sel.id)"
                              />
                              <ul
                                class="max-h-40 overflow-y-auto rounded-lg border border-navy-800 bg-navy-950/40 divide-y divide-navy-800"
                              >
                                @for (e of filteredPilotAssignables(); track e.id) {
                                  <li>
                                    <button
                                      type="button"
                                      (click)="pickPilotEmployee(e.id)"
                                      [disabled]="!coachUserId(sel.id)"
                                      class="w-full flex items-center gap-3 px-3 py-2 text-left text-sm hover:bg-navy-800/60 disabled:opacity-40"
                                    >
                                      <span
                                        class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-600/40 text-[11px] font-semibold text-slate-100"
                                        >{{ employeeInitials(e) }}</span
                                      >
                                      <span class="min-w-0">
                                        <span class="block font-medium text-slate-100 truncate"
                                          >{{ e.firstName }} {{ e.lastName }}</span
                                        >
                                        <span class="block text-xs text-slate-500 truncate">{{ e.role }}</span>
                                      </span>
                                    </button>
                                  </li>
                                } @empty {
                                  <li class="px-3 py-3 text-sm text-slate-500">Aucun résultat</li>
                                }
                              </ul>
                              @if (teamsForCell(sel.id).length > 1) {
                                <select
                                  class="w-full rounded-lg border border-navy-700 bg-navy-900 px-3 py-2.5 text-sm text-slate-200"
                                  [value]="draftPilotTeamId()"
                                  (change)="draftPilotTeamId.set(selectVal($event))"
                                >
                                  @for (t of teamsForCell(sel.id); track t.id) {
                                    <option [value]="t.id">{{ t.name }}</option>
                                  }
                                </select>
                              }
                              <button
                                type="button"
                                (click)="addPilot(sel.id)"
                                [disabled]="saving() || !coachUserId(sel.id) || !draftPilotId()"
                                class="w-full inline-flex items-center justify-center gap-2 rounded-lg bg-slate-700 px-4 py-2.5 text-sm font-medium text-white hover:bg-slate-600 disabled:opacity-50"
                              >
                                <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                                Ajouter le pilote
                              </button>
                            </div>
                          </div>
                        </div>
                      }
                    </div>
                  } @else {
                    <div
                      class="flex flex-1 flex-col items-center justify-center rounded-xl border border-dashed border-slate-600/35 bg-navy-900/20 px-8 py-14 text-center min-h-[18rem]"
                    >
                      <p
                        class="text-sm sm:text-base text-slate-500 max-w-md leading-relaxed tracking-tight"
                      >
                        Sélectionnez un pôle, une cellule ou un service dans l’arbre pour afficher le formulaire
                        d’affectation.
                      </p>
                    </div>
                  }
                </div>
              </app-prime-card>

              <div class="min-w-0 space-y-4 xl:space-y-5 flex flex-col">
                <app-prime-card
                  className="p-0"
                  title="Indicateurs"
                  description="Périmètre du nœud sélectionné."
                  [hasAction]="false"
                >
                  <div class="space-y-4 -mt-1">
                    <p class="text-xs text-slate-500 flex items-center gap-2">
                      <app-lucide-icon [icon]="icons.building" className="w-3.5 h-3.5 shrink-0" />
                      <span class="truncate font-medium text-slate-300">{{ structureContextKpis().scopeTitle }}</span>
                    </p>
                    <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                      <div class="rounded-lg border border-navy-800 bg-navy-950/50 px-3 py-3">
                        <p class="text-[11px] uppercase tracking-wide text-slate-500">Effectif</p>
                        <p class="text-2xl font-semibold text-slate-50 tabular-nums">
                          {{ structureContextKpis().effectif }}
                        </p>
                        <p class="text-[11px] text-slate-500 mt-1">Employés rattachés</p>
                      </div>
                      <div class="rounded-lg border border-navy-800 bg-navy-950/50 px-3 py-3">
                        <p class="text-[11px] uppercase tracking-wide text-slate-500">Parité</p>
                        <p class="text-lg font-medium text-slate-200 leading-snug">{{ structureContextKpis().parite }}</p>
                        <p class="text-[11px] text-slate-500 mt-1">Non renseigné en base (V1)</p>
                      </div>
                      <div class="rounded-lg border border-navy-800 bg-navy-950/50 px-3 py-3">
                        <p class="text-[11px] uppercase tracking-wide text-slate-500">Structure</p>
                        <p class="text-sm font-medium text-amber-200/90 leading-snug">
                          {{ structureContextKpis().vacants }}
                        </p>
                        <p class="text-[11px] text-slate-500 mt-1">Indicateur rapide</p>
                      </div>
                    </div>
                  </div>
                </app-prime-card>

                <app-prime-card
                  className="p-0 flex flex-col max-h-[min(42vh,22rem)]"
                  title="Aperçu du périmètre"
                  description="Membres du périmètre sélectionné."
                  [hasAction]="false"
                >
                  <div class="-m-6 flex-1 min-h-0 overflow-y-auto p-4">
                    <ul class="space-y-2">
                      @for (m of structureContextMembers(); track m.id) {
                        <li
                          class="flex items-center gap-3 rounded-lg border border-navy-800/80 bg-navy-900/40 px-3 py-2"
                        >
                          @if (m.avatar) {
                            <img
                              [src]="m.avatar"
                              alt=""
                              class="h-9 w-9 shrink-0 rounded-full object-cover ring-1 ring-navy-700"
                            />
                          } @else {
                            <span
                              class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-indigo-500/25 text-xs font-semibold text-indigo-100"
                              >{{ employeeInitials(m) }}</span
                            >
                          }
                          <span class="min-w-0 flex-1">
                            <span class="block text-sm font-medium text-slate-100 truncate"
                              >{{ m.firstName }} {{ m.lastName }}</span
                            >
                            <span class="block text-xs text-slate-500 truncate">{{ m.role }}</span>
                          </span>
                        </li>
                      } @empty {
                        <li class="text-sm text-slate-500 py-4 text-center">Aucun employé dans ce périmètre.</li>
                      }
                    </ul>
                  </div>
                </app-prime-card>

                <app-prime-card
                  className="p-0 flex flex-col max-h-[min(36vh,18rem)]"
                  title="Journal d’activité"
                  description="Dernières mutations structurelles enregistrées."
                  [hasAction]="false"
                >
                  <div class="-m-6 flex-1 min-h-0 overflow-y-auto p-4">
                    <ul class="space-y-3">
                      @for (entry of structureActivityLog(); track $index) {
                        <li class="flex gap-3 text-sm">
                          <app-lucide-icon
                            [icon]="icons.activity"
                            className="w-4 h-4 shrink-0 text-slate-500 mt-0.5"
                          />
                          <div class="min-w-0">
                            <p class="text-xs text-slate-500">{{ entry.at }}</p>
                            <p class="text-slate-200 leading-snug">{{ entry.message }}</p>
                          </div>
                        </li>
                      } @empty {
                        <li class="flex gap-3 text-sm text-slate-500">
                          <app-lucide-icon [icon]="icons.activity" className="w-4 h-4 shrink-0" />
                          <p>Aucun événement pour l’instant. Les enregistrements apparaîtront ici.</p>
                        </li>
                      }
                    </ul>
                  </div>
                </app-prime-card>
              </div>
            </div>
          }
        }
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganisationManagementComponent implements OnInit {
  private readonly orgApi = inject(PrimeOrgApiService);
  private readonly role = inject(RoleService);

  readonly icons = {
    refresh: RefreshCw,
    chevRight: ChevronRight,
    chevDown: ChevronDown,
    check: Check,
    trash: Trash2,
    activity: Activity,
    building: Building2,
    plus: Plus,
  };

  readonly mainTabs: { id: OrgMainTab; label: string }[] = [
    { id: 'departments', label: 'Gestion des pôles' },
    { id: 'poles', label: 'Gestion des cellules' },
    { id: 'cellules', label: 'Gestion des services' },
    { id: 'structure', label: 'Vue structure' },
  ];

  readonly mainTab = signal<OrgMainTab>('departments');

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly data = signal<OrgAssignmentsOverview | null>(null);

  readonly expandedDeptIds = signal<Set<string>>(new Set());
  readonly expandedPoleIds = signal<Set<string>>(new Set());
  readonly selection = signal<OrgTreeSelection | null>(null);
  readonly search = signal('');

  readonly newDepartmentName = signal('');
  readonly newPoleDeptId = signal('');
  readonly newPoleName = signal('');
  readonly newCellDeptId = signal('');
  readonly newCellPoleId = signal('');
  readonly newCellName = signal('');

  readonly draftManagerByDept = signal<Record<string, string>>({});
  readonly draftSupervisorByPole = signal<Record<string, string>>({});
  readonly draftCoachByCell = signal<Record<string, string>>({});
  readonly draftPilotByCell = signal<Record<string, string>>({});
  readonly draftPilotTeamByCell = signal<Record<string, string>>({});

  readonly expandedCellPilotIds = signal<Set<string>>(new Set());

  readonly draftEmployeeId = signal('');
  readonly draftPilotId = signal('');
  readonly draftPilotTeamId = signal('');

  /** Recherche pour les listes filtrées (chef de projet / superviseur / référent technique) dans la vue structure. */
  readonly structureDetailEmpSearch = signal('');
  readonly structurePilotEmpSearch = signal('');
  readonly structureActivityLog = signal<StructureLogEntry[]>([]);

  ngOnInit(): void {
    this.role.preferRhForOrgScreen();
    this.load(false);
  }

  load(silent: boolean): void {
    this.error.set(null);
    if (!silent) this.loading.set(true);
    this.orgApi
      .loadOverview()
      .pipe(finalize(() => (!silent ? this.loading.set(false) : void 0)))
      .subscribe({
        next: (d) => {
          this.data.set(d);
          if (!silent) this.expandAllForDiscovery(d.departments);
          this.rebuildRowDraftsFromData(d);
          this.syncDraftFromSelection();
          this.ensureStructureCreateFormDefaults(d);
        },
        error: (err: unknown) => {
          this.data.set(null);
          this.error.set(httpErrMessage(err));
        },
      });
  }

  private rebuildRowDraftsFromData(d: OrgAssignmentsOverview): void {
    const mgr: Record<string, string> = {};
    const sup: Record<string, string> = {};
    const coach: Record<string, string> = {};
    const pilotPick: Record<string, string> = {};
    const pilotTeam: Record<string, string> = {};

    for (const dept of d.departments) {
      mgr[dept.id] =
        matchAssignmentUserId(d.managerEtage, [dept.id, ...dept.poles.map((p) => p.id)]) ?? '';
      for (const pole of dept.poles) {
        const cellIds = pole.cells.map((c) => c.id);
        const teamIds = pole.cells.flatMap((c) => (c.teams ?? []).map((t) => t.id));
        sup[pole.id] =
          matchAssignmentUserId(d.supervisorService, [pole.id, ...cellIds, ...teamIds]) ?? '';
        for (const cell of pole.cells) {
          const teamIdsForCell = (cell.teams ?? []).map((t) => t.id);
          coach[cell.id] =
            matchAssignmentUserId(d.coachSousService, [cell.id, ...teamIdsForCell]) ?? '';
          pilotPick[cell.id] = '';
          const teams = cell.teams ?? [];
          pilotTeam[cell.id] = teams[0]?.id ?? '';
        }
      }
    }

    this.draftManagerByDept.set(mgr);
    this.draftSupervisorByPole.set(sup);
    this.draftCoachByCell.set(coach);
    this.draftPilotByCell.set(pilotPick);
    this.draftPilotTeamByCell.set(pilotTeam);
  }

  private ensureStructureCreateFormDefaults(d: OrgAssignmentsOverview): void {
    const depts = d.departments;
    if (depts.length === 0) {
      this.newPoleDeptId.set('');
      this.newCellDeptId.set('');
      this.newCellPoleId.set('');
      return;
    }
    if (!depts.some((x) => x.id === this.newPoleDeptId())) {
      this.newPoleDeptId.set(depts[0].id);
    }
    if (!depts.some((x) => x.id === this.newCellDeptId())) {
      this.newCellDeptId.set(depts[0].id);
    }
    const poles = depts.find((x) => x.id === this.newCellDeptId())?.poles ?? [];
    if (!poles.some((p) => p.id === this.newCellPoleId())) {
      this.newCellPoleId.set(poles[0]?.id ?? '');
    }
  }

  patchNewCellDept(deptId: string): void {
    this.newCellDeptId.set(deptId);
    const poles = this.data()?.departments?.find((d) => d.id === deptId)?.poles ?? [];
    this.newCellPoleId.set(poles[0]?.id ?? '');
  }

  submitNewDepartment(): void {
    const name = this.newDepartmentName().trim();
    if (!name) return;
    this.runMutation(
      this.orgApi.createStructureDepartment(name),
      () => this.newDepartmentName.set(''),
      'Pôle ajouté',
    );
  }

  submitNewPole(): void {
    const deptId = this.newPoleDeptId().trim();
    const name = this.newPoleName().trim();
    if (!deptId || !name) return;
    this.runMutation(this.orgApi.createStructurePole(deptId, name), () => this.newPoleName.set(''), 'Cellule ajoutée');
  }

  submitNewCellule(): void {
    const poleId = this.newCellPoleId().trim();
    const name = this.newCellName().trim();
    if (!poleId || !name) return;
    this.runMutation(
      this.orgApi.createStructureCellule(poleId, name),
      () => this.newCellName.set(''),
      'Service ajouté',
    );
  }

  private syncDraftFromSelection(): void {
    const sel = this.selection();
    if (!sel) return;
    if (sel.kind === 'department') {
      this.draftEmployeeId.set(this.managerUserId(sel.id) ?? '');
      return;
    }
    if (sel.kind === 'pole') {
      this.draftEmployeeId.set(this.supervisorUserId(sel.id) ?? '');
      return;
    }
    this.draftEmployeeId.set(this.coachUserId(sel.id) ?? '');
    const teams = this.teamsForCell(sel.id);
    this.draftPilotTeamId.set(teams[0]?.id ?? '');
  }

  private expandAllForDiscovery(departments: Department[]): void {
    const ds = new Set<string>();
    const ps = new Set<string>();
    for (const d of departments) {
      ds.add(d.id);
      for (const p of d.poles) {
        ps.add(p.id);
      }
    }
    this.expandedDeptIds.set(ds);
    this.expandedPoleIds.set(ps);
  }

  allPolesFlat = computed((): FlatPoleRow[] => {
    const out: FlatPoleRow[] = [];
    for (const d of this.data()?.departments ?? []) {
      for (const p of d.poles) {
        out.push({
          departmentId: d.id,
          departmentName: d.name,
          poleId: p.id,
          poleName: p.name,
        });
      }
    }
    return out;
  });

  allCellulesFlat = computed((): FlatCelluleRow[] => {
    const out: FlatCelluleRow[] = [];
    for (const d of this.data()?.departments ?? []) {
      for (const p of d.poles) {
        for (const c of p.cells) {
          out.push({
            departmentId: d.id,
            departmentName: d.name,
            poleId: p.id,
            poleName: p.name,
            celluleId: c.id,
            celluleName: c.name,
          });
        }
      }
    }
    return out;
  });

  private matchesSearch(q: string, ...parts: string[]): boolean {
    if (!q) return true;
    return parts.some((p) => p.toLowerCase().includes(q));
  }

  filteredDepartmentsForTable = computed(() => {
    const q = this.search().trim().toLowerCase();
    const depts = this.data()?.departments ?? [];
    return depts.filter((d) => this.matchesSearch(q, d.name));
  });

  filteredPolesForTable = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.allPolesFlat().filter((r) => this.matchesSearch(q, r.departmentName, r.poleName));
  });

  filteredCellulesForTable = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this
      .allCellulesFlat()
      .filter((r) => this.matchesSearch(q, r.departmentName, r.poleName, r.celluleName));
  });

  filteredDepartments = computed(() => {
    const q = this.search().trim().toLowerCase();
    const depts = this.data()?.departments ?? [];
    if (!q) return depts;
    return depts.filter((d) => {
      if (d.name.toLowerCase().includes(q)) return true;
      return d.poles.some(
        (p) =>
          p.name.toLowerCase().includes(q) || p.cells.some((c) => c.name.toLowerCase().includes(q)),
      );
    });
  });

  readonly polesForNewCellForm = computed((): Pole[] => {
    const deptId = this.newCellDeptId();
    return this.data()?.departments?.find((d) => d.id === deptId)?.poles ?? [];
  });

  assignableEmployees = computed((): Employee[] => {
    const list = this.data()?.employees ?? [];
    return employeesForOrgAssignmentSelect(list);
  });

  readonly filteredDetailAssignables = computed((): Employee[] => {
    const q = this.structureDetailEmpSearch().trim().toLowerCase();
    const list = this.assignableEmployees();
    if (!q) return list;
    return list.filter(
      (e) =>
        `${e.firstName} ${e.lastName}`.toLowerCase().includes(q) ||
        e.role.toLowerCase().includes(q) ||
        (e.email ?? '').toLowerCase().includes(q),
    );
  });

  readonly filteredPilotAssignables = computed((): Employee[] => {
    const q = this.structurePilotEmpSearch().trim().toLowerCase();
    const list = this.assignableEmployees();
    if (!q) return list;
    return list.filter(
      (e) =>
        `${e.firstName} ${e.lastName}`.toLowerCase().includes(q) ||
        e.role.toLowerCase().includes(q) ||
        (e.email ?? '').toLowerCase().includes(q),
    );
  });

  /** Membres rattachés au nœud sélectionné (vue structure, panneau droit). */
  readonly structureContextMembers = computed((): Employee[] => {
    const sel = this.selection();
    const employees = this.data()?.employees ?? [];
    let scope: Employee[];
    if (!sel) scope = employees;
    else if (sel.kind === 'department') scope = employees.filter((e) => e.departementId === sel.id);
    else if (sel.kind === 'pole') scope = employees.filter((e) => e.poleId === sel.id);
    else scope = employees.filter((e) => e.celluleId === sel.id);
    return [...scope].sort((a, b) =>
      `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`, 'fr'),
    );
  });

  readonly structureContextKpis = computed(() => {
    const sel = this.selection();
    const data = this.data();
    const employees = data?.employees ?? [];
    const depts = data?.departments ?? [];

    if (!sel) {
      const sansMgr = depts.filter((d) => !this.managerUserId(d.id)).length;
      return {
        scopeTitle: 'Organisation',
        effectif: employees.length,
        parite: 'Non disponible',
        vacants: `${sansMgr} pôle(s) sans chef de projet`,
      };
    }
    if (sel.kind === 'department') {
      const d = depts.find((x) => x.id === sel.id);
      const sansSup = (d?.poles ?? []).filter((p) => !this.supervisorUserId(p.id)).length;
      const effectif = employees.filter((e) => e.departementId === sel.id).length;
      return {
        scopeTitle: sel.name,
        effectif,
        parite: 'Non disponible',
        vacants: `${sansSup} cellule(s) sans superviseur`,
      };
    }
    if (sel.kind === 'pole') {
      let cells: Cellule[] = [];
      for (const d of depts) {
        const p = d.poles.find((x) => x.id === sel.id);
        if (p) {
          cells = p.cells;
          break;
        }
      }
      const sansCoach = cells.filter((c) => !this.coachUserId(c.id)).length;
      const effectif = employees.filter((e) => e.poleId === sel.id).length;
      return {
        scopeTitle: sel.name,
        effectif,
        parite: 'Non disponible',
        vacants: `${sansCoach} service(s) sans référent technique`,
      };
    }
    const teams = this.teamsForCell(sel.id);
    const nPilotes = this.pilotsInCell(sel.id).length;
    const effectif = employees.filter((e) => e.celluleId === sel.id).length;
    const vacants =
      teams.length > 0
        ? `${Math.max(0, teams.length - nPilotes)} poste(s) pilote à compléter (approx.)`
        : '—';
    return {
      scopeTitle: sel.name,
      effectif,
      parite: 'Non disponible',
      vacants,
    };
  });

  draftManagerDept(id: string): string {
    return this.draftManagerByDept()[id] ?? '';
  }

  draftSupervisorPole(id: string): string {
    return this.draftSupervisorByPole()[id] ?? '';
  }

  draftCoachCell(id: string): string {
    return this.draftCoachByCell()[id] ?? '';
  }

  draftPilotCell(id: string): string {
    return this.draftPilotByCell()[id] ?? '';
  }

  draftPilotTeamCell(id: string): string {
    return this.draftPilotTeamByCell()[id] ?? '';
  }

  readonly employeeOptionLabel = employeeSelectOptionLabel;

  employeesForManagerRow(deptId: string): Employee[] {
    const emps = this.data()?.employees ?? [];
    return employeesForOrgAssignmentSelect(
      emps,
      this.draftManagerDept(deptId) || this.managerUserId(deptId),
    );
  }

  employeesForSupervisorRow(poleId: string): Employee[] {
    const emps = this.data()?.employees ?? [];
    return employeesForOrgAssignmentSelect(
      emps,
      this.draftSupervisorPole(poleId) || this.supervisorUserId(poleId),
    );
  }

  employeesForCoachRow(cellId: string): Employee[] {
    const emps = this.data()?.employees ?? [];
    return employeesForOrgAssignmentSelect(
      emps,
      this.draftCoachCell(cellId) || this.coachUserId(cellId),
    );
  }

  selectManagerValue(deptId: string): string {
    const opts = this.employeesForManagerRow(deptId).map((e) => e.id);
    return selectValueOrEmpty(this.draftManagerDept(deptId), opts);
  }

  selectSupervisorValue(poleId: string): string {
    const opts = this.employeesForSupervisorRow(poleId).map((e) => e.id);
    return selectValueOrEmpty(this.draftSupervisorPole(poleId), opts);
  }

  selectCoachValue(cellId: string): string {
    const opts = this.employeesForCoachRow(cellId).map((e) => e.id);
    return selectValueOrEmpty(this.draftCoachCell(cellId), opts);
  }

  employeesForPilotRow(cellId: string): Employee[] {
    const emps = this.data()?.employees ?? [];
    return employeesForOrgAssignmentSelect(emps, this.draftPilotCell(cellId));
  }

  selectPilotValue(cellId: string): string {
    const opts = this.employeesForPilotRow(cellId).map((e) => e.id);
    return selectValueOrEmpty(this.draftPilotCell(cellId), opts);
  }

  patchDraftManager(deptId: string, value: string): void {
    this.draftManagerByDept.update((m) => ({ ...m, [deptId]: value }));
  }

  patchDraftSupervisor(poleId: string, value: string): void {
    this.draftSupervisorByPole.update((m) => ({ ...m, [poleId]: value }));
  }

  patchDraftCoach(cellId: string, value: string): void {
    this.draftCoachByCell.update((m) => ({ ...m, [cellId]: value }));
  }

  patchDraftPilotCell(cellId: string, value: string): void {
    this.draftPilotByCell.update((m) => ({ ...m, [cellId]: value }));
  }

  patchDraftPilotTeamCell(cellId: string, value: string): void {
    this.draftPilotTeamByCell.update((m) => ({ ...m, [cellId]: value }));
  }

  cellPilotsExpanded(cellId: string): boolean {
    return this.expandedCellPilotIds().has(cellId);
  }

  toggleCellPilots(cellId: string): void {
    const s = new Set(this.expandedCellPilotIds());
    if (s.has(cellId)) s.delete(cellId);
    else s.add(cellId);
    this.expandedCellPilotIds.set(s);
  }

  deptExpanded(id: string): boolean {
    return this.expandedDeptIds().has(id);
  }

  poleExpanded(id: string): boolean {
    return this.expandedPoleIds().has(id);
  }

  toggleDept(id: string): void {
    const s = new Set(this.expandedDeptIds());
    if (s.has(id)) s.delete(id);
    else s.add(id);
    this.expandedDeptIds.set(s);
  }

  deptTreeRowClass(deptId: string): string {
    const base =
      'w-full grid grid-cols-[1.25rem_minmax(0,1fr)_minmax(0,9rem)] gap-x-3 gap-y-0.5 items-center px-3 py-3 text-left text-slate-100 hover:bg-navy-800/50 rounded-t-lg transition-colors';
    const sel = this.selection();
    if (sel?.kind === 'department' && sel.id === deptId) {
      return `${base} ring-2 ring-inset ring-indigo-500/45 bg-indigo-950/30`;
    }
    return base;
  }

  poleTreeRowClass(poleId: string): string {
    const base =
      'w-full grid grid-cols-[1.25rem_minmax(0,1fr)_minmax(0,8.5rem)] gap-x-2 gap-y-0.5 items-center px-2 py-2.5 text-left text-slate-200 hover:bg-navy-800/45 transition-colors';
    const sel = this.selection();
    if (sel?.kind === 'pole' && sel.id === poleId) {
      return `${base} ring-2 ring-inset ring-indigo-500/45 bg-indigo-950/25`;
    }
    return base;
  }

  cellButtonClass(cellId: string): string {
    const base =
      'grid grid-cols-[0.75rem_minmax(0,1fr)_minmax(0,6.5rem)] gap-x-2 items-center w-full px-2 py-2.5 rounded-md text-left text-sm text-slate-300 hover:bg-navy-800/40 transition-colors';
    const sel = this.selection();
    if (sel?.kind === 'cellule' && sel.id === cellId) {
      return `${base} ring-2 ring-inset ring-indigo-500/50 bg-indigo-950/35 text-slate-100`;
    }
    return base;
  }

  /** Tooltip sur la colonne « responsable » de l’arbre (nom complet si tronqué). */
  structureBadgeTitle(displayValue: string, roleLabel: string): string {
    const v = (displayValue || '').trim();
    if (!v || v === '—') return `Aucun ${roleLabel.toLowerCase()} affecté`;
    return `${roleLabel} : ${v}`;
  }

  togglePole(id: string): void {
    const s = new Set(this.expandedPoleIds());
    if (s.has(id)) s.delete(id);
    else s.add(id);
    this.expandedPoleIds.set(s);
  }

  selectDepartment(d: Department): void {
    this.selection.set({ kind: 'department', id: d.id, name: d.name });
    this.draftEmployeeId.set(this.managerUserId(d.id) ?? '');
    this.draftPilotId.set('');
    this.draftPilotTeamId.set('');
    this.structureDetailEmpSearch.set('');
    this.structurePilotEmpSearch.set('');
  }

  selectPole(d: Department, p: Pole): void {
    this.selection.set({
      kind: 'pole',
      id: p.id,
      name: p.name,
      departmentId: d.id,
    });
    this.draftEmployeeId.set(this.supervisorUserId(p.id) ?? '');
    this.draftPilotId.set('');
    this.draftPilotTeamId.set('');
    this.structureDetailEmpSearch.set('');
    this.structurePilotEmpSearch.set('');
  }

  selectCellule(d: Department, p: Pole, c: Cellule): void {
    this.selection.set({
      kind: 'cellule',
      id: c.id,
      name: c.name,
      poleId: p.id,
      departmentId: d.id,
    });
    this.draftEmployeeId.set(this.coachUserId(c.id) ?? '');
    const teams = c.teams ?? [];
    this.draftPilotId.set('');
    this.draftPilotTeamId.set(teams[0]?.id ?? '');
    this.structureDetailEmpSearch.set('');
    this.structurePilotEmpSearch.set('');
  }

  employeeInitials(e: Employee): string {
    const a = `${e.firstName?.[0] ?? ''}${e.lastName?.[0] ?? ''}`.trim();
    return a.toUpperCase() || '?';
  }

  setDetailEmpSearch(ev: Event): void {
    this.structureDetailEmpSearch.set((ev.target as HTMLInputElement).value);
  }

  setPilotEmpSearch(ev: Event): void {
    this.structurePilotEmpSearch.set((ev.target as HTMLInputElement).value);
  }

  pickDetailEmployee(id: string): void {
    this.draftEmployeeId.set(id);
    this.structureDetailEmpSearch.set('');
  }

  beginRepickDetailEmployee(): void {
    this.draftEmployeeId.set('');
  }

  pickPilotEmployee(id: string): void {
    this.draftPilotId.set(id);
    this.structurePilotEmpSearch.set('');
  }

  beginRepickPilotEmployee(): void {
    this.draftPilotId.set('');
  }

  private pushStructureLog(message: string): void {
    const at = new Date().toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
    this.structureActivityLog.update((list) => [{ at, message }, ...list].slice(0, 30));
  }

  managerUserId(deptId: string): string | undefined {
    const d = this.data();
    if (!d) return undefined;
    const dept = d.departments.find((x) => x.id === deptId);
    if (!dept) return undefined;
    return matchAssignmentUserId(d.managerEtage, [dept.id, ...dept.poles.map((p) => p.id)]);
  }

  supervisorUserId(poleId: string): string | undefined {
    const d = this.data();
    if (!d) return undefined;
    for (const dept of d.departments) {
      const pole = dept.poles.find((p) => p.id === poleId);
      if (pole) {
        const cellIds = pole.cells.map((c) => c.id);
        const teamIds = pole.cells.flatMap((c) => (c.teams ?? []).map((t) => t.id));
        return matchAssignmentUserId(d.supervisorService, [pole.id, ...cellIds, ...teamIds]);
      }
    }
    return matchAssignmentUserId(d.supervisorService, [poleId]);
  }

  coachUserId(cellId: string): string | undefined {
    const d = this.data();
    if (!d) return undefined;
    for (const dept of d.departments) {
      for (const pole of dept.poles) {
        const cell = pole.cells.find((c) => c.id === cellId);
        if (cell) {
          const teamIds = (cell.teams ?? []).map((t) => t.id);
          return matchAssignmentUserId(d.coachSousService, [cell.id, ...teamIds]);
        }
      }
    }
    return matchAssignmentUserId(d.coachSousService, [cellId]);
  }

  employeeLabel(id: string): string {
    const e = this.data()?.employees.find((x) => x.id === id);
    return e ? employeeSelectOptionLabel(e) : id;
  }

  managerLabel(deptId: string): string {
    const uid = this.managerUserId(deptId);
    return uid ? this.employeeLabel(uid) : '—';
  }

  supervisorLabel(poleId: string): string {
    const uid = this.supervisorUserId(poleId);
    return uid ? this.employeeLabel(uid) : '—';
  }

  coachLabel(cellId: string): string {
    const uid = this.coachUserId(cellId);
    return uid ? this.employeeLabel(uid) : '—';
  }

  managerBadge(deptId: string): string {
    const uid = this.managerUserId(deptId);
    return uid ? this.employeeLabel(uid) : '';
  }

  supervisorBadge(poleId: string): string {
    const uid = this.supervisorUserId(poleId);
    return uid ? this.employeeLabel(uid) : '';
  }

  coachBadge(cellId: string): string {
    const uid = this.coachUserId(cellId);
    return uid ? this.employeeLabel(uid) : '';
  }

  pilotsInCell(cellId: string): Employee[] {
    /** Les lignes « services » utilisent l’id du service feuille (ex. c1), pas l’id cellule (p1). */
    return (this.data()?.employees ?? []).filter(
      (e) =>
        e.role === 'Pilote' &&
        (e.serviceId === cellId || (e.serviceId === '' && e.celluleId === cellId)),
    );
  }

  teamsForCell(cellId: string): Team[] {
    const depts = this.data()?.departments ?? [];
    for (const d of depts) {
      for (const p of d.poles) {
        const c = p.cells.find((x) => x.id === cellId);
        if (c) return c.teams ?? [];
      }
    }
    return [];
  }

  selectVal(ev: Event): string {
    const t = ev.target as HTMLSelectElement;
    return t?.value ?? '';
  }

  saveDepartmentManagerRow(departmentId: string): void {
    const id = this.draftManagerByDept()[departmentId];
    if (!id) return;
    this.runMutation(
      this.orgApi.setStructureManager(departmentId, id),
      undefined,
      'Chef de projet enregistré (liste pôles)',
    );
  }

  clearDepartmentManagerRow(departmentId: string): void {
    this.runMutation(
      this.orgApi.clearStructureManager(departmentId),
      undefined,
      'Chef de projet retiré (liste pôles)',
    );
  }

  savePoleSupervisorRow(poleId: string): void {
    const id = this.draftSupervisorByPole()[poleId];
    if (!id) return;
    this.runMutation(
      this.orgApi.setStructureSupervisor(poleId, id),
      undefined,
      'Superviseur enregistré (liste cellules)',
    );
  }

  clearPoleSupervisorRow(poleId: string): void {
    this.runMutation(
      this.orgApi.clearStructureSupervisor(poleId),
      undefined,
      'Superviseur retiré (liste cellules)',
    );
  }

  saveCellCoachRow(celluleId: string): void {
    const id = this.draftCoachByCell()[celluleId];
    if (!id) return;
    this.runMutation(
      this.orgApi.setStructureCoach(celluleId, id),
      undefined,
      'Référent technique enregistré (liste services)',
    );
  }

  clearCellCoachRow(celluleId: string): void {
    this.runMutation(
      this.orgApi.clearStructureCoach(celluleId),
      undefined,
      'Référent technique retiré (liste services)',
    );
  }

  addPilotRow(celluleId: string): void {
    const emp = this.draftPilotByCell()[celluleId];
    if (!emp) return;
    const teams = this.teamsForCell(celluleId);
    const teamId =
      teams.length > 1
        ? this.draftPilotTeamByCell()[celluleId] || teams[0]?.id || undefined
        : undefined;
    this.runMutation(
      this.orgApi.addStructurePilot(celluleId, emp, teamId),
      undefined,
      'Pilote ajouté (liste services)',
    );
  }

  saveDepartmentManager(departmentId: string): void {
    const id = this.draftEmployeeId();
    if (!id) return;
    this.runMutation(
      this.orgApi.setStructureManager(departmentId, id),
      undefined,
      'Chef de projet mis à jour (vue structure)',
    );
  }

  clearDepartmentManager(departmentId: string): void {
    this.runMutation(
      this.orgApi.clearStructureManager(departmentId),
      undefined,
      'Chef de projet retiré (vue structure)',
    );
  }

  savePoleSupervisor(poleId: string): void {
    const id = this.draftEmployeeId();
    if (!id) return;
    this.runMutation(
      this.orgApi.setStructureSupervisor(poleId, id),
      undefined,
      'Superviseur mis à jour (vue structure)',
    );
  }

  clearPoleSupervisor(poleId: string): void {
    this.runMutation(
      this.orgApi.clearStructureSupervisor(poleId),
      undefined,
      'Superviseur retiré (vue structure)',
    );
  }

  saveCellCoach(celluleId: string): void {
    const id = this.draftEmployeeId();
    if (!id) return;
    this.runMutation(
      this.orgApi.setStructureCoach(celluleId, id),
      undefined,
      'Référent technique mis à jour (vue structure)',
    );
  }

  clearCellCoach(celluleId: string): void {
    this.runMutation(
      this.orgApi.clearStructureCoach(celluleId),
      undefined,
      'Référent technique retiré (vue structure)',
    );
  }

  addPilot(celluleId: string): void {
    const emp = this.draftPilotId();
    if (!emp) return;
    const teams = this.teamsForCell(celluleId);
    const teamId =
      teams.length > 1 ? this.draftPilotTeamId() || teams[0]?.id || undefined : undefined;
    this.runMutation(
      this.orgApi.addStructurePilot(celluleId, emp, teamId),
      undefined,
      'Pilote ajouté (vue structure)',
    );
  }

  removePilot(celluleId: string, employeeId: string): void {
    this.runMutation(
      this.orgApi.removeStructurePilot(celluleId, employeeId),
      undefined,
      'Pilote retiré',
    );
  }

  private runMutation(obs: Observable<unknown>, onOk?: () => void, logMessage?: string): void {
    this.error.set(null);
    this.saving.set(true);
    obs.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        onOk?.();
        if (logMessage) this.pushStructureLog(logMessage);
        this.draftPilotId.set('');
        this.load(true);
      },
      error: (err: unknown) => this.error.set(httpErrMessage(err)),
    });
  }
}
