import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AllowanceApiService, AllowanceRequestDto } from '../../services/allowance-api.service';
import { AllowanceStatusBadgeComponent } from '../../components/allowances/allowance-status-badge.component';
import { AllowancesPageShellComponent } from '../../components/allowances/allowances-page-shell.component';
import { PrimeCardComponent } from '../../components/prime-card.component';

@Component({
  selector: 'app-allowances-my-page',
  standalone: true,
  imports: [CommonModule, AllowanceStatusBadgeComponent, AllowancesPageShellComponent, PrimeCardComponent],
  template: `
    <app-allowances-page-shell
      title="Mes primes reçues"
      subtitle="Primes Support qui vous ont été attribuées."
    >
      @if (loading()) {
        <div class="flex justify-center py-12">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-500"></div>
        </div>
      } @else if (rows().length === 0) {
        <app-prime-card title="Aucune prime">
          <p class="text-sm text-muted">Aucune prime Support enregistrée pour le moment.</p>
        </app-prime-card>
      } @else {
        <ul class="space-y-3">
          @for (r of rows(); track r.id) {
            <app-prime-card className="ky-card--compact">
              <div class="flex flex-wrap justify-between gap-2 items-center text-sm">
                <div>
                  <p class="font-medium text-primary">{{ r.typeLabel }}</p>
                  <p class="text-muted">{{ r.period }}</p>
                </div>
                <span class="flex items-center gap-2">
                  <span class="text-primary">{{ r.amount | number:'1.0-2' }} MAD</span>
                  <app-allowance-status-badge [status]="r.status" />
                </span>
              </div>
            </app-prime-card>
          }
        </ul>
      }
    </app-allowances-page-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesMyPageComponent implements OnInit {
  private readonly api = inject(AllowanceApiService);
  readonly loading = signal(true);
  readonly rows = signal<AllowanceRequestDto[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.rows.set(await this.api.listRequests());
    } finally {
      this.loading.set(false);
    }
  }
}
