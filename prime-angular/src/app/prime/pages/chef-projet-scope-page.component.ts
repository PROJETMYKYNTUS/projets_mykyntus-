import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { PrimeCardComponent } from '../components/prime-card.component';
import { resolveEmployeeOrgLabels } from '../lib/org-display-labels';
import { PrimeService } from '../services/prime.service';
import { RoleService } from '../state/role.service';

interface ScopeRow {
  id: string;
  fullName: string;
  role: string;
  service: string;
  cellule: string;
  pole: string;
}

@Component({
  selector: 'app-chef-projet-scope-page',
  standalone: true,
  imports: [PrimeCardComponent],
  template: `
    @if (loading()) {
      <div class="p-8 flex justify-center">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
      </div>
    } @else {
      <div class="prime-page-shell">
        <div>
          <h1 class="prime-page-title">Périmètre Chef de projet</h1>
          <p class="prime-page-subtitle">
            Collaborateurs du même pôle que votre affectation (superviseurs, référents techniques, pilotes).
          </p>
        </div>

        <app-prime-card title="Vue pôle" className="p-0">
          <div class="overflow-x-auto">
            <table class="prime-table">
              <thead>
                <tr>
                  <th>Collaborateur</th>
                  <th>Rôle</th>
                  <th>Pôle</th>
                  <th>Cellule</th>
                  <th>Service</th>
                </tr>
              </thead>
              <tbody>
                @if (rows().length === 0) {
                  <tr>
                    <td colspan="5" class="text-center prime-cell-muted py-8">Aucune donnée.</td>
                  </tr>
                } @else {
                  @for (item of rows(); track item.id) {
                    <tr>
                      <td><span class="prime-cell-strong">{{ item.fullName }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.role }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.pole }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.cellule }}</span></td>
                      <td><span class="prime-cell-muted">{{ item.service }}</span></td>
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
export class ChefProjetScopePageComponent {
  private readonly roleService = inject(RoleService);

  readonly rows = signal<ScopeRow[]>([]);
  readonly loading = signal(true);

  constructor() {
    effect(() => {
      void this.roleService.currentUser().id;
      void this.roleService.currentRole();
      this.fetch();
    });
  }

  private fetch(): void {
    this.loading.set(true);
    const poleId = this.roleService.currentUser().poleId;
    void Promise.all([PrimeService.getEmployees(), PrimeService.getDepartments()]).then(([employees, departments]) => {
      const scopeEmployees = employees.filter((e) => e.poleId === poleId);

      const mapped: ScopeRow[] = scopeEmployees.map((e) => {
        const labels = resolveEmployeeOrgLabels(e, departments);
        return {
          id: e.id,
          fullName: `${e.firstName} ${e.lastName}`,
          role: e.role,
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
