import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
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
import { KyntusSelectSyncDirective } from '@/shared/directives/kyntus-select-sync.directive';
import { PrimeCardComponent } from '../components/prime-card.component';
import {
  PrimeOrgApiService,
  type OrgAssignmentsOverview,
  type StructuralRoleAssignmentResult,
} from '../services/prime-org-api.service';
import type { Employee, Team } from '../models';
import {
  employeeSelectOptionLabel,
  employeesForOrgAssignmentSelect,
  reconcileSelectModel,
} from '../lib/prime-select-options';
import { RoleService } from '../state/role.service';
import { parseOrganisationRhTab, type OrganisationRhTab } from '../../../core/navigation/organisation-nav';
import {
  ORG_DUPLICATE_CELLULE_MSG,
  ORG_DUPLICATE_POLE_MSG,
  ORG_DUPLICATE_SERVICE_MSG,
  orgNamesEqual,
} from '../lib/org-name-uniqueness';
import {
  buildCrossRoleOverwriteMessage,
  buildStructureOverwriteMessage,
  employeeDisplayName,
  findEmployeeStructuralRole,
  findStructureIncumbent,
  shouldConfirmOverwrite,
} from '../../../core/org/org-structure-incumbent.util';
import { KyntusConfirmService } from '../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import type { OperationalDepartmentNode, OrgCelluleNode, OrgPoleNode } from '../models/org-tree.types';

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
  | { kind: 'metierDepartment'; id: string; name: string; code: string }
  | { kind: 'pole'; id: string; name: string; metierDepartmentId: string }
  | { kind: 'cellule'; id: string; name: string; poleId: string; metierDepartmentId: string }
  | { kind: 'service'; id: string; name: string; celluleId: string; poleId: string; metierDepartmentId: string };

export type FlatPoleRow = {
  metierDepartmentId: string;
  metierDepartmentName: string;
  poleId: string;
  poleName: string;
  unassigned: boolean;
};

export type FlatCelluleRow = FlatPoleRow & {
  celluleId: string;
  celluleName: string;
  /** Nom de la cellule parente (onglet Services uniquement). */
  parentCelluleName?: string;
};

export type OrgMainTab = OrganisationRhTab;

export type StructureLogEntry = { at: string; message: string };

function httpErrMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as
      | { error?: string; title?: string; detail?: string; message?: string }
      | string
      | null;
    if (body && typeof body === 'object') {
      if (typeof body.error === 'string' && body.error.trim()) return body.error;
      if (typeof body.detail === 'string' && body.detail.trim()) return body.detail;
      if (typeof body.title === 'string' && body.title.trim()) return body.title;
      if (typeof body.message === 'string' && body.message.trim()) return body.message;
    }
    if (typeof body === 'string' && body.trim()) return body.trim();
    if (err.status === 0) return 'Impossible de joindre le serveur. Vérifiez votre connexion.';
    if (err.status >= 500) {
      return `Erreur serveur (${err.status}). Réessayez ou contactez l’administrateur.`;
    }
    return `Erreur HTTP ${err.status}. Réessayez ultérieurement.`;
  }
  return err instanceof Error ? err.message : 'Erreur inconnue';
}

@Component({
  selector: 'app-organisation-management',
  standalone: true,
  imports: [
    LucideIconComponent,
    PrimeCardComponent,
    KyntusSelectSyncDirective,
  ],
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
                <span class="font-medium text-slate-300">Départements opérationnels</span> — managers métier
                (interface Prime classique) distincts des
                <span class="font-medium text-slate-300">chefs de projet</span> par pôle.
              </p>
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
              (click)="selectMainTab(t.id)"
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
          @case ('metier-departments') {
            <app-prime-card
              className="p-0"
              title="Départements opérationnels"
              description="Manager métier (interface Prime classique). Distinct du chef de projet, rattaché à chaque pôle."
            >
              <div
                class="px-4 py-3 sm:px-6 border-b border-navy-800 bg-navy-950/40 flex flex-col sm:flex-row sm:flex-wrap gap-3 sm:items-end"
              >
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[12rem] flex-1 max-w-md">
                  <span>Nom du département</span>
                  <input
                    type="text"
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 placeholder:text-slate-500"
                    placeholder="Ex. Opérations terrain"
                    [value]="newMetierDeptName()"
                    (input)="newMetierDeptName.set($any($event.target).value)"
                  />
                </label>
                <button
                  type="button"
                  (click)="submitNewMetierDepartment()"
                  [disabled]="saving() || !newMetierDeptName().trim() || !!newMetierDeptNameConflict()"
                  class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50 shrink-0"
                >
                  <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                  Créer
                </button>
              </div>
              @if (newMetierDeptNameConflict(); as msg) {
                <p class="px-4 sm:px-6 pb-2 text-xs text-amber-400">{{ msg }}</p>
              }
              <p class="px-4 sm:px-6 py-2 text-xs text-slate-400 border-b border-navy-800">
                {{ filteredMetierDepartmentsForTable().length }} département(s) affiché(s)
              </p>
              <div class="overflow-x-auto">
                <table class="prime-table prime-table--dense w-full text-sm text-left">
                  <thead>
                    <tr>
                      <th class="font-medium">Département</th>
                      <th class="font-medium">Manager actuel</th>
                      <th class="font-medium min-w-[220px]">Nouveau manager</th>
                      <th class="font-medium w-40 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (d of filteredMetierDepartmentsForTable(); track d.id) {
                      <tr>
                        <td class="px-4 py-2.5">
                          <span class="prime-cell-strong">{{ d.code }} — {{ d.name }}</span>
                        </td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ metierManagerLabel(d.id) }}</span></td>
                        <td class="px-4 py-2.5">
                          <select
                            class="w-full min-w-[12rem] rounded-lg border border-navy-700 bg-navy-950 px-2 py-1.5 text-slate-200 text-sm"
                            [kyntusSelectSync]="draftMetierManager(d.id)"
                            (kyntusSelectSyncChange)="patchDraftMetierManager(d.id, $event)"
                          >
                            <option value="">— Sélectionner —</option>
                            @for (e of assignableEmployees(); track e.id) {
                              <option [value]="e.id">{{ employeeOptionLabel(e) }}</option>
                            }
                          </select>
                        </td>
                        <td class="px-4 py-2.5 text-right">
                          <div class="inline-flex flex-wrap justify-end gap-2">
                            <button
                              type="button"
                              (click)="saveMetierManagerRow(d.id)"
                              [disabled]="saving() || !draftMetierManager(d.id)"
                              class="rounded-md bg-indigo-600 px-2.5 py-1.5 text-xs font-medium text-white disabled:opacity-50"
                            >
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearMetierManagerRow(d.id)"
                              [disabled]="saving() || !d.managerEmployeeId"
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
                          Aucun département. Créez-en un avant d’ajouter des pôles.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </app-prime-card>
          }
          @case ('departments') {
            <app-prime-card className="p-0" title="Pôles" description="Un chef de projet par pôle. Parent obligatoire : département métier.">
              <div
                class="px-4 py-3 sm:px-6 border-b border-navy-800 bg-navy-950/40 flex flex-col sm:flex-row sm:flex-wrap gap-3 sm:items-end"
              >
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Département métier</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [kyntusSelectSync]="newPoleBusinessDeptId()"
                    (kyntusSelectSyncChange)="newPoleBusinessDeptId.set($event)"
                  >
                    @for (d of data()?.operationalDepartments ?? []; track d.id) {
                      <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
                    }
                  </select>
                </label>
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
                  [disabled]="saving() || !newPoleBusinessDeptId() || !newDepartmentName().trim() || !!newDepartmentNameConflict()"
                  class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50 shrink-0"
                >
                  <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                  Créer
                </button>
              </div>
              @if ((data()?.operationalDepartments?.length ?? 0) === 0) {
                <p class="px-4 sm:px-6 py-2 text-xs text-amber-400/90">
                  Créez d’abord un département opérationnel (onglet Départements).
                </p>
              }
              @if (newDepartmentNameConflict(); as msg) {
                <p class="px-4 sm:px-6 pb-2 text-xs text-amber-400">{{ msg }}</p>
              }
              <p class="px-4 sm:px-6 py-2 text-xs text-slate-400 border-b border-navy-800">
                {{ filteredDepartmentsForTable().length }} pôle(s) affiché(s)
              </p>
              <div class="overflow-x-auto">
                <table class="prime-table prime-table--dense w-full text-sm text-left">
                  <thead>
                    <tr>
                      <th class="font-medium">Département</th>
                      <th class="font-medium max-w-[min(100%,20rem)]">Pôle</th>
                      <th class="font-medium">Chef de projet actuel</th>
                      <th class="font-medium min-w-[220px]">Nouveau chef de projet</th>
                      <th class="font-medium w-48 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of filteredDepartmentsForTable(); track row.poleId) {
                      <tr>
                        <td class="px-4 py-2.5">
                          @if (row.unassigned) {
                            <span class="text-xs text-amber-400">Sans département</span>
                          } @else {
                            <span class="prime-cell-muted">{{ row.metierDepartmentName }}</span>
                          }
                        </td>
                        <td class="px-4 py-2.5"><span class="prime-cell-strong">{{ row.poleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ managerLabel(row.poleId) }}</span></td>
                        <td class="px-4 py-2.5">
                          <select
                            class="w-full min-w-[12rem] rounded-lg border border-navy-700 bg-navy-950 px-2 py-1.5 text-slate-200 text-sm"
                            [kyntusSelectSync]="draftManagerDept(row.poleId)"
                            (kyntusSelectSyncChange)="patchDraftManager(row.poleId, $event)"
                          >
                            <option value="">— Sélectionner —</option>
                            @for (e of employeesForManagerRow(row.poleId); track e.id) {
                              <option [value]="e.id">{{ employeeOptionLabel(e) }}</option>
                            }
                          </select>
                        </td>
                        <td class="px-4 py-2.5 text-right">
                          <div class="inline-flex flex-col items-end gap-2">
                            @if (row.unassigned) {
                              <div class="flex flex-wrap justify-end gap-2 items-center">
                                <select
                                  class="rounded-lg border border-navy-700 bg-navy-950 px-2 py-1 text-xs text-slate-200 min-w-[10rem]"
                                  [kyntusSelectSync]="draftAttachPole(row.poleId)"
                                  (kyntusSelectSyncChange)="patchDraftAttachPole(row.poleId, $event)"
                                >
                                  <option value="">Rattacher à…</option>
                                  @for (d of data()?.operationalDepartments ?? []; track d.id) {
                                    <option [value]="d.id">{{ d.name }}</option>
                                  }
                                </select>
                                <button
                                  type="button"
                                  (click)="attachOrphanPole(row.poleId)"
                                  [disabled]="saving() || !draftAttachPole(row.poleId)"
                                  class="rounded-md bg-amber-700 px-2 py-1 text-xs text-white disabled:opacity-50"
                                >
                                  Rattacher
                                </button>
                              </div>
                            }
                            <div class="inline-flex flex-wrap justify-end gap-2">
                              <button
                                type="button"
                                (click)="saveDepartmentManagerRow(row.poleId)"
                                [disabled]="saving() || !draftManagerDept(row.poleId)"
                                class="rounded-md bg-indigo-600 px-2.5 py-1.5 text-xs font-medium text-white disabled:opacity-50"
                              >
                                Enregistrer
                              </button>
                              <button
                                type="button"
                                (click)="clearDepartmentManagerRow(row.poleId)"
                                [disabled]="saving() || !managerUserId(row.poleId)"
                                class="rounded-md border border-navy-600 px-2.5 py-1.5 text-xs text-red-300 hover:bg-navy-800 disabled:opacity-50"
                              >
                                Retirer
                              </button>
                            </div>
                          </div>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="5" class="px-4 py-10 text-center text-slate-500 text-sm">
                          Aucun pôle à afficher. Créez un département puis un pôle.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </app-prime-card>
          }
          @case ('poles') {
            <app-prime-card className="p-0" title="Cellules" description="Un superviseur par cellule.">
              <div
                class="px-4 py-3 sm:px-6 border-b border-navy-800 bg-navy-950/40 flex flex-col lg:flex-row lg:flex-wrap gap-3 lg:items-end"
              >
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Département</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [kyntusSelectSync]="newPoleMetierDeptId()"
                    (kyntusSelectSyncChange)="patchNewPoleMetierDept($event)"
                  >
                    @for (d of data()?.operationalDepartments ?? []; track d.id) {
                      <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
                    }
                  </select>
                </label>
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Pôle</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [kyntusSelectSync]="newPoleDeptId()"
                    (kyntusSelectSyncChange)="newPoleDeptId.set($event)"
                  >
                    @for (p of polesForMetierDept(newPoleMetierDeptId()); track p.id) {
                      <option [value]="p.id">{{ p.name }}</option>
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
                  [disabled]="saving() || !newPoleDeptId() || !newPoleName().trim() || !!newPoleNameConflict()"
                  class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50 shrink-0"
                >
                  <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                  Créer la cellule
                </button>
              </div>
              @if (newPoleNameConflict(); as msg) {
                <p class="px-4 sm:px-6 pb-2 text-xs text-amber-400">{{ msg }}</p>
              }
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
                      <th class="font-medium">Département</th>
                      <th class="font-medium">Pôle</th>
                      <th class="font-medium">Cellule</th>
                      <th class="px-4 py-2.5 font-medium">Superviseur actuel</th>
                      <th class="px-4 py-2.5 font-medium min-w-[220px]">Nouveau superviseur</th>
                      <th class="px-4 py-2.5 font-medium w-40 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of filteredPolesForTable(); track row.celluleId) {
                      <tr>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.metierDepartmentName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.poleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-strong">{{ row.celluleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ supervisorLabel(row.celluleId) }}</span></td>
                        <td class="px-4 py-2.5">
                          <select
                            class="w-full min-w-[12rem] rounded-lg border border-navy-700 bg-navy-950 px-2 py-1.5 text-slate-200 text-sm"
                            [kyntusSelectSync]="draftSupervisorPole(row.celluleId)"
                            (kyntusSelectSyncChange)="patchDraftSupervisor(row.celluleId, $event)"
                          >
                            <option value="">— Sélectionner —</option>
                            @for (e of employeesForSupervisorRow(row.celluleId); track e.id) {
                              <option [value]="e.id">{{ employeeOptionLabel(e) }}</option>
                            }
                          </select>
                        </td>
                        <td class="px-4 py-2.5 text-right">
                          <div class="inline-flex flex-wrap justify-end gap-2">
                            <button
                              type="button"
                              (click)="savePoleSupervisorRow(row.celluleId)"
                              [disabled]="saving() || !draftSupervisorPole(row.celluleId)"
                              class="rounded-md bg-indigo-600 px-2.5 py-1.5 text-xs font-medium text-white disabled:opacity-50"
                            >
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearPoleSupervisorRow(row.celluleId)"
                              [disabled]="saving() || !supervisorUserId(row.celluleId)"
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
              title="Services"
              description="Référent technique par service ; pilotes rattachés listés par ligne."
            >
              <div
                class="px-4 py-3 sm:px-6 border-b border-navy-800 bg-navy-950/40 flex flex-col xl:flex-row xl:flex-wrap gap-3 xl:items-end"
              >
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Département</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [kyntusSelectSync]="newCellMetierDeptId()"
                    (kyntusSelectSyncChange)="patchNewCellMetierDept($event)"
                  >
                    @for (d of data()?.operationalDepartments ?? []; track d.id) {
                      <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
                    }
                  </select>
                </label>
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Pôle</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [kyntusSelectSync]="newCellDeptId()"
                    (kyntusSelectSyncChange)="patchNewCellDept($event)"
                  >
                    @for (p of polesForNewCellForm(); track p.id) {
                      <option [value]="p.id">{{ p.name }}</option>
                    }
                  </select>
                </label>
                <label class="text-sm text-slate-400 flex flex-col gap-1 min-w-[10rem]">
                  <span>Cellule</span>
                  <select
                    class="rounded-lg border border-navy-700 bg-navy-900 px-3 py-2 text-sm text-slate-200 min-w-[12rem]"
                    [kyntusSelectSync]="newCellPoleId()"
                    (kyntusSelectSyncChange)="newCellPoleId.set($event)"
                  >
                    @for (c of cellulesForPole(newCellDeptId()); track c.id) {
                      <option [value]="c.id">{{ c.name }}</option>
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
                  [disabled]="saving() || !newCellPoleId() || !newCellName().trim() || !!newCellNameConflict()"
                  class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50 shrink-0"
                >
                  <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                  Créer le service
                </button>
              </div>
              @if (newCellNameConflict(); as msg) {
                <p class="px-4 sm:px-6 pb-2 text-xs text-amber-400">{{ msg }}</p>
              }
              @if (cellulesForPole(newCellDeptId()).length === 0) {
                <p class="px-4 sm:px-6 py-2 text-xs text-amber-400/90">
                  Ce pôle n’a pas encore de cellule : créez-en une depuis l’onglet « Cellules », puis revenez ici.
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
                      <th class="px-4 py-2.5 font-medium">Département</th>
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
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.metierDepartmentName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.poleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ row.parentCelluleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-strong">{{ row.celluleName }}</span></td>
                        <td class="px-4 py-2.5"><span class="prime-cell-muted">{{ coachLabel(row.celluleId) }}</span></td>
                        <td class="px-4 py-2.5">
                          <select
                            class="w-full min-w-[12rem] rounded-lg border border-navy-700 bg-navy-950 px-2 py-1.5 text-slate-200 text-sm"
                            [kyntusSelectSync]="draftCoachCell(row.celluleId)"
                            (kyntusSelectSyncChange)="patchDraftCoach(row.celluleId, $event)"
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
                                [kyntusSelectSync]="draftPilotCell(row.celluleId)"
                                (kyntusSelectSyncChange)="patchDraftPilotCell(row.celluleId, $event)"
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
                                  [kyntusSelectSync]="draftPilotTeamCell(row.celluleId)"
                                  (kyntusSelectSyncChange)="patchDraftPilotTeamCell(row.celluleId, $event)"
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
                        <td colspan="8" class="px-4 py-10 text-center text-slate-500 text-sm">
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
                  @for (md of filteredOperationalTree(); track md.id) {
                    <div
                      class="rounded-xl border border-navy-800/70 bg-navy-900/45 overflow-hidden shadow-sm shadow-black/10"
                    >
                      <button
                        type="button"
                        [class]="deptTreeRowClass(md.id)"
                        (click)="toggleDept(md.id); selectMetierDepartment(md)"
                      >
                        <span class="flex h-full items-center justify-center shrink-0">
                          <app-lucide-icon
                            [icon]="deptExpanded(md.id) ? icons.chevDown : icons.chevRight"
                            className="w-4 h-4 text-slate-500"
                          />
                        </span>
                        <span class="min-w-0 flex items-center gap-2 font-semibold leading-snug text-slate-100">
                          <span
                            class="shrink-0 rounded px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide bg-amber-500/20 text-amber-100 ring-1 ring-amber-500/35"
                            >Dépt</span
                          >
                          <span class="truncate">{{ md.code }} — {{ md.name }}</span>
                        </span>
                        <span
                          class="min-w-0 text-xs leading-snug text-slate-400 text-right truncate"
                          [attr.title]="structureBadgeTitle(metierManagerLabel(md.id), 'Manager')"
                          >{{ metierManagerLabel(md.id) }}</span
                        >
                      </button>
                      @if (deptExpanded(md.id)) {
                        <div class="border-t border-navy-800/50 pl-3 ml-2 mr-1 space-y-2.5 pb-3 pt-1">
                          @for (pole of md.poles; track pole.id) {
                            <div class="rounded-lg border border-navy-800/55 bg-navy-950/55 overflow-hidden">
                              <button
                                type="button"
                                [class]="poleTreeRowClass(pole.id)"
                                (click)="togglePole(pole.id); selectPole(md, pole)"
                              >
                                <span class="flex items-center justify-center shrink-0">
                                  <app-lucide-icon
                                    [icon]="poleExpanded(pole.id) ? icons.chevDown : icons.chevRight"
                                    className="w-3.5 h-3.5 text-slate-500"
                                  />
                                </span>
                                <span class="min-w-0 flex items-center gap-2 text-sm font-medium leading-snug text-slate-200">
                                  <span
                                    class="shrink-0 rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide bg-sky-500/20 text-sky-100 ring-1 ring-sky-500/35"
                                    >Pôle</span
                                  >
                                  <span class="truncate">{{ pole.name }}</span>
                                </span>
                                <span
                                  class="min-w-0 text-xs leading-snug text-slate-400 text-right truncate"
                                  [attr.title]="structureBadgeTitle(managerBadge(pole.id), 'Chef de projet')"
                                  >{{ managerBadge(pole.id) || '—' }}</span
                                >
                              </button>
                              @if (poleExpanded(pole.id)) {
                                <div class="border-t border-navy-800/45 pl-2.5 ml-1.5 space-y-1 pb-2.5 pt-0.5">
                                  @for (cell of pole.cellules; track cell.id) {
                                    <div class="rounded-md border border-navy-800/40 overflow-hidden">
                                      <button
                                        type="button"
                                        [class]="celluleTreeRowClass(cell.id)"
                                        (click)="toggleCelluleExpand(cell.id); selectCellule(md, pole, cell)"
                                      >
                                        <span class="flex items-center justify-center shrink-0">
                                          <app-lucide-icon
                                            [icon]="celluleExpanded(cell.id) ? icons.chevDown : icons.chevRight"
                                            className="w-3 h-3 text-slate-500"
                                          />
                                        </span>
                                        <span class="min-w-0 flex items-center gap-2 truncate text-sm">
                                          <span
                                            class="shrink-0 rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide bg-emerald-500/20 text-emerald-100 ring-1 ring-emerald-500/35"
                                            >Cell.</span
                                          >
                                          <span class="truncate">{{ cell.name }}</span>
                                        </span>
                                        <span
                                          class="min-w-0 text-xs text-slate-400 text-right truncate"
                                          [attr.title]="structureBadgeTitle(supervisorBadge(cell.id), 'Superviseur')"
                                          >{{ supervisorBadge(cell.id) || '—' }}</span
                                        >
                                      </button>
                                      @if (celluleExpanded(cell.id)) {
                                        <div class="border-t border-navy-800/40 pl-2 space-y-0.5 pb-1">
                                          @for (svc of cell.services; track svc.id) {
                                            <button
                                              type="button"
                                              [class]="serviceButtonClass(svc.id)"
                                              (click)="selectService(md, pole, cell, svc)"
                                            >
                                              <span class="w-3 shrink-0 block" aria-hidden="true"></span>
                                              <span class="min-w-0 flex items-center gap-2 truncate">
                                                <span
                                                  class="shrink-0 rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide bg-violet-500/20 text-violet-100 ring-1 ring-violet-500/35"
                                                  >Svc.</span
                                                >
                                                <span class="truncate">{{ svc.name }}</span>
                                              </span>
                                              <span
                                                class="min-w-0 text-xs text-slate-400 text-right truncate"
                                                [attr.title]="structureBadgeTitle(coachBadge(svc.id), 'Référent technique')"
                                                >{{ coachBadge(svc.id) || '—' }}</span
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
                      }
                    </div>
                  }
                  @if ((data()?.unassignedPoles?.length ?? 0) > 0) {
                    <div class="rounded-xl border border-amber-800/40 bg-amber-950/20 p-3 space-y-2">
                      <p class="text-xs font-medium text-amber-200">Pôles sans département</p>
                      @for (pole of data()?.unassignedPoles ?? []; track pole.id) {
                        <div class="text-sm text-slate-300 pl-2">{{ pole.name }}</div>
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
                            @case ('metierDepartment') {
                              Département
                            }
                            @case ('pole') {
                              Pôle
                            }
                            @case ('cellule') {
                              Cellule
                            }
                            @case ('service') {
                              Service
                            }
                          }
                        </p>
                        <h2 class="text-2xl font-semibold tracking-tight text-slate-50">{{ sel.name }}</h2>
                      </header>

                      @if (sel.kind === 'metierDepartment') {
                        <div class="space-y-3">
                          <label class="text-sm font-medium text-slate-300 block">Manager opérationnel</label>
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
                              (click)="saveMetierManagerStructure(sel.id)"
                              [disabled]="saving() || !draftEmployeeId()"
                              class="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50"
                            >
                              <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                              Enregistrer
                            </button>
                            <button
                              type="button"
                              (click)="clearMetierManagerStructure(sel.id)"
                              [disabled]="saving() || metierManagerLabel(sel.id) === '—'"
                              class="inline-flex items-center justify-center gap-2 rounded-lg border border-navy-600 px-4 py-2.5 text-sm text-slate-300 hover:bg-navy-800 disabled:opacity-50"
                            >
                              <app-lucide-icon [icon]="icons.trash" className="w-4 h-4" />
                              Retirer le manager
                            </button>
                          </div>
                        </div>
                      }

                      @if (sel.kind === 'pole') {
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

                      @if (sel.kind === 'cellule') {
                        <div class="space-y-3">
                          <label class="text-sm font-medium text-slate-300 block">Superviseur</label>
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
                            <div class="flex flex-wrap gap-3">
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

                      @if (sel.kind === 'service') {
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
                                  [kyntusSelectSync]="draftPilotTeamId()"
                                  (kyntusSelectSyncChange)="draftPilotTeamId.set($event)"
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
  private readonly confirmService = inject(KyntusConfirmService);
  private readonly role = inject(RoleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

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
    { id: 'metier-departments', label: 'Départements' },
    { id: 'departments', label: 'Pôles' },
    { id: 'poles', label: 'Cellules' },
    { id: 'cellules', label: 'Services' },
    { id: 'structure', label: 'Vue structure' },
  ];

  readonly mainTab = signal<OrgMainTab>('metier-departments');

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly data = signal<OrgAssignmentsOverview | null>(null);

  readonly expandedDeptIds = signal<Set<string>>(new Set());
  readonly expandedPoleIds = signal<Set<string>>(new Set());
  readonly selection = signal<OrgTreeSelection | null>(null);
  readonly search = signal('');

  readonly newMetierDeptName = signal('');
  readonly newPoleBusinessDeptId = signal('');
  readonly newDepartmentName = signal('');
  readonly newPoleMetierDeptId = signal('');
  readonly newPoleDeptId = signal('');
  readonly newPoleName = signal('');
  readonly newCellMetierDeptId = signal('');
  readonly newCellDeptId = signal('');
  readonly newCellPoleId = signal('');
  readonly newCellName = signal('');

  readonly draftMetierManagerByDept = signal<Record<string, string>>({});
  readonly draftAttachPoleDept = signal<Record<string, string>>({});

  readonly draftManagerByDept = signal<Record<string, string>>({});
  readonly draftSupervisorByPole = signal<Record<string, string>>({});
  readonly draftCoachByCell = signal<Record<string, string>>({});
  readonly draftPilotByCell = signal<Record<string, string>>({});
  readonly draftPilotTeamByCell = signal<Record<string, string>>({});

  /** Lignes modifiées par l'utilisateur — préservées lors d'un rechargement silencieux. */
  private readonly dirtyRowDrafts = signal({
    mgr: new Set<string>(),
    sup: new Set<string>(),
    coach: new Set<string>(),
    pilot: new Set<string>(),
  });

  readonly expandedCelluleIds = signal<Set<string>>(new Set());

  readonly expandedCellPilotIds = signal<Set<string>>(new Set());

  readonly draftEmployeeId = signal('');
  readonly draftPilotId = signal('');
  readonly draftPilotTeamId = signal('');

  /** Recherche pour les listes filtrées (chef de projet / superviseur / référent technique) dans la vue structure. */
  readonly structureDetailEmpSearch = signal('');
  readonly structurePilotEmpSearch = signal('');
  readonly structureActivityLog = signal<StructureLogEntry[]>([]);

  readonly newMetierDeptNameConflict = computed((): string | null => {
    const name = this.newMetierDeptName().trim();
    if (!name) return null;
    const dupe = (this.data()?.operationalDepartments ?? []).some((d) => orgNamesEqual(d.name, name));
    return dupe ? 'Un département porte déjà ce nom.' : null;
  });

  readonly newDepartmentNameConflict = computed((): string | null => {
    const name = this.newDepartmentName().trim();
    if (!name) return null;
    const dupe = this.allPolesFlat().some((p) => orgNamesEqual(p.poleName, name));
    return dupe ? ORG_DUPLICATE_POLE_MSG : null;
  });

  readonly newPoleNameConflict = computed((): string | null => {
    const name = this.newPoleName().trim();
    const poleId = this.newPoleDeptId().trim();
    if (!name || !poleId) return null;
    const pole = this.findPoleNode(poleId);
    const dupe = (pole?.cellules ?? []).some((c) => orgNamesEqual(c.name, name));
    return dupe ? ORG_DUPLICATE_CELLULE_MSG : null;
  });

  readonly newCellNameConflict = computed((): string | null => {
    const name = this.newCellName().trim();
    const celluleId = this.newCellPoleId().trim();
    if (!name || !celluleId) return null;
    const cellule = this.findCelluleNode(celluleId);
    const dupe = (cellule?.services ?? []).some((s) => orgNamesEqual(s.name, name));
    return dupe ? ORG_DUPLICATE_SERVICE_MSG : null;
  });

  ngOnInit(): void {
    this.role.preferRhForOrgScreen();
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.mainTab.set(parseOrganisationRhTab(params.get('tab')));
    });
    this.load(false);
  }

  selectMainTab(id: OrgMainTab): void {
    this.mainTab.set(id);
    const hadTabQuery = this.route.snapshot.queryParamMap.has('tab');
    const tabQuery =
      id === 'departments' || id === 'metier-departments'
        ? (hadTabQuery ? id : null)
        : id;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: tabQuery },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
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
          if (!silent) this.expandAllForDiscovery(d.operationalDepartments);
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
    const dirty = this.dirtyRowDrafts();
    const prevMgr = this.draftManagerByDept();
    const prevSup = this.draftSupervisorByPole();
    const prevCoach = this.draftCoachByCell();
    const prevPilot = this.draftPilotByCell();
    const prevPilotTeam = this.draftPilotTeamByCell();

    const mgr: Record<string, string> = {};
    const sup: Record<string, string> = {};
    const coach: Record<string, string> = {};
    const pilotPick: Record<string, string> = {};
    const pilotTeam: Record<string, string> = {};

    for (const dept of d.departments) {
      mgr[dept.id] = dirty.mgr.has(dept.id) ? (prevMgr[dept.id] ?? '') : '';
      for (const pole of dept.poles) {
        sup[pole.id] = dirty.sup.has(pole.id) ? (prevSup[pole.id] ?? '') : '';
        for (const cell of pole.cells) {
          coach[cell.id] = dirty.coach.has(cell.id) ? (prevCoach[cell.id] ?? '') : '';
          pilotPick[cell.id] = dirty.pilot.has(cell.id) ? (prevPilot[cell.id] ?? '') : '';
          const teams = cell.teams ?? [];
          const defaultTeam = teams[0]?.id ?? '';
          pilotTeam[cell.id] = prevPilotTeam[cell.id] ?? defaultTeam;
          if (!teams.some((t) => t.id === pilotTeam[cell.id])) {
            pilotTeam[cell.id] = defaultTeam;
          }
        }
      }
    }

    const metierMgr: Record<string, string> = {};
    for (const md of d.operationalDepartments) {
      metierMgr[md.id] = prevMgr[md.id] ?? '';
    }

    this.draftManagerByDept.set(mgr);
    this.draftMetierManagerByDept.set(metierMgr);
    this.draftSupervisorByPole.set(sup);
    this.draftCoachByCell.set(coach);
    this.draftPilotByCell.set(pilotPick);
    this.draftPilotTeamByCell.set(pilotTeam);
  }

  private ensureStructureCreateFormDefaults(d: OrgAssignmentsOverview): void {
    const metierDepts = d.operationalDepartments;
    if (metierDepts.length === 0) {
      this.newPoleBusinessDeptId.set('');
      this.newPoleMetierDeptId.set('');
      this.newCellMetierDeptId.set('');
      this.newPoleDeptId.set('');
      this.newCellDeptId.set('');
      this.newCellPoleId.set('');
      return;
    }
    if (!metierDepts.some((x) => x.id === this.newPoleBusinessDeptId())) {
      this.newPoleBusinessDeptId.set(metierDepts[0].id);
    }
    if (!metierDepts.some((x) => x.id === this.newPoleMetierDeptId())) {
      this.newPoleMetierDeptId.set(metierDepts[0].id);
    }
    if (!metierDepts.some((x) => x.id === this.newCellMetierDeptId())) {
      this.newCellMetierDeptId.set(metierDepts[0].id);
    }
    const poles = this.polesForMetierDept(this.newPoleMetierDeptId());
    if (!poles.some((p) => p.id === this.newPoleDeptId())) {
      this.newPoleDeptId.set(poles[0]?.id ?? '');
    }
    const cellPoles = this.polesForMetierDept(this.newCellMetierDeptId());
    if (!cellPoles.some((p) => p.id === this.newCellDeptId())) {
      this.newCellDeptId.set(cellPoles[0]?.id ?? '');
    }
    const cellules = this.cellulesForPole(this.newCellDeptId());
    if (!cellules.some((c) => c.id === this.newCellPoleId())) {
      this.newCellPoleId.set(cellules[0]?.id ?? '');
    }
  }

  polesForMetierDept(metierDeptId: string): OrgPoleNode[] {
    return this.data()?.operationalDepartments.find((d) => d.id === metierDeptId)?.poles ?? [];
  }

  cellulesForPole(poleId: string): OrgCelluleNode[] {
    return this.findPoleNode(poleId)?.cellules ?? [];
  }

  findPoleNode(poleId: string): OrgPoleNode | undefined {
    const d = this.data();
    if (!d) return undefined;
    for (const md of d.operationalDepartments) {
      const pole = md.poles.find((p) => p.id === poleId);
      if (pole) return pole;
    }
    return d.unassignedPoles.find((p) => p.id === poleId);
  }

  findCelluleNode(celluleId: string): OrgCelluleNode | undefined {
    const d = this.data();
    if (!d) return undefined;
    for (const md of d.operationalDepartments) {
      for (const pole of md.poles) {
        const cellule = pole.cellules.find((c) => c.id === celluleId);
        if (cellule) return cellule;
      }
    }
    for (const pole of d.unassignedPoles) {
      const cellule = pole.cellules.find((c) => c.id === celluleId);
      if (cellule) return cellule;
    }
    return undefined;
  }

  patchNewPoleMetierDept(metierDeptId: string): void {
    this.newPoleMetierDeptId.set(metierDeptId);
    const poles = this.polesForMetierDept(metierDeptId);
    const cur = this.newPoleDeptId();
    if (!cur || !poles.some((p) => p.id === cur)) {
      this.newPoleDeptId.set(poles[0]?.id ?? '');
    }
  }

  patchNewCellMetierDept(metierDeptId: string): void {
    this.newCellMetierDeptId.set(metierDeptId);
    const poles = this.polesForMetierDept(metierDeptId);
    const curPole = this.newCellDeptId();
    if (!curPole || !poles.some((p) => p.id === curPole)) {
      this.newCellDeptId.set(poles[0]?.id ?? '');
    }
    this.patchNewCellDept(this.newCellDeptId());
  }

  patchNewCellDept(poleId: string): void {
    this.newCellDeptId.set(poleId);
    const cellules = this.cellulesForPole(poleId);
    const curCell = this.newCellPoleId();
    if (curCell && cellules.some((c) => c.id === curCell)) return;
    this.newCellPoleId.set(cellules[0]?.id ?? '');
  }

  submitNewMetierDepartment(): void {
    const name = this.newMetierDeptName().trim();
    if (!name || this.newMetierDeptNameConflict()) return;
    this.runMutation(
      this.orgApi.createOperationalDepartment(name),
      () => this.newMetierDeptName.set(''),
      'Département opérationnel créé',
    );
  }

  submitNewDepartment(): void {
    const businessDeptId = this.newPoleBusinessDeptId().trim();
    const name = this.newDepartmentName().trim();
    if (!businessDeptId || !name || this.newDepartmentNameConflict()) return;
    this.runMutation(
      this.orgApi.createStructurePole(businessDeptId, name),
      () => this.newDepartmentName.set(''),
      'Pôle ajouté',
    );
  }

  submitNewPole(): void {
    const poleId = this.newPoleDeptId().trim();
    const name = this.newPoleName().trim();
    if (!poleId || !name || this.newPoleNameConflict()) return;
    this.runMutation(this.orgApi.createStructureCellule(poleId, name), () => this.newPoleName.set(''), 'Cellule ajoutée');
  }

  submitNewCellule(): void {
    const celluleId = this.newCellPoleId().trim();
    const name = this.newCellName().trim();
    if (!celluleId || !name || this.newCellNameConflict()) return;
    this.runMutation(
      this.orgApi.createStructureService(celluleId, name),
      () => this.newCellName.set(''),
      'Service ajouté',
    );
  }

  attachOrphanPole(poleId: string): void {
    const deptId = (this.draftAttachPoleDept()[poleId] ?? '').trim();
    if (!deptId) return;
    this.runMutation(
      this.orgApi.attachPoleToOperationalDepartment(poleId, deptId),
      () => this.draftAttachPoleDept.update((m) => ({ ...m, [poleId]: '' })),
      'Pôle rattaché au département',
    );
  }

  patchDraftAttachPole(poleId: string, value: string): void {
    this.draftAttachPoleDept.update((m) => ({ ...m, [poleId]: value }));
  }

  draftAttachPole(poleId: string): string {
    return this.draftAttachPoleDept()[poleId] ?? '';
  }

  draftMetierManager(deptId: string): string {
    return this.draftMetierManagerByDept()[deptId] ?? '';
  }

  patchDraftMetierManager(deptId: string, value: string): void {
    this.draftMetierManagerByDept.update((m) => ({ ...m, [deptId]: value }));
  }

  metierManagerLabel(deptId: string): string {
    const md = this.data()?.operationalDepartments.find((d) => d.id === deptId);
    const uid = md?.managerEmployeeId;
    return uid ? this.employeeLabel(uid) : '—';
  }

  async saveMetierManagerRow(deptId: string): Promise<void> {
    const id = this.draftMetierManager(deptId);
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) return;
    const md = this.data()?.operationalDepartments.find((d) => d.id === deptId);
    if (md?.managerEmployeeId && md.managerEmployeeId !== id) {
      const ok = await this.confirmService.confirm({
        title: 'Remplacer le manager ?',
        message: 'Le manager opérationnel accède à l’interface Prime classique.',
        confirmLabel: 'Remplacer',
      });
      if (!ok) return;
    }
    this.runMutation(
      this.orgApi.setOperationalDepartmentManager(deptId, id),
      () => this.patchDraftMetierManager(deptId, ''),
      'Manager opérationnel enregistré',
    );
  }

  clearMetierManagerRow(deptId: string): void {
    this.runMutation(
      this.orgApi.clearOperationalDepartmentManager(deptId),
      undefined,
      'Manager opérationnel retiré',
    );
  }

  private syncDraftFromSelection(): void {
    const sel = this.selection();
    if (!sel) return;
    const serviceId = sel.kind === 'service' ? sel.id : '';
    const teams = serviceId ? this.teamsForCell(serviceId) : [];
    if (teams.length > 0) {
      const curTeam = this.draftPilotTeamId();
      if (!curTeam || !teams.some((t) => t.id === curTeam)) {
        this.draftPilotTeamId.set(teams[0]?.id ?? '');
      }
    }
  }

  private expandAllForDiscovery(departments: OperationalDepartmentNode[]): void {
    const ds = new Set<string>();
    const ps = new Set<string>();
    const cs = new Set<string>();
    for (const d of departments) {
      ds.add(d.id);
      for (const p of d.poles) {
        ps.add(p.id);
        for (const c of p.cellules) {
          cs.add(c.id);
        }
      }
    }
    this.expandedDeptIds.set(ds);
    this.expandedPoleIds.set(ps);
    this.expandedCelluleIds.set(cs);
  }

  celluleExpanded(id: string): boolean {
    return this.expandedCelluleIds().has(id);
  }

  toggleCelluleExpand(id: string): void {
    const s = new Set(this.expandedCelluleIds());
    if (s.has(id)) s.delete(id);
    else s.add(id);
    this.expandedCelluleIds.set(s);
  }

  allPolesFlat = computed((): FlatPoleRow[] => {
    const out: FlatPoleRow[] = [];
    for (const md of this.data()?.operationalDepartments ?? []) {
      for (const p of md.poles) {
        out.push({
          metierDepartmentId: md.id,
          metierDepartmentName: md.name,
          poleId: p.id,
          poleName: p.name,
          unassigned: false,
        });
      }
    }
    for (const p of this.data()?.unassignedPoles ?? []) {
      out.push({
        metierDepartmentId: '',
        metierDepartmentName: 'Sans département',
        poleId: p.id,
        poleName: p.name,
        unassigned: true,
      });
    }
    return out;
  });

  allCellulesFlat = computed((): FlatCelluleRow[] => {
    const out: FlatCelluleRow[] = [];
    const pushCellules = (
      md: OperationalDepartmentNode,
      pole: OrgPoleNode,
      unassigned: boolean,
    ) => {
      for (const c of pole.cellules) {
        out.push({
          metierDepartmentId: md.id,
          metierDepartmentName: unassigned ? 'Sans département' : md.name,
          poleId: pole.id,
          poleName: pole.name,
          unassigned,
          celluleId: c.id,
          celluleName: c.name,
        });
      }
    };
    for (const md of this.data()?.operationalDepartments ?? []) {
      for (const p of md.poles) {
        pushCellules(md, p, false);
      }
    }
    for (const p of this.data()?.unassignedPoles ?? []) {
      pushCellules({ id: '', code: '', name: 'Sans département', poles: [] }, p, true);
    }
    return out;
  });

  allServicesFlat = computed((): FlatCelluleRow[] => {
    const out: FlatCelluleRow[] = [];
    const pushServices = (
      md: OperationalDepartmentNode,
      pole: OrgPoleNode,
      unassigned: boolean,
    ) => {
      for (const c of pole.cellules) {
        for (const s of c.services) {
          out.push({
            metierDepartmentId: md.id,
            metierDepartmentName: unassigned ? 'Sans département' : md.name,
            poleId: pole.id,
            poleName: pole.name,
            unassigned,
            parentCelluleName: c.name,
            celluleId: s.id,
            celluleName: s.name,
          });
        }
      }
    };
    for (const md of this.data()?.operationalDepartments ?? []) {
      for (const p of md.poles) {
        pushServices(md, p, false);
      }
    }
    for (const p of this.data()?.unassignedPoles ?? []) {
      pushServices({ id: '', code: '', name: 'Sans département', poles: [] }, p, true);
    }
    return out;
  });

  private matchesSearch(q: string, ...parts: string[]): boolean {
    if (!q) return true;
    return parts.some((p) => p.toLowerCase().includes(q));
  }

  filteredDepartmentsForTable = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.allPolesFlat().filter((r) =>
      this.matchesSearch(q, r.metierDepartmentName, r.poleName),
    );
  });

  filteredMetierDepartmentsForTable = computed(() => {
    const q = this.search().trim().toLowerCase();
    return (this.data()?.operationalDepartments ?? []).filter((d) =>
      this.matchesSearch(q, d.code, d.name),
    );
  });

  filteredPolesForTable = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.allCellulesFlat().filter((r) =>
      this.matchesSearch(q, r.metierDepartmentName, r.poleName, r.celluleName),
    );
  });

  filteredCellulesForTable = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.allServicesFlat().filter((r) =>
      this.matchesSearch(q, r.metierDepartmentName, r.poleName, r.celluleName),
    );
  });

  filteredOperationalTree = computed(() => {
    const q = this.search().trim().toLowerCase();
    const depts = this.data()?.operationalDepartments ?? [];
    if (!q) return depts;
    return depts.filter((d) => {
      if (d.name.toLowerCase().includes(q) || d.code.toLowerCase().includes(q)) return true;
      return d.poles.some(
        (p) =>
          p.name.toLowerCase().includes(q) ||
          p.cellules.some(
            (c) =>
              c.name.toLowerCase().includes(q) ||
              c.services.some((s) => s.name.toLowerCase().includes(q)),
          ),
      );
    });
  });

  readonly polesForNewCellForm = computed((): OrgPoleNode[] => {
    return this.polesForMetierDept(this.newCellMetierDeptId());
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

  /** Employés rattachés au nœud sélectionné dans la vue structure. */
  private employeesForStructureSelection(
    sel: OrgTreeSelection,
    employees: readonly Employee[],
  ): Employee[] {
    if (sel.kind === 'metierDepartment') {
      const poleIds = new Set(
        (this.data()?.operationalDepartments.find((d) => d.id === sel.id)?.poles ?? []).map((p) => p.id),
      );
      return employees.filter((e) => poleIds.has(e.poleId));
    }
    if (sel.kind === 'pole') {
      return employees.filter((e) => e.poleId === sel.id);
    }
    if (sel.kind === 'cellule') {
      return employees.filter((e) => e.celluleId === sel.id);
    }
    return employees.filter(
      (e) => e.serviceId === sel.id || (e.serviceId === '' && e.celluleId === sel.id),
    );
  }

  /** Membres rattachés au nœud sélectionné (vue structure, panneau droit). */
  readonly structureContextMembers = computed((): Employee[] => {
    const sel = this.selection();
    const employees = this.data()?.employees ?? [];
    const scope = sel ? this.employeesForStructureSelection(sel, employees) : employees;
    return [...scope].sort((a, b) =>
      `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`, 'fr'),
    );
  });

  readonly structureContextKpis = computed(() => {
    const sel = this.selection();
    const data = this.data();
    const employees = data?.employees ?? [];

    if (!sel) {
      const sansMgr = this.allPolesFlat().filter((p) => !this.managerUserId(p.poleId)).length;
      const sansMetier = (data?.operationalDepartments ?? []).filter((d) => !d.managerEmployeeId).length;
      return {
        scopeTitle: 'Organisation',
        effectif: employees.length,
        parite: 'Non disponible',
        vacants: `${sansMetier} dépt(s) sans manager · ${sansMgr} pôle(s) sans chef de projet`,
      };
    }
    if (sel.kind === 'metierDepartment') {
      const md = data?.operationalDepartments.find((x) => x.id === sel.id);
      const sansChef = (md?.poles ?? []).filter((p) => !this.managerUserId(p.id)).length;
      const effectif = this.employeesForStructureSelection(sel, employees).length;
      return {
        scopeTitle: sel.name,
        effectif,
        parite: 'Non disponible',
        vacants: `${sansChef} pôle(s) sans chef de projet`,
      };
    }
    if (sel.kind === 'pole') {
      const pole = this.findPoleNode(sel.id);
      const sansSup = (pole?.cellules ?? []).filter((c) => !this.supervisorUserId(c.id)).length;
      const effectif = this.employeesForStructureSelection(sel, employees).length;
      return {
        scopeTitle: sel.name,
        effectif,
        parite: 'Non disponible',
        vacants: `${sansSup} cellule(s) sans superviseur`,
      };
    }
    if (sel.kind === 'cellule') {
      const cellule = this.findCelluleNode(sel.id);
      const sansCoach = (cellule?.services ?? []).filter((s) => !this.coachUserId(s.id)).length;
      const effectif = this.employeesForStructureSelection(sel, employees).length;
      return {
        scopeTitle: sel.name,
        effectif,
        parite: 'Non disponible',
        vacants: `${sansCoach} service(s) sans référent technique`,
      };
    }
    const teams = this.teamsForCell(sel.id);
    const nPilotes = this.pilotsInCell(sel.id).length;
    const effectif = this.employeesForStructureSelection(sel, employees).length;
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

  private markRowDraftDirty(kind: 'mgr' | 'sup' | 'coach' | 'pilot', id: string): void {
    this.dirtyRowDrafts.update((d) => {
      const next = { mgr: new Set(d.mgr), sup: new Set(d.sup), coach: new Set(d.coach), pilot: new Set(d.pilot) };
      next[kind].add(id);
      return next;
    });
  }

  private clearRowDraftDirty(kind: 'mgr' | 'sup' | 'coach' | 'pilot', id: string): void {
    this.dirtyRowDrafts.update((d) => {
      const next = { mgr: new Set(d.mgr), sup: new Set(d.sup), coach: new Set(d.coach), pilot: new Set(d.pilot) };
      next[kind].delete(id);
      return next;
    });
  }

  private reconcileDraftWithOptions(
    draft: string,
    options: Employee[],
  ): string {
    const opts = options.map((e) => e.id);
    return reconcileSelectModel(draft, opts);
  }

  patchDraftManager(deptId: string, value: string): void {
    const reconciled = this.reconcileDraftWithOptions(
      value,
      this.employeesForManagerRow(deptId),
    );
    this.draftManagerByDept.update((m) => ({ ...m, [deptId]: reconciled }));
    if (reconciled) this.markRowDraftDirty('mgr', deptId);
    else this.clearRowDraftDirty('mgr', deptId);
  }

  patchDraftSupervisor(poleId: string, value: string): void {
    const reconciled = this.reconcileDraftWithOptions(
      value,
      this.employeesForSupervisorRow(poleId),
    );
    this.draftSupervisorByPole.update((m) => ({ ...m, [poleId]: reconciled }));
    if (reconciled) this.markRowDraftDirty('sup', poleId);
    else this.clearRowDraftDirty('sup', poleId);
  }

  patchDraftCoach(cellId: string, value: string): void {
    const reconciled = this.reconcileDraftWithOptions(value, this.employeesForCoachRow(cellId));
    this.draftCoachByCell.update((m) => ({ ...m, [cellId]: reconciled }));
    if (reconciled) this.markRowDraftDirty('coach', cellId);
    else this.clearRowDraftDirty('coach', cellId);
  }

  employeesForPilotRow(cellId: string): Employee[] {
    const emps = this.data()?.employees ?? [];
    return employeesForOrgAssignmentSelect(emps, this.draftPilotCell(cellId));
  }

  patchDraftPilotCell(cellId: string, value: string): void {
    const reconciled = this.reconcileDraftWithOptions(value, this.employeesForPilotRow(cellId));
    this.draftPilotByCell.update((m) => ({ ...m, [cellId]: reconciled }));
    if (reconciled) this.markRowDraftDirty('pilot', cellId);
    else this.clearRowDraftDirty('pilot', cellId);
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
    if (sel?.kind === 'metierDepartment' && sel.id === deptId) {
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

  celluleTreeRowClass(celluleId: string): string {
    const base =
      'w-full grid grid-cols-[1.25rem_minmax(0,1fr)_minmax(0,8rem)] gap-x-2 gap-y-0.5 items-center px-2 py-2 text-left text-slate-300 hover:bg-navy-800/40 transition-colors';
    const sel = this.selection();
    if (sel?.kind === 'cellule' && sel.id === celluleId) {
      return `${base} ring-2 ring-inset ring-indigo-500/45 bg-indigo-950/25`;
    }
    return base;
  }

  serviceButtonClass(serviceId: string): string {
    const base =
      'grid grid-cols-[0.75rem_minmax(0,1fr)_minmax(0,6.5rem)] gap-x-2 items-center w-full px-2 py-2.5 rounded-md text-left text-sm text-slate-300 hover:bg-navy-800/40 transition-colors';
    const sel = this.selection();
    if (sel?.kind === 'service' && sel.id === serviceId) {
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

  selectMetierDepartment(d: OperationalDepartmentNode): void {
    this.selection.set({ kind: 'metierDepartment', id: d.id, name: d.name, code: d.code });
    this.draftEmployeeId.set('');
    this.draftPilotId.set('');
    this.draftPilotTeamId.set('');
    this.structureDetailEmpSearch.set('');
    this.structurePilotEmpSearch.set('');
  }

  selectPole(md: OperationalDepartmentNode, p: OrgPoleNode): void {
    this.selection.set({
      kind: 'pole',
      id: p.id,
      name: p.name,
      metierDepartmentId: md.id,
    });
    this.draftEmployeeId.set('');
    this.draftPilotId.set('');
    this.draftPilotTeamId.set('');
    this.structureDetailEmpSearch.set('');
    this.structurePilotEmpSearch.set('');
  }

  selectCellule(md: OperationalDepartmentNode, p: OrgPoleNode, c: OrgCelluleNode): void {
    this.selection.set({
      kind: 'cellule',
      id: c.id,
      name: c.name,
      poleId: p.id,
      metierDepartmentId: md.id,
    });
    this.draftEmployeeId.set('');
    this.draftPilotId.set('');
    this.draftPilotTeamId.set('');
    this.structureDetailEmpSearch.set('');
    this.structurePilotEmpSearch.set('');
  }

  selectService(
    md: OperationalDepartmentNode,
    p: OrgPoleNode,
    c: OrgCelluleNode,
    s: { id: string; name: string },
  ): void {
    this.selection.set({
      kind: 'service',
      id: s.id,
      name: s.name,
      celluleId: c.id,
      poleId: p.id,
      metierDepartmentId: md.id,
    });
    this.draftEmployeeId.set('');
    this.draftPilotId.set('');
    const teams = this.teamsForCell(s.id);
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

  managerUserId(poleId: string): string | undefined {
    const d = this.data();
    if (!d) return undefined;
    return findStructureIncumbent(d, 'Chef de projet', { orgPoleId: poleId })?.userId;
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

  private async confirmCrossRoleAssignment(employeeId: string): Promise<boolean> {
    const overview = this.data();
    if (!overview) return true;
    const existing = findEmployeeStructuralRole(overview, employeeId);
    if (!existing) return true;
    const name = employeeDisplayName(overview.employees, employeeId);
    return this.confirmService.confirm({
      title: 'Remplacer le rôle actuel',
      message: buildCrossRoleOverwriteMessage(name, existing),
      confirmLabel: 'Remplacer et continuer',
      cancelLabel: 'Annuler',
      variant: 'warning',
    });
  }

  private formatRevokedLog(result: unknown): string[] {
    if (!result || typeof result !== 'object') return [];
    const revoked = (result as StructuralRoleAssignmentResult).revoked;
    if (!Array.isArray(revoked) || revoked.length === 0) return [];
    return revoked.map((v) => {
      const where = v.nodeLabel ?? v.departmentCode ?? v.nodeId;
      return `Ancien rôle retiré : ${v.role}${where ? ` (${where})` : ''}`;
    });
  }

  private async confirmStructureReplace(
    roleName: 'Chef de projet' | 'Superviseur' | 'Référent technique',
    incumbentUserId: string | undefined,
    newUserId: string,
  ): Promise<boolean> {
    if (!shouldConfirmOverwrite(incumbentUserId, newUserId)) {
      return true;
    }
    const employees = this.data()?.employees ?? [];
    const incumbent = {
      userId: incumbentUserId!,
      displayName: employeeDisplayName(employees, incumbentUserId!),
    };
    return this.confirmService.confirm({
      title: 'Remplacer le titulaire actuel',
      message: buildStructureOverwriteMessage(incumbent, roleName),
      confirmLabel: 'Écraser et continuer',
      cancelLabel: 'Annuler',
      variant: 'warning',
    });
  }

  async saveDepartmentManagerRow(departmentId: string): Promise<void> {
    const id = this.draftManagerByDept()[departmentId];
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) {
      return;
    }
    if (!(await this.confirmStructureReplace('Chef de projet', this.managerUserId(departmentId), id))) {
      return;
    }
    this.runMutation(
      this.orgApi.setStructureManager(departmentId, id),
      () => {
        this.clearRowDraftDirty('mgr', departmentId);
        this.patchDraftManager(departmentId, '');
      },
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

  async savePoleSupervisorRow(poleId: string): Promise<void> {
    const id = this.draftSupervisorByPole()[poleId];
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) return;
    if (!(await this.confirmStructureReplace('Superviseur', this.supervisorUserId(poleId), id))) {
      return;
    }
    this.runMutation(
      this.orgApi.setStructureSupervisor(poleId, id),
      () => {
        this.clearRowDraftDirty('sup', poleId);
        this.patchDraftSupervisor(poleId, '');
      },
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

  async saveCellCoachRow(celluleId: string): Promise<void> {
    const id = this.draftCoachByCell()[celluleId];
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) return;
    if (!(await this.confirmStructureReplace('Référent technique', this.coachUserId(celluleId), id))) {
      return;
    }
    this.runMutation(
      this.orgApi.setStructureCoach(celluleId, id),
      () => {
        this.clearRowDraftDirty('coach', celluleId);
        this.patchDraftCoach(celluleId, '');
      },
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

  async addPilotRow(celluleId: string): Promise<void> {
    const emp = this.draftPilotByCell()[celluleId];
    if (!emp) return;
    if (!(await this.confirmCrossRoleAssignment(emp))) return;
    const teams = this.teamsForCell(celluleId);
    const teamId =
      teams.length > 1
        ? this.draftPilotTeamByCell()[celluleId] || teams[0]?.id || undefined
        : undefined;
    this.runMutation(
      this.orgApi.addStructurePilot(celluleId, emp, teamId),
      () => {
        this.clearRowDraftDirty('pilot', celluleId);
        this.patchDraftPilotCell(celluleId, '');
      },
      'Pilote ajouté (liste services)',
    );
  }

  async saveMetierManagerStructure(deptId: string): Promise<void> {
    const id = this.draftEmployeeId();
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) return;
    const md = this.data()?.operationalDepartments.find((d) => d.id === deptId);
    if (md?.managerEmployeeId && md.managerEmployeeId !== id) {
      const ok = await this.confirmService.confirm({
        title: 'Remplacer le manager ?',
        message: 'Le manager opérationnel accède à l’interface Prime classique.',
        confirmLabel: 'Remplacer',
      });
      if (!ok) return;
    }
    this.runMutation(
      this.orgApi.setOperationalDepartmentManager(deptId, id),
      undefined,
      'Manager opérationnel mis à jour (vue structure)',
    );
  }

  clearMetierManagerStructure(deptId: string): void {
    this.runMutation(
      this.orgApi.clearOperationalDepartmentManager(deptId),
      undefined,
      'Manager opérationnel retiré (vue structure)',
    );
  }

  async saveDepartmentManager(departmentId: string): Promise<void> {
    const id = this.draftEmployeeId();
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) {
      return;
    }
    if (!(await this.confirmStructureReplace('Chef de projet', this.managerUserId(departmentId), id))) {
      return;
    }
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

  async savePoleSupervisor(poleId: string): Promise<void> {
    const id = this.draftEmployeeId();
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) return;
    if (!(await this.confirmStructureReplace('Superviseur', this.supervisorUserId(poleId), id))) {
      return;
    }
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

  async saveCellCoach(celluleId: string): Promise<void> {
    const id = this.draftEmployeeId();
    if (!id) return;
    if (!(await this.confirmCrossRoleAssignment(id))) return;
    if (!(await this.confirmStructureReplace('Référent technique', this.coachUserId(celluleId), id))) {
      return;
    }
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

  async addPilot(celluleId: string): Promise<void> {
    const emp = this.draftPilotId();
    if (!emp) return;
    if (!(await this.confirmCrossRoleAssignment(emp))) return;
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
      next: (result: unknown) => {
        onOk?.();
        if (logMessage) this.pushStructureLog(logMessage);
        for (const msg of this.formatRevokedLog(result)) {
          this.pushStructureLog(msg);
        }
        this.draftEmployeeId.set('');
        this.draftPilotId.set('');
        this.load(true);
      },
      error: (err: unknown) => this.error.set(httpErrMessage(err)),
    });
  }
}
