import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Building2, Check, Plus, RefreshCw, Trash2, Users } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import {
  employeesForOrgAssignmentSelect,
} from '../../lib/prime-select-options';
import type { Employee } from '../../models';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import { employeeSupportDepartmentLabel } from '../../../../core/org/user-org-perimeter';

interface BusinessDepartmentDto {
  id: string;
  code: string;
  name: string;
  kind: string;
  managerEmployeeId?: string;
  isActive: boolean;
  poleIds: string[];
}

interface DirectoryEmployeeDto {
  id: string;
  firstName: string;
  lastName: string;
  role: string;
  parentId?: string | null;
  serviceId?: string | null;
  poleId?: string | null;
  celluleId?: string | null;
  email: string;
  businessDepartmentId?: string | null;
  businessDepartmentKind?: string | null;
}

type PageTab = 'list' | 'assignments';

@Component({
  selector: 'app-business-departments-page',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent, PrimeCardComponent, KyntusPageHeaderComponent],
  template: `
    <div class="ky-page-shell dept-page">
      <app-kyntus-page-header
        title="Départements métier"
        subtitle="Organisation support : équipes plates sans pôle, cellule ou service. Même structure, même rythme visuel que les autres modules."
      >
        <button
          actions
          type="button"
          (click)="reload()"
          [disabled]="saving()"
          class="ky-btn-secondary inline-flex items-center gap-2"
        >
          <app-lucide-icon [icon]="icons.refresh" className="w-4 h-4" />
          Actualiser
        </button>
      </app-kyntus-page-header>

      <div class="dept-toolbar ky-card">
        <label class="text-sm text-muted flex items-center gap-2">
          Rechercher
          <input
            type="search"
            class="rounded-lg border border-default bg-input px-3 py-2 text-sm text-primary w-64"
            placeholder="Filtrer les départements…"
            [value]="search()"
            (input)="search.set($any($event.target).value)"
          />
        </label>
      </div>

      @if (flash()) {
        <div
          class="rounded-lg border px-4 py-3 text-sm"
          [class.dept-flash--ok]="flashOk()"
          [class.dept-flash--err]="!flashOk()"
        >
          {{ flash() }}
        </div>
      }

      <div class="flex flex-wrap gap-2 border-b border-default pb-2" role="tablist">
        @for (t of pageTabs; track t.id) {
          <button
            type="button"
            role="tab"
            [attr.aria-selected]="activeTab() === t.id"
            (click)="activeTab.set(t.id)"
            class="rounded-lg px-4 py-2 text-sm font-medium transition-colors"
            [class.dept-tab--active]="activeTab() === t.id"
            [class.bg-card]="activeTab() !== t.id"
            [class.text-muted]="activeTab() !== t.id"
          >
            {{ t.label }}
          </button>
        }
      </div>

      @if (loading()) {
        <p class="text-muted text-sm">Chargement…</p>
      } @else if (activeTab() === 'list') {
        <app-prime-card className="p-0" title="Créer un département Support" description="Équipe plate — code, nom, puis affectations dans l’onglet suivant.">
          <div class="px-4 sm:px-6 py-4 space-y-4 border-b border-default">
            <p class="text-xs text-muted rounded-lg border border-[var(--info-border)] bg-[var(--info-bg)] px-3 py-2">
              Type fixe : <span class="font-medium text-[var(--info-text)]">Support (équipe plate)</span> — manager → N-1 directs, module Allowances.
            </p>
            <div class="flex flex-col sm:flex-row sm:flex-wrap gap-3 sm:items-end">
              <label class="text-sm text-muted flex flex-col gap-1 min-w-[8rem]">
                Code
                <input class="rounded-lg border border-default bg-card px-3 py-2 text-sm text-primary" placeholder="IT" [value]="formCode()" (input)="formCode.set($any($event.target).value)" />
              </label>
              <label class="text-sm text-muted flex flex-col gap-1 flex-1 min-w-[12rem]">
                Nom
                <input class="rounded-lg border border-default bg-card px-3 py-2 text-sm text-primary" placeholder="Informatique" [value]="formName()" (input)="formName.set($any($event.target).value)" />
              </label>
              <button
                type="button"
                (click)="create()"
                [disabled]="saving() || !canCreate()"
                class="ky-btn-primary inline-flex items-center gap-2 text-sm font-medium disabled:opacity-50"
              >
                <app-lucide-icon [icon]="icons.plus" className="w-4 h-4" />
                Créer
              </button>
            </div>
          </div>
        </app-prime-card>

        <app-prime-card className="p-0 mt-4" title="Départements enregistrés" [hasAction]="false">
          <div class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="text-xs uppercase text-muted border-b border-default bg-input/50">
                <tr>
                  <th class="px-4 py-3">Code</th>
                  <th class="px-4 py-3">Nom</th>
                  <th class="px-4 py-3">Type</th>
                  <th class="px-4 py-3">Manager</th>
                  <th class="px-4 py-3">Effectif</th>
                  <th class="px-4 py-3"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                @for (d of filteredDepartments(); track d.id) {
                  <tr class="hover:bg-card/40">
                    <td class="px-4 py-3 font-mono text-muted">{{ d.code }}</td>
                    <td class="px-4 py-3 text-primary">{{ d.name }}</td>
                    <td class="px-4 py-3">
                      <span class="ky-badge ky-badge--info">
                        Support
                      </span>
                    </td>
                    <td class="px-4 py-3 text-muted">{{ employeeLabel(d.managerEmployeeId) }}</td>
                    <td class="px-4 py-3 text-muted tabular-nums">{{ teamCount(d) }}</td>
                    <td class="px-4 py-3 text-right">
                      <button type="button" class="text-xs text-[var(--electric-blue)] hover:opacity-80" (click)="openAssignments(d.id)">Affecter</button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="px-4 py-8 text-center text-muted">Aucun département. Créez-en un ci-dessus.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </app-prime-card>
      } @else {
        <div
          class="w-full grid grid-cols-1 xl:grid-cols-3 gap-6 xl:gap-8 items-stretch xl:items-start"
        >
          <app-prime-card
            className="min-w-0 p-0 flex flex-col xl:min-h-[28rem]"
            title="Départements métier"
            description="Sélectionnez un département dans la liste."
            [hasAction]="false"
          >
            <ul class="divide-y divide-default max-h-[min(70vh,32rem)] overflow-y-auto -m-6 mt-0">
              @for (d of filteredDepartments(); track d.id) {
                <li>
                  <button
                    type="button"
                    (click)="selectDepartment(d)"
                    class="w-full text-left px-4 py-3 hover:bg-input/50 transition-colors"
                    [class.dept-item--selected]="selectedId() === d.id"
                  >
                    <div class="flex items-center gap-2 min-w-0">
                      <span
                        class="shrink-0 rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide bg-[var(--info-bg)] text-[var(--info-text)]"
                      >Support</span>
                      <span class="min-w-0 truncate font-medium text-primary">{{ d.name }}</span>
                    </div>
                    <p class="text-xs text-muted mt-1 truncate">{{ d.code }} · {{ teamCount(d) }} membre(s) · {{ employeeLabel(d.managerEmployeeId) }}</p>
                  </button>
                </li>
              } @empty {
                <li class="px-4 py-6 text-sm text-muted">Aucun département.</li>
              }
            </ul>
          </app-prime-card>

          <app-prime-card
            className="min-w-0 p-0 flex flex-col xl:min-h-[28rem] shadow-md shadow-black/20"
            title="Détail du département"
            description="Choisissez un employé puis enregistrez. Le rôle devient Manager automatiquement."
            [hasAction]="false"
          >
            <div class="flex min-h-[20rem] flex-1 flex-col p-4 sm:p-6">
              @if (selectedDept(); as dept) {
                <header class="space-y-1 border-b border-default/80 pb-4 mb-6">
                  <p class="text-xs font-semibold uppercase tracking-wider text-muted">Support — équipe plate</p>
                  <h2 class="text-2xl font-semibold tracking-tight text-primary">{{ dept.name }}</h2>
                </header>

                <div class="space-y-3">
                  <label class="text-sm font-medium text-muted block">Manager du département</label>
                  @if (draftEmployeeId()) {
                    <div class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-default bg-input/50 px-3 py-2.5">
                      <span class="text-sm text-primary">
                        <span class="text-muted">Sélection :</span>
                        <strong class="ml-1">{{ employeeLabel(draftEmployeeId()) }}</strong>
                      </span>
                      <button type="button" (click)="beginRepickDetailEmployee()" class="text-xs font-medium text-[var(--electric-blue)] hover:opacity-80">Changer</button>
                    </div>
                  }
                  <input
                    type="search"
                    class="w-full rounded-lg border border-default bg-card px-3 py-2.5 text-sm text-primary placeholder:text-muted"
                    placeholder="Rechercher un employé (nom, rôle, e-mail)…"
                    [value]="detailEmpSearch()"
                    (input)="detailEmpSearch.set($any($event.target).value)"
                  />
                  <ul class="max-h-56 overflow-y-auto rounded-lg border border-default bg-input/40 divide-y divide-default">
                    @for (e of filteredDetailAssignables(); track e.id) {
                      <li>
                        <button type="button" (click)="pickDetailEmployee(e.id)" class="w-full flex items-center gap-3 px-3 py-2.5 text-left text-sm hover:bg-input/60 transition-colors">
                          <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--info-bg)] text-xs font-semibold text-[var(--info-text)]">{{ employeeInitials(e) }}</span>
                          <span class="min-w-0">
                            <span class="block font-medium text-primary truncate">{{ e.firstName }} {{ e.lastName }}</span>
                            <span class="block text-xs text-muted truncate">{{ e.role }}@if (employeeOrgHint(e.id); as hint) { · {{ hint }} }</span>
                          </span>
                        </button>
                      </li>
                    } @empty {
                      <li class="px-3 py-4 text-sm text-muted">Aucun résultat</li>
                    }
                  </ul>
                  <div class="flex flex-wrap gap-3 pt-2">
                    <button type="button" (click)="saveManager(dept.id)" [disabled]="saving() || !draftEmployeeId()" class="ky-btn-primary inline-flex items-center justify-center gap-2 text-sm font-medium disabled:opacity-50">
                      <app-lucide-icon [icon]="icons.check" className="w-4 h-4" /> Enregistrer
                    </button>
                    <button type="button" (click)="clearManager(dept.id)" [disabled]="saving() || !dept.managerEmployeeId" class="inline-flex items-center justify-center gap-2 rounded-lg border border-default px-4 py-2.5 text-sm text-muted hover:bg-input disabled:opacity-50">
                      <app-lucide-icon [icon]="icons.trash" className="w-4 h-4" /> Retirer le manager
                    </button>
                  </div>
                </div>

                <div class="space-y-3 mt-8 pt-6 border-t border-default">
                  <label class="text-sm font-medium text-muted block">Collaborateurs</label>
                    @if (!dept.managerEmployeeId) {
                      <p class="text-sm text-[var(--warning-text)]">Affectez d’abord un manager.</p>
                    }
                    @if (draftMemberId()) {
                      <div class="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-default bg-input/50 px-3 py-2.5">
                        <span class="text-sm text-primary">
                          <span class="text-muted">Sélection :</span>
                          <strong class="ml-1">{{ employeeLabel(draftMemberId()) }}</strong>
                        </span>
                        <button type="button" (click)="draftMemberId.set('')" class="text-xs font-medium text-indigo-400 hover:text-indigo-300">Changer</button>
                      </div>
                    }
                    <input
                      type="search"
                      class="w-full rounded-lg border border-default bg-card px-3 py-2.5 text-sm text-primary placeholder:text-muted"
                      placeholder="Rechercher un employé…"
                      [value]="memberSearch()"
                      (input)="memberSearch.set($any($event.target).value)"
                      [disabled]="!dept.managerEmployeeId"
                    />
                    <ul class="max-h-40 overflow-y-auto rounded-lg border border-default bg-input/40 divide-y divide-default">
                      @for (e of filteredMemberCandidates(); track e.id) {
                        <li>
                          <button type="button" (click)="pickMemberEmployee(e.id)" [disabled]="!dept.managerEmployeeId" class="w-full flex items-center gap-3 px-3 py-2 text-left text-sm hover:bg-input/60 disabled:opacity-40">
                            <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-input/40 text-[11px] font-semibold text-primary">{{ employeeInitials(e) }}</span>
                            <span class="min-w-0">
                              <span class="block font-medium text-primary truncate">{{ e.firstName }} {{ e.lastName }}</span>
                              <span class="block text-xs text-muted truncate">{{ e.role }}@if (employeeOrgHint(e.id); as hint) { · {{ hint }} }</span>
                            </span>
                          </button>
                        </li>
                      } @empty {
                        <li class="px-3 py-3 text-sm text-muted">Aucun résultat</li>
                      }
                    </ul>
                    <button type="button" (click)="addSelectedMember(dept)" [disabled]="saving() || !dept.managerEmployeeId || !draftMemberId()" class="w-full inline-flex items-center justify-center gap-2 rounded-lg bg-input px-4 py-2.5 text-sm font-medium text-primary hover:bg-input disabled:opacity-50">
                      <app-lucide-icon [icon]="icons.check" className="w-4 h-4" /> Ajouter à l’équipe
                    </button>
                  </div>
              } @else {
                <div class="flex flex-1 flex-col items-center justify-center rounded-xl border border-dashed border-default/35 bg-card/20 px-8 py-14 text-center min-h-[18rem]">
                  <p class="text-sm text-muted max-w-md leading-relaxed">Sélectionnez un département dans la liste pour afficher le formulaire d’affectation.</p>
                </div>
              }
            </div>
          </app-prime-card>

          <div class="min-w-0 space-y-4 xl:space-y-5 flex flex-col">
            <app-prime-card className="p-0" title="Indicateurs" description="Périmètre du département sélectionné." [hasAction]="false">
              @if (selectedDept(); as dept) {
                <div class="space-y-4 p-4 sm:p-6 -mt-1">
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div class="rounded-lg border border-default bg-input/50 px-3 py-3">
                      <p class="text-[11px] uppercase tracking-wide text-muted">Effectif</p>
                      <p class="text-2xl font-semibold text-primary tabular-nums">{{ teamMembers().length }}</p>
                    </div>
                    <div class="rounded-lg border border-default bg-input/50 px-3 py-3">
                      <p class="text-[11px] uppercase tracking-wide text-muted">Manager</p>
                      <p class="text-sm font-medium text-primary truncate">{{ employeeLabel(dept.managerEmployeeId) }}</p>
                    </div>
                  </div>
                </div>
              }
            </app-prime-card>

            <app-prime-card className="p-0 flex flex-col max-h-[min(42vh,22rem)]" title="Aperçu du périmètre" description="Membres du département sélectionné." [hasAction]="false">
              <div class="-m-6 flex-1 min-h-0 overflow-y-auto p-4">
                <ul class="space-y-2">
                  @for (m of teamMembers(); track m.id) {
                    <li class="flex items-center gap-3 rounded-lg border border-default/80 bg-card/40 px-3 py-2">
                      <span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-indigo-500/25 text-xs font-semibold text-indigo-100">{{ memberInitials(m) }}</span>
                      <span class="min-w-0 flex-1">
                        <span class="block text-sm font-medium text-primary truncate">{{ m.firstName }} {{ m.lastName }}</span>
                        <span class="block text-xs text-muted truncate">{{ m.role }}</span>
                      </span>
                      @if (m.id !== selectedDept()?.managerEmployeeId) {
                        <button type="button" (click)="removeFromTeam(m)" [disabled]="saving()" class="shrink-0 text-xs text-red-400 hover:text-red-300 disabled:opacity-50">Retirer</button>
                      }
                    </li>
                  } @empty {
                    <li class="text-sm text-muted py-4 text-center">Aucun employé dans ce périmètre.</li>
                  }
                </ul>
              </div>
            </app-prime-card>
          </div>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .dept-page {
        display: grid;
        gap: 1rem;
      }
      .dept-toolbar {
        padding: 0.9rem 1rem;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        flex-wrap: wrap;
      }
      .dept-toolbar label {
        margin: 0;
      }
      .dept-tab--active {
        background: var(--ky-gradient, var(--soft-blue));
        color: #f1f5f9;
        box-shadow: 0 0 0 1px var(--soft-blue) inset;
      }
      .dept-flash--ok {
        border-color: var(--success-border);
        background: var(--success-bg);
        color: var(--success-text);
      }
      .dept-flash--err {
        border-color: var(--danger-border);
        background: var(--danger-bg);
        color: var(--danger-text);
      }
      .dept-page :is(.ky-btn-primary, .ky-btn-secondary) {
        white-space: nowrap;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BusinessDepartmentsPageComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly confirm = inject(KyntusConfirmService);

  readonly icons = { refresh: RefreshCw, plus: Plus, check: Check, trash: Trash2, users: Users, building: Building2 };

  readonly pageTabs: { id: PageTab; label: string }[] = [
    { id: 'list', label: 'Liste des départements' },
    { id: 'assignments', label: 'Espaces d’affectation' },
  ];

  readonly activeTab = signal<PageTab>('list');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly search = signal('');
  readonly flash = signal('');
  readonly flashOk = signal(true);

  readonly departments = signal<BusinessDepartmentDto[]>([]);
  readonly employees = signal<DirectoryEmployeeDto[]>([]);

  readonly selectedId = signal<string | null>(null);
  readonly formCode = signal('');
  readonly formName = signal('');

  readonly draftEmployeeId = signal('');
  readonly draftMemberId = signal('');
  readonly detailEmpSearch = signal('');
  readonly memberSearch = signal('');

  readonly filteredDepartments = computed(() => {
    const q = this.search().trim().toLowerCase();
    const rows = this.departments().filter((d) => d.isActive && d.kind === 'Support');
    if (!q) return rows;
    return rows.filter(
      (d) =>
        d.code.toLowerCase().includes(q) ||
        d.name.toLowerCase().includes(q) ||
        d.kind.toLowerCase().includes(q),
    );
  });

  readonly selectedDept = computed(() => {
    const id = this.selectedId();
    return id ? this.departments().find((d) => d.id === id) ?? null : null;
  });

  readonly assignableEmployees = computed(() =>
    employeesForOrgAssignmentSelect(this.mapEmployeesForSelect(this.employees())),
  );

  readonly filteredDetailAssignables = computed(() => {
    const q = this.detailEmpSearch().trim().toLowerCase();
    const list = this.assignableEmployees();
    if (!q) return list.slice(0, 40);
    return list
      .filter(
        (e) =>
          `${e.firstName} ${e.lastName}`.toLowerCase().includes(q) ||
          e.role.toLowerCase().includes(q) ||
          (e.email ?? '').toLowerCase().includes(q),
      )
      .slice(0, 40);
  });

  readonly teamMembers = computed((): DirectoryEmployeeDto[] => {
    const dept = this.selectedDept();
    if (!dept) return [];
    return this.employees()
      .filter((e) => e.businessDepartmentId === dept.id)
      .sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`, 'fr'));
  });

  readonly filteredMemberCandidates = computed(() => {
    const dept = this.selectedDept();
    if (!dept) return [];
    const q = this.memberSearch().trim().toLowerCase();
    const inDept = new Set(this.teamMembers().map((e) => e.id));
    const managerId = dept.managerEmployeeId;
    let list = this.assignableEmployees().filter((e) => !inDept.has(e.id) && e.id !== managerId);
    if (!q) return list.slice(0, 25);
    return list
      .filter(
        (e) =>
          `${e.firstName} ${e.lastName}`.toLowerCase().includes(q) ||
          e.role.toLowerCase().includes(q),
      )
      .slice(0, 25);
  });

  ngOnInit(): void {
    void this.reload();
  }

  canCreate(): boolean {
    return !!this.formCode().trim() && !!this.formName().trim();
  }

  employeeLabel(id?: string | null): string {
    if (!id) return '—';
    const e = this.employees().find((x) => x.id === id);
    return e ? `${e.firstName} ${e.lastName}` : id;
  }

  /** Département Support actuel de l'employé (ex. IT — Informatique), vide si opérationnel. */
  employeeOrgHint(employeeId: string): string {
    const raw = this.employees().find((e) => e.id === employeeId);
    return employeeSupportDepartmentLabel(raw, this.departments()) ?? '';
  }

  teamCount(d: BusinessDepartmentDto): number {
    return this.employees().filter((e) => e.businessDepartmentId === d.id).length;
  }

  employeeInitials(e: Pick<Employee, 'firstName' | 'lastName'>): string {
    return `${e.firstName?.[0] ?? ''}${e.lastName?.[0] ?? ''}`.toUpperCase() || '?';
  }

  memberInitials(m: DirectoryEmployeeDto): string {
    return `${m.firstName?.[0] ?? ''}${m.lastName?.[0] ?? ''}`.toUpperCase() || '?';
  }

  pickDetailEmployee(id: string): void {
    this.draftEmployeeId.set(id);
  }

  beginRepickDetailEmployee(): void {
    this.draftEmployeeId.set('');
  }

  pickMemberEmployee(id: string): void {
    this.draftMemberId.set(id);
  }

  selectDepartment(d: BusinessDepartmentDto): void {
    this.selectedId.set(d.id);
    this.draftEmployeeId.set(d.managerEmployeeId ?? '');
    this.draftMemberId.set('');
    this.detailEmpSearch.set('');
    this.memberSearch.set('');
  }

  openAssignments(deptId: string): void {
    const dept = this.departments().find((d) => d.id === deptId);
    if (dept) this.selectDepartment(dept);
    this.activeTab.set('assignments');
  }

  async reload(): Promise<void> {
    this.loading.set(true);
    try {
      const [depts, emps] = await Promise.all([
        firstValueFrom(this.http.get<BusinessDepartmentDto[]>('/api/directory/business-departments')),
        firstValueFrom(this.http.get<DirectoryEmployeeDto[]>('/api/directory/employees')),
      ]);
      this.departments.set(depts);
      this.employees.set(emps);
      const supportDepts = depts.filter((d) => d.isActive && d.kind === 'Support');
      const sel = this.selectedId();
      if (!sel && supportDepts.length) {
        this.selectDepartment(supportDepts[0]);
      } else if (sel) {
        const dept = supportDepts.find((d) => d.id === sel);
        if (dept) this.draftEmployeeId.set(dept.managerEmployeeId ?? '');
        else if (supportDepts.length) this.selectDepartment(supportDepts[0]);
      }
    } catch {
      this.notify('Impossible de charger les données.', false);
    } finally {
      this.loading.set(false);
    }
  }

  async create(): Promise<void> {
    if (!this.canCreate()) return;
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.http.post('/api/directory/business-departments', {
          code: this.formCode().trim(),
          name: this.formName().trim(),
          kind: 'Support',
        }),
      );
      this.formCode.set('');
      this.formName.set('');
      this.notify('Département créé.', true);
      await this.reload();
    } catch {
      this.notify('Erreur lors de la création.', false);
    } finally {
      this.saving.set(false);
    }
  }

  async saveManager(deptId: string): Promise<void> {
    const employeeId = this.draftEmployeeId().trim();
    if (!employeeId) return;
    const dept = this.departments().find((d) => d.id === deptId);
    const current = dept?.managerEmployeeId;
    if (current && current !== employeeId) {
      const ok = await this.confirm.confirm({
        title: 'Remplacer le manager ?',
        message: `Le manager actuel (${this.employeeLabel(current)}) sera remplacé par ${this.employeeLabel(employeeId)}. Le nouveau titulaire recevra le rôle Manager.`,
        confirmLabel: 'Remplacer',
      });
      if (!ok) return;
    }
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.http.post(`/api/directory/business-departments/${deptId}/manager`, { employeeId }),
      );
      this.notify('Manager enregistré — rôle Manager appliqué automatiquement.', true);
      await this.reload();
    } catch {
      this.notify('Erreur lors de l’affectation du manager.', false);
    } finally {
      this.saving.set(false);
    }
  }

  async clearManager(deptId: string): Promise<void> {
    this.saving.set(true);
    try {
      await firstValueFrom(this.http.delete(`/api/directory/business-departments/${deptId}/manager`));
      this.draftEmployeeId.set('');
      this.notify('Manager retiré.', true);
      await this.reload();
    } catch {
      this.notify('Erreur.', false);
    } finally {
      this.saving.set(false);
    }
  }

  async addSelectedMember(dept: BusinessDepartmentDto): Promise<void> {
    const id = this.draftMemberId().trim();
    if (!id || !dept.managerEmployeeId) return;
    const emp = this.assignableEmployees().find((e) => e.id === id);
    if (!emp) return;
    await this.addToTeam(dept, emp);
    this.draftMemberId.set('');
  }

  async addToTeam(dept: BusinessDepartmentDto, emp: Employee): Promise<void> {
    if (!dept.managerEmployeeId) return;
    const raw = this.employees().find((e) => e.id === emp.id);
    if (!raw) return;
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.http.put(`/api/directory/employees/${emp.id}`, {
          firstName: raw.firstName,
          lastName: raw.lastName,
          email: raw.email,
          role: raw.role,
          serviceId: null,
          parentId: dept.managerEmployeeId,
          isActive: true,
          hireDate: null,
          businessDepartmentId: dept.id,
        }),
      );
      this.memberSearch.set('');
      this.notify(`${emp.firstName} ${emp.lastName} ajouté(e) à l’équipe.`, true);
      await this.reload();
    } catch {
      this.notify('Erreur lors du rattachement.', false);
    } finally {
      this.saving.set(false);
    }
  }

  async removeFromTeam(emp: DirectoryEmployeeDto): Promise<void> {
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.http.put(`/api/directory/employees/${emp.id}`, {
          firstName: emp.firstName,
          lastName: emp.lastName,
          email: emp.email,
          role: emp.role,
          serviceId: emp.serviceId ?? null,
          parentId: emp.parentId ?? null,
          isActive: true,
          hireDate: null,
          businessDepartmentId: null,
        }),
      );
      this.notify('Collaborateur retiré du département.', true);
      await this.reload();
    } catch {
      this.notify('Erreur lors du retrait.', false);
    } finally {
      this.saving.set(false);
    }
  }

  private mapEmployeesForSelect(list: DirectoryEmployeeDto[]): Employee[] {
    return list.map((e) => ({
      id: e.id,
      firstName: e.firstName,
      lastName: e.lastName,
      role: e.role as Employee['role'],
      parentId: e.parentId ?? undefined,
      serviceId: e.serviceId ?? '',
      poleId: e.poleId ?? '',
      celluleId: e.celluleId ?? '',
      email: e.email,
    }));
  }

  private notify(msg: string, ok: boolean): void {
    this.flash.set(msg);
    this.flashOk.set(ok);
  }
}
