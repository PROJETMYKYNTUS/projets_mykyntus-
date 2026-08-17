import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';
import { CongeService } from '../../../../core/services/conge.service';
import { UserService } from '../../../users/services/user.service';
import { QuotaCongeServiceDto } from '../../../../core/models/conge.models';
import { resolveCurrentUserGuid, resolveUserGuid } from '../../../../core/lib/user-guid.util';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';

@Component({
  selector: 'app-conge-quotas-service',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  template: `
    <div class="ky-page-shell">
      <app-kyntus-page-header
        title="Quotas congés (cellules / services)"
        subtitle="Nombre max d’employés absents le même jour par cellule ou service. Seuls les congés validés superviseur (en attente RH) et validés RH comptent.">
        <div actions>
          <button type="button" class="ky-btn-secondary" (click)="load()">Actualiser</button>
        </div>
      </app-kyntus-page-header>

      <p class="hint" *ngIf="loading">Chargement…</p>
      <p class="hint" *ngIf="!loading && rows.length === 0">
        Aucun périmètre (cellule / service) trouvé.
        Vérifiez vos affectations Organisation RH (superviseur sur une cellule), puis actualisez.
      </p>

      <div class="table-wrap" *ngIf="!loading && rows.length > 0">
        <table class="prime-table">
          <thead>
            <tr>
              <th>Type</th>
              <th>Périmètre</th>
              <th>Effectif</th>
              <th>Max absents / jour</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let r of rows">
              <td>
                <span class="scope-chip" [attr.data-scope]="r.scopeKind">{{ scopeLabel(r.scopeKind) }}</span>
              </td>
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
    .scope-chip {
      display: inline-block;
      font-size: 0.68rem;
      font-weight: 700;
      padding: 0.15rem 0.5rem;
      border-radius: 0.35rem;
      background: #e2e8f0;
      color: #334155;
    }
    .scope-chip[data-scope="Cellule"] {
      background: #dbeafe;
      color: #1d4ed8;
    }
    .scope-chip[data-scope="Service"] {
      background: #ccfbf1;
      color: #0f766e;
    }
  `]
})
export class CongeQuotasServiceComponent implements OnInit {
  private readonly toast = inject(KyntusToastService);
  private readonly svc = inject(CongeService);
  private readonly userSvc = inject(UserService);
  private readonly session = inject(KyntusSessionService);
  private readonly cdr = inject(ChangeDetectorRef);

  rows: QuotaCongeServiceDto[] = [];
  edits: Record<string, number | null> = {};
  loading = true;
  private superviseurId = '';

  ngOnInit(): void {
    this.userSvc.getCurrentUser().subscribe({
      next: (u) => {
        // Directory / ReBAC utilisent le subject Auth (Guid), pas seulement le guid Planning.
        this.superviseurId =
          this.session.getSubjectId()?.trim()
          || resolveCurrentUserGuid()
          || resolveUserGuid(u);
        this.load();
      },
      error: () => this.toast.error('Impossible de récupérer le profil.')
    });
  }

  scopeLabel(kind?: string | null): string {
    return (kind || '').toLowerCase() === 'cellule' ? 'Cellule' : 'Service';
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
    this.svc.upsertQuotaService(r.serviceId, val, this.superviseurId, r.scopeKind).subscribe({
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
