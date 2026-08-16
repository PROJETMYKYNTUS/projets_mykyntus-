import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';
import { CongeService } from '../../../../core/services/conge.service';
import { UserService } from '../../../users/services/user.service';
import { QuotaCongeServiceDto } from '../../../../core/models/conge.models';

@Component({
  selector: 'app-conge-quotas-service',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  template: `
    <div class="ky-page-shell">
      <app-kyntus-page-header
        title="Quotas congés (services)"
        subtitle="Nombre max d’employés absents le même jour par service. Seuls les congés validés superviseur (en attente RH) et validés RH comptent.">
        <div actions>
          <button type="button" class="ky-btn-secondary" (click)="load()">Actualiser</button>
        </div>
      </app-kyntus-page-header>

      <p class="hint" *ngIf="loading">Chargement…</p>
      <p class="hint" *ngIf="!loading && rows.length === 0">Aucun service dans votre périmètre.</p>

      <div class="table-wrap" *ngIf="!loading && rows.length > 0">
        <table class="prime-table">
          <thead>
            <tr>
              <th>Service</th>
              <th>Effectif</th>
              <th>Max absents / jour</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let r of rows">
              <td>{{ r.serviceNom }}</td>
              <td>{{ r.effectif }}</td>
              <td>
                <input class="ky-input" type="number" min="1" [(ngModel)]="edits[r.serviceId]"
                       [placeholder]="r.maxAbsentsSimultanes == null ? 'Non défini' : ''" />
              </td>
              <td>
                <button type="button" class="ky-btn-primary" (click)="save(r)">Enregistrer</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .hint { color: #64748b; margin-top: 1rem; }
    .table-wrap { margin-top: 1rem; overflow-x: auto; }
    input.ky-input { max-width: 120px; }
  `]
})
export class CongeQuotasServiceComponent implements OnInit {
  private readonly toast = inject(KyntusToastService);
  private readonly svc = inject(CongeService);
  private readonly userSvc = inject(UserService);
  private readonly cdr = inject(ChangeDetectorRef);

  rows: QuotaCongeServiceDto[] = [];
  edits: Record<string, number | null> = {};
  loading = true;
  private superviseurId = '';

  ngOnInit(): void {
    this.userSvc.getCurrentUser().subscribe({
      next: (u) => {
        this.superviseurId = u.guid;
        this.load();
      },
      error: () => this.toast.error('Impossible de récupérer le profil.')
    });
  }

  load(): void {
    if (!this.superviseurId) return;
    this.loading = true;
    this.svc.getQuotasService(this.superviseurId).subscribe({
      next: (data) => {
        this.rows = data;
        this.edits = {};
        for (const r of data) {
          this.edits[r.serviceId] = r.maxAbsentsSimultanes;
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.toast.error('Impossible de charger les quotas.');
        this.cdr.detectChanges();
      }
    });
  }

  save(r: QuotaCongeServiceDto): void {
    const val = Number(this.edits[r.serviceId]);
    if (!Number.isFinite(val) || val < 1) {
      this.toast.error('Indiquez un quota entier ≥ 1.');
      return;
    }
    this.svc.upsertQuotaService(r.serviceId, val, this.superviseurId).subscribe({
      next: (updated) => {
        const idx = this.rows.findIndex(x => x.serviceId === updated.serviceId);
        if (idx >= 0) this.rows[idx] = updated;
        this.edits[updated.serviceId] = updated.maxAbsentsSimultanes;
        this.toast.success('Quota enregistré.');
        this.cdr.detectChanges();
      },
      error: (err) => this.toast.error(err?.error?.message || 'Échec de l\'enregistrement.')
    });
  }
}
