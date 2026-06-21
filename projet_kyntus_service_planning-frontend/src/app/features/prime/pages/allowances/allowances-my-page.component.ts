import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AllowanceApiService, AllowanceRequestDto } from '../../services/allowance-api.service';
import { AllowanceStatusBadgeComponent } from '../../components/allowances/allowance-status-badge.component';

@Component({
  selector: 'app-allowances-my-page',
  standalone: true,
  imports: [CommonModule, AllowanceStatusBadgeComponent],
  template: `
    <div class="space-y-6">
      <h1 class="text-xl font-semibold text-primary">Mes primes Support</h1>
      @if (loading()) {
        <p class="text-muted text-sm">Chargement…</p>
      } @else if (rows().length === 0) {
        <p class="text-muted text-sm">Aucune prime enregistrée.</p>
      } @else {
        <ul class="space-y-2">
          @for (r of rows(); track r.id) {
            <li class="card-navy p-3 text-sm flex flex-wrap justify-between gap-2 items-center">
              <span>{{ r.typeLabel }} — {{ r.period }}</span>
              <span class="flex items-center gap-2">
                <span>{{ r.amount | number:'1.0-2' }} MAD</span>
                <app-allowance-status-badge [status]="r.status" />
              </span>
            </li>
          }
        </ul>
      }
    </div>
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
