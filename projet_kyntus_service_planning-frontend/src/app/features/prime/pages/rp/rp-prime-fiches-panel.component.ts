import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { PrimeCardComponent } from '../../components/prime-card.component';
import {
  PrimeFicheResultService,
  type EmployeePrimeServiceFicheValidationDto,
  type WorkflowValidationSummaryDto,
} from '../../services/prime-fiche-result.service';

@Component({
  selector: 'app-rp-prime-fiches-panel',
  standalone: true,
  imports: [PrimeCardComponent],
  template: `
    <div class="space-y-6">
      <app-prime-card title="Synthèse validation fiches (pôle)">
        @if (summary(); as s) {
          <div class="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
            @for (row of s.statusCounts; track row.status) {
              <div class="rounded-lg border border-navy-800 bg-navy-900/60 p-3">
                <div class="text-xs text-slate-500 uppercase">{{ row.status }}</div>
                <div class="text-2xl font-bold text-slate-100">{{ row.count }}</div>
              </div>
            }
          </div>
          <p class="text-xs text-slate-500 mt-2">Total : {{ s.total }} fiches sur la période sélectionnée.</p>
        } @else {
          <p class="text-slate-500 text-sm">Chargement…</p>
        }
      </app-prime-card>
      <app-prime-card title="Fiches en cours">
        @if (fiches().length === 0) {
          <p class="text-slate-500 text-sm">Aucune fiche.</p>
        } @else {
          <div class="overflow-x-auto text-sm">
            <table class="w-full text-left">
              <thead class="text-xs text-slate-500 border-b border-navy-800">
                <tr>
                  <th class="py-2">Pilote</th>
                  <th class="py-2">Statut</th>
                  <th class="py-2">Période</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-navy-800">
                @for (f of fiches(); track f.id) {
                  <tr>
                    <td class="py-2 text-slate-200">{{ f.employeeId }}</td>
                    <td class="py-2">{{ f.validationStatus }}</td>
                    <td class="py-2 font-mono">{{ f.period }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </app-prime-card>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RpPrimeFichesPanelComponent {
  readonly rpUserId = input.required<string>();
  private readonly api = inject(PrimeFicheResultService);

  readonly summary = signal<WorkflowValidationSummaryDto | null>(null);
  readonly fiches = signal<EmployeePrimeServiceFicheValidationDto[]>([]);

  constructor() {
    effect(() => {
      const id = this.rpUserId();
      void id;
      this.api.summary({ userId: id, role: 'Chef de projet' }).subscribe((s) => this.summary.set(s));
      this.api.list({ userId: id, role: 'Chef de projet' }).subscribe((rows) => this.fiches.set(rows));
    });
  }
}
