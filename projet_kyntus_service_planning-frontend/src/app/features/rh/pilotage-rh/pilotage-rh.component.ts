import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { KyntusPageHeaderComponent } from '../../../shared/components/ui/kyntus-page-header.component';
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
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-kyntus-page-header
      title="Historique rotations"
      subtitle="Parcours des pilotes par service — filtres par poste, rotations et tri"
    />

    <div class="card-navy p-4 mx-1">
      <div class="flex flex-wrap items-end gap-3">
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
        <button type="button" class="ky-btn-secondary text-sm" (click)="resetFilters()">Réinitialiser</button>
        <button type="button" class="ky-btn-primary text-sm" [disabled]="loading()" (click)="load()">
          Appliquer
        </button>
      </div>
    </div>

    @if (loading()) {
      <p class="text-muted text-sm p-4">Chargement de l’historique…</p>
    } @else if (error()) {
      <p class="text-rose-400 text-sm p-4">{{ error() }}</p>
    } @else if (rows().length === 0) {
      <p class="text-muted text-sm p-4">Aucun pilote ne correspond aux filtres.</p>
    } @else {
      <div class="card-navy mx-1 mt-4 overflow-x-auto">
        <table class="w-full text-sm text-left">
          <thead class="text-xs uppercase text-muted border-b border-default">
            <tr>
              <th class="px-4 py-3 font-medium">Pilote</th>
              <th class="px-4 py-3 font-medium">Email</th>
              <th class="px-4 py-3 font-medium">Service actuel</th>
              <th class="px-4 py-3 font-medium text-right">Rotations</th>
              <th class="px-4 py-3 font-medium">Dernière rotation</th>
              <th class="px-4 py-3 font-medium text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.employeeId) {
              <tr class="border-b border-default/40 hover:bg-navy-950/40">
                <td class="px-4 py-3 text-primary font-medium whitespace-nowrap">
                  {{ row.lastName }} {{ row.firstName }}
                </td>
                <td class="px-4 py-3 text-muted">{{ row.email }}</td>
                <td class="px-4 py-3">{{ row.currentServiceName || '—' }}</td>
                <td class="px-4 py-3 text-right tabular-nums font-semibold">{{ row.rotationCount }}</td>
                <td class="px-4 py-3 whitespace-nowrap">{{ formatDate(row.lastEffectiveFrom) }}</td>
                <td class="px-4 py-3 text-right">
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
      <p class="text-xs text-muted px-4 py-2">{{ rows().length }} pilote(s)</p>
    }
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
