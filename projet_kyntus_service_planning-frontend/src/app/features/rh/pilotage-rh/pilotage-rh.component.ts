import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
import { KyntusEmptyStateComponent } from '../../../shared/components/ui/kyntus-empty-state.component';
import { KyntusErrorStateComponent } from '../../../shared/components/ui/kyntus-error-state.component';
import {
  DirectoryEmployeeApiService,
  type PilotRotationSort,
  type PilotRotationSummaryDto,
} from '../../../core/directory/directory-employee-api.service';
import { PrimeOrgApiService } from '../../prime/services/prime-org-api.service';
import { UserService } from '../../users/services/user.service';
import type { User } from '../../users/users-module';

type ServiceOption = { id: string; name: string };

@Component({
  selector: 'app-pilotage-rh',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    KyntusPageHeaderComponent,
    KyntusEmptyStateComponent,
    KyntusErrorStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="ky-page-shell">
      <app-kyntus-page-header
        title="Historique rotations"
        subtitle="Parcours des pilotes par service — filtres par poste, rotations et tri"
      />

      <div class="card-navy p-4">
        <div class="flex flex-wrap items-end gap-4">
          <label class="text-xs text-muted space-y-1 block min-w-[220px] flex-1">
            Service (poste)
            <select class="ky-input w-full" [(ngModel)]="serviceId" (ngModelChange)="onFilterChange()">
              <option value="">Tous les services</option>
              @for (s of services(); track s.id) {
                <option [value]="s.id">{{ s.name }}</option>
              }
            </select>
          </label>
          <label class="text-xs text-muted space-y-1 block w-[140px]">
            Min. rotations
            <input
              class="ky-input w-full"
              type="number"
              min="0"
              [(ngModel)]="minRotations"
              (ngModelChange)="onFilterChange()"
              placeholder="0"
            />
          </label>
          <label class="text-xs text-muted space-y-1 block min-w-[200px]">
            Tri
            <select class="ky-input w-full" [(ngModel)]="sort" (ngModelChange)="onFilterChange()">
              <option value="rotationCountDesc">Plus de rotations</option>
              <option value="rotationCountAsc">Moins de rotations</option>
              <option value="name">Nom A→Z</option>
            </select>
          </label>
          <div class="ml-auto flex items-end gap-2">
            <button type="button" class="ky-btn-secondary text-sm" (click)="resetFilters()">
              Réinitialiser
            </button>
            <button
              type="button"
              class="ky-btn-primary text-sm"
              [class.ky-btn-loading]="loading()"
              [disabled]="loading()"
              (click)="load()"
            >
              Appliquer
            </button>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="card-navy p-4 space-y-3" aria-busy="true" aria-label="Chargement de l’historique">
          <div class="ky-skeleton ky-skeleton-title"></div>
          <div class="ky-skeleton ky-skeleton-text"></div>
          <div class="ky-skeleton ky-skeleton-text"></div>
          <div class="ky-skeleton ky-skeleton-text"></div>
          <div class="ky-skeleton ky-skeleton-text"></div>
        </div>
      } @else if (error()) {
        <app-kyntus-error-state [message]="error()!" retryLabel="Réessayer" (retry)="load()" />
      } @else if (rows().length === 0) {
        <app-kyntus-empty-state
          title="Aucun pilote"
          description="Aucun pilote ne correspond aux filtres sélectionnés. Élargissez la recherche ou réinitialisez les filtres."
        >
          <button type="button" class="ky-btn-secondary text-sm mt-2" (click)="resetFilters()">
            Réinitialiser les filtres
          </button>
        </app-kyntus-empty-state>
      } @else {
        <div class="card-navy overflow-x-auto ky-fade-up">
          <table class="prime-table prime-table--dense">
            <thead>
              <tr>
                <th>Pilote</th>
                <th>Email</th>
                <th>Service actuel</th>
                <th class="text-right">Rotations</th>
                <th>Dernière rotation</th>
                <th class="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track row.employeeId) {
                <tr>
                  <td class="font-medium">{{ row.lastName }} {{ row.firstName }}</td>
                  <td><span class="prime-cell-muted">{{ row.email }}</span></td>
                  <td>{{ row.currentServiceName || '—' }}</td>
                  <td class="text-right tabular-nums font-semibold">{{ row.rotationCount }}</td>
                  <td>{{ formatDate(row.lastEffectiveFrom) }}</td>
                  <td class="text-right">
                    <button
                      type="button"
                      class="ky-btn-secondary text-xs"
                      [disabled]="!planningUserId(row.employeeId)"
                      [title]="
                        planningUserId(row.employeeId)
                          ? 'Modifier la fiche employé'
                          : 'Employé introuvable dans Planning'
                      "
                      (click)="editEmployee(row)"
                    >
                      Modifier
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="text-xs text-muted">{{ rows().length }} pilote(s)</p>
      }
    </section>
  `,
})
export class PilotageRhComponent implements OnInit {
  private readonly api = inject(DirectoryEmployeeApiService);
  private readonly orgApi = inject(PrimeOrgApiService);
  private readonly users = inject(UserService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly rows = signal<PilotRotationSummaryDto[]>([]);
  readonly services = signal<ServiceOption[]>([]);

  private usersByGuid = new Map<string, User>();
  private filterTimer: ReturnType<typeof setTimeout> | null = null;

  serviceId = '';
  minRotations: number | null = null;
  sort: PilotRotationSort = 'rotationCountDesc';

  ngOnInit(): void {
    void this.bootstrap();
  }

  onFilterChange(): void {
    if (this.filterTimer) clearTimeout(this.filterTimer);
    this.filterTimer = setTimeout(() => void this.load(), 350);
  }

  resetFilters(): void {
    this.serviceId = '';
    this.minRotations = null;
    this.sort = 'rotationCountDesc';
    void this.load();
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('fr-FR');
  }

  planningUserId(employeeGuid: string): number | null {
    return this.usersByGuid.get(employeeGuid.trim().toLowerCase())?.id ?? null;
  }

  editEmployee(row: PilotRotationSummaryDto): void {
    const id = this.planningUserId(row.employeeId);
    if (id == null) return;
    void this.router.navigate(['/users', 'edit', id]);
  }

  private async bootstrap(): Promise<void> {
    try {
      const [overview, userList] = await Promise.all([
        firstValueFrom(this.orgApi.loadOverview()),
        firstValueFrom(this.users.getAllUsers()).catch(() => [] as User[]),
      ]);

      const leafServices = (overview.sousServices ?? []).map((s) => ({ id: s.id, name: s.name }));
      const midServices = (overview.services ?? []).map((s) => ({ id: s.id, name: s.name }));
      const byId = new Map<string, ServiceOption>();
      for (const s of [...leafServices, ...midServices]) {
        if (s.id?.trim()) byId.set(s.id, s);
      }
      this.services.set([...byId.values()].sort((a, b) => a.name.localeCompare(b.name, 'fr')));

      this.usersByGuid = new Map(
        (userList ?? [])
          .filter((u) => !!u.guid?.trim())
          .map((u) => [u.guid.trim().toLowerCase(), u] as const),
      );
    } catch {
      this.services.set([]);
    }
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const rows = await firstValueFrom(
        this.api.listPilotRotations({
          serviceId: this.serviceId || undefined,
          minRotations: this.minRotations ?? undefined,
          sort: this.sort,
        }),
      );
      this.rows.set(rows ?? []);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Impossible de charger l’historique des rotations.');
      this.rows.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
