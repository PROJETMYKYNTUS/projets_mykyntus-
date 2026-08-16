import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { KyntusToastService } from '../../../../shared/components/ui/kyntus-toast.service';
import { CongeService } from '../../../../core/services/conge.service';
import { UserService } from '../../../users/services/user.service';
import { MOIS_LABELS, PeriodesInterditesDto } from '../../../../core/models/conge.models';

@Component({
  selector: 'app-conge-periodes-interdites',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  template: `
    <div class="ky-page-shell">
      <app-kyntus-page-header
        title="Périodes interdites"
        subtitle="Mois calendaires où les demandes de congé sont refusées (récurrents chaque année). Défaut : septembre et octobre.">
        <div actions>
          <button type="button" class="ky-btn-primary" [disabled]="saving" (click)="save()">
            Enregistrer
          </button>
        </div>
      </app-kyntus-page-header>

      <p class="hint" *ngIf="loading">Chargement…</p>

      <div class="months-grid" *ngIf="!loading">
        <label class="month-chip" *ngFor="let m of allMonths" [class.on]="selected.has(m)">
          <input type="checkbox" [checked]="selected.has(m)" (change)="toggle(m)" />
          {{ moisLabels[m] }}
        </label>
      </div>

      <p class="meta" *ngIf="updatedAt">Dernière mise à jour : {{ updatedAt | date:'dd/MM/yyyy HH:mm' }}</p>
    </div>
  `,
  styles: [`
    .hint { color: var(--ky-muted, #64748b); }
    .months-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      gap: 0.75rem;
      margin-top: 1rem;
    }
    .month-chip {
      display: flex; align-items: center; gap: 0.5rem;
      padding: 0.75rem 1rem; border: 1px solid var(--ky-border, #e2e8f0);
      border-radius: 8px; cursor: pointer; background: #fff;
    }
    .month-chip.on { border-color: var(--ky-primary, #0f766e); background: #f0fdfa; }
    .meta { margin-top: 1.25rem; font-size: 0.85rem; color: #64748b; }
  `]
})
export class CongePeriodesInterditesComponent implements OnInit {
  private readonly toast = inject(KyntusToastService);
  private readonly svc = inject(CongeService);
  private readonly userSvc = inject(UserService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly allMonths = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  readonly moisLabels = MOIS_LABELS;
  selected = new Set<number>([9, 10]);
  loading = true;
  saving = false;
  updatedAt: string | null = null;
  private userId = '';

  ngOnInit(): void {
    this.userSvc.getCurrentUser().subscribe({
      next: (u) => { this.userId = u.guid; },
      error: () => {}
    });
    this.svc.getPeriodesInterdites().subscribe({
      next: (dto: PeriodesInterditesDto) => {
        this.selected = new Set(dto.mois?.length ? dto.mois : [9, 10]);
        this.updatedAt = dto.updatedAt;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.toast.error('Impossible de charger les périodes interdites.');
        this.cdr.detectChanges();
      }
    });
  }

  toggle(m: number): void {
    if (this.selected.has(m)) this.selected.delete(m);
    else this.selected.add(m);
  }

  save(): void {
    this.saving = true;
    const mois = [...this.selected].sort((a, b) => a - b);
    this.svc.updatePeriodesInterdites(mois, this.userId || undefined).subscribe({
      next: (dto) => {
        this.selected = new Set(dto.mois);
        this.updatedAt = dto.updatedAt;
        this.saving = false;
        this.toast.success('Périodes interdites enregistrées.');
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.saving = false;
        this.toast.error(err?.error?.message || 'Échec de l\'enregistrement.');
        this.cdr.detectChanges();
      }
    });
  }
}
