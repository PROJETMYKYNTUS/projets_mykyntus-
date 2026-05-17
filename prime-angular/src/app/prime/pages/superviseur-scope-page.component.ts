import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
} from '@angular/core';
import { PrimeCardComponent } from '../components/prime-card.component';
import { PrimeService } from '../services/prime.service';
import { RoleService } from '../state/role.service';
import type { Employee } from '../models';

interface ScopeRow {
  id: string;
  fullName: string;
  role: string;
  service: string;
  cellule: string;
  pole: string;
}

@Component({
  selector: 'app-superviseur-scope-page',
  standalone: true,
  imports: [PrimeCardComponent],
  template: `
    @if (loading()) {
      <div class="p-8 flex justify-center">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
      </div>
    } @else {
      <div class="p-8 space-y-6 bg-navy-950 min-h-full">
        <div>
          <h1 class="text-3xl font-bold text-white tracking-tight">Périmètre Superviseur</h1>
          <p class="text-slate-400 mt-1">
            Vue des référents techniques et pilotes rattachés au superviseur courant (même périmètre qu’avant « coachs »).
          </p>
        </div>

        <app-prime-card title="Équipe supervisée" className="p-0">
          <div class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="text-xs text-slate-400 uppercase bg-navy-900 border-b border-navy-800">
                <tr>
                  <th class="px-6 py-3 font-medium tracking-wider">Collaborateur</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Rôle</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Pôle</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Cellule</th>
                  <th class="px-6 py-3 font-medium tracking-wider">Service</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-navy-800">
                @if (rows().length === 0) {
                  <tr>
                    <td colspan="5" class="px-6 py-8 text-center text-slate-500">Aucune donnée.</td>
                  </tr>
                } @else {
                  @for (item of rows(); track item.id) {
                    <tr class="bg-navy-900 hover:bg-navy-800 transition-colors">
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <span class="font-medium text-slate-900">{{ item.fullName }}</span>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <span class="text-slate-700">{{ item.role }}</span>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <span class="text-slate-700">{{ item.pole }}</span>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <span class="text-slate-700">{{ item.cellule }}</span>
                      </td>
                      <td class="px-6 py-4 whitespace-nowrap text-slate-200">
                        <span class="text-slate-700">{{ item.service }}</span>
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
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuperviseurScopePageComponent {
  private readonly roleService = inject(RoleService);

  readonly rows = signal<ScopeRow[]>([]);
  readonly loading = signal(true);

  constructor() {
    effect(() => {
      void this.roleService.currentUser().id;
      this.fetch();
    });
  }

  private fetch(): void {
    this.loading.set(true);
    const userId = this.roleService.currentUser().id;
    void Promise.all([PrimeService.getEmployees(), PrimeService.getDepartments()]).then(
      ([employees, departments]) => {
        const directReferents = employees.filter(
          (e) =>
            (e.role === 'Référent technique' || e.role === 'Coach') && e.parentId === userId,
        );
        const referentIds = new Set(directReferents.map((c) => c.id));
        const scopeEmployees = employees.filter(
          (e) =>
            e.id === userId ||
            referentIds.has(e.id) ||
            (e.role === 'Pilote' && e.parentId !== undefined && referentIds.has(e.parentId)),
        );

        const deptById = new Map(departments.map((d) => [d.id, d]));
        const poleLabel = (employee: Employee) => {
          const dept = deptById.get(employee.departementId ?? employee.poleId);
          const pole = dept?.poles.find((p) => p.id === employee.poleId);
          return pole?.name ?? employee.poleId;
        };
        const celluleLabel = (employee: Employee) => {
          const dept = deptById.get(employee.departementId ?? employee.poleId);
          const pole = dept?.poles.find((p) => p.id === employee.poleId);
          const cellule = pole?.cells.find((c) => c.id === employee.celluleId);
          return cellule?.name ?? employee.celluleId;
        };

        const mapped: ScopeRow[] = scopeEmployees.map((e) => ({
          id: e.id,
          fullName: `${e.firstName} ${e.lastName}`,
          role: e.role,
          service: e.serviceId ?? '—',
          cellule: celluleLabel(e),
          pole: poleLabel(e),
        }));

        mapped.sort((a, b) => a.fullName.localeCompare(b.fullName));
        this.rows.set(mapped);
        this.loading.set(false);
      },
    );
  }
}
