import {
  ChangeDetectionStrategy,
  Component,
  Input,
  inject,
  signal,
  effect,
} from '@angular/core';
import { PrimeCardComponent } from '../../components/prime-card.component';
import { RpPrimeService } from '../../services/rp-prime.service';
import type { RpValidationItem } from '../../services/rp-prime.service';
import { HierarchyDrillService } from '../../state/hierarchy-drill.service';
import { LucideIconComponent } from '../../../shared/lucide-icon.component';
import { Check as CheckIcon, X as XIcon } from 'lucide';
import { cn } from '@/lib/utils';

@Component({
  selector: 'app-rp-final-validation',
  standalone: true,
  imports: [PrimeCardComponent, LucideIconComponent],
  template: `
    @if (loading()) {
      <div class="flex justify-center py-8">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
      </div>
    } @else {
      <div class="space-y-6">
        <div>
          <h2 class="text-2xl font-bold text-white tracking-tight">Validation finale</h2>
          <p class="text-slate-400 mt-1">
            Primes deja validees par le manager, en attente de decision RP.
          </p>
        </div>

        <app-prime-card title="Validations manager">
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-default">
                  <th class="text-left py-3 text-slate-400 font-medium">Employe</th>
                  <th class="text-left py-3 text-slate-400 font-medium">Projet</th>
                  <th class="text-left py-3 text-slate-400 font-medium">Score performance</th>
                  <th class="text-left py-3 text-slate-400 font-medium">Statut</th>
                  <th class="text-right py-3 text-slate-400 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr class="border-b border-default/60">
                    <td class="py-3 text-slate-200">{{ item.employeeName }}</td>
                    <td class="py-3 text-slate-300">{{ item.projectName }}</td>
                    <td class="py-3 text-cyan-300 font-semibold">{{ item.performanceScore }}%</td>
                    <td class="py-3">
                      <span [class]="statusBadgeClass(item.status)">
                        {{ item.status }}
                      </span>
                    </td>
                    <td class="py-3">
                      <div class="flex justify-end gap-2">
                        @if (item.status === 'Manager Approved') {
                          <button
                            type="button"
                            (click)="onApprove(item.id)"
                            class="p-2 rounded-md border border-emerald-500/40 text-emerald-300 hover:bg-emerald-500/10"
                            title="Approuver"
                          >
                            <app-lucide-icon [icon]="icons.check" className="w-4 h-4" />
                          </button>
                          <button
                            type="button"
                            (click)="onReject(item.id)"
                            class="p-2 rounded-md border border-rose-500/40 text-rose-300 hover:bg-rose-500/10"
                            title="Rejeter"
                          >
                            <app-lucide-icon [icon]="icons.x" className="w-4 h-4" />
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
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
export class RpFinalValidationComponent {
  @Input({ required: true }) rpUserId!: string;
  readonly hierarchy = inject(HierarchyDrillService);

  readonly items = signal<RpValidationItem[]>([]);
  readonly loading = signal(true);

  readonly icons = { check: CheckIcon, x: XIcon };

  constructor() {
    effect(() => {
      void this.rpUserId;
      const drill = this.hierarchy.drill();
      void drill.managerId;
      void drill.coachId;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    RpPrimeService.getManagerValidatedPrimes(this.rpUserId, this.hierarchy.drill()).then((data) => {
      this.items.set(data);
      this.loading.set(false);
    });
  }

  async onApprove(id: string): Promise<void> {
    await RpPrimeService.updateRpValidationStatus(
      this.rpUserId,
      this.hierarchy.drill(),
      id,
      'RP Approved',
    );
    this.load();
  }

  async onReject(id: string): Promise<void> {
    await RpPrimeService.updateRpValidationStatus(
      this.rpUserId,
      this.hierarchy.drill(),
      id,
      'Rejected',
    );
    this.load();
  }

  statusBadgeClass(status: string): string {
    return cn(
      'inline-flex px-2.5 py-1 rounded-full text-xs font-medium',
      status === 'RP Approved'
        ? 'bg-emerald-500/15 text-emerald-300'
        : status === 'Rejected'
          ? 'bg-rose-500/15 text-rose-300'
          : 'bg-amber-500/15 text-amber-300',
    );
  }
}
