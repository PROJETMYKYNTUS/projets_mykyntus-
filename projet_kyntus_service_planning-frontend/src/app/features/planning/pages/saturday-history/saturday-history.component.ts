// planning/pages/saturday-history/saturday-history.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PlanningService, SaturdayHistoryResponse, SetSaturdayHistoryDto } from '../../services/planning.service';

@Component({
  selector: 'app-saturday-history',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-wrapper">
      <h1 class="page-title">📅 Historique Samedis</h1>
      <p class="page-sub">Saisir manuellement qui a travaillé samedi</p>

      <div class="filters">
        <select [(ngModel)]="subServiceId" (change)="load()">
          <option [value]="0" disabled>-- Sous-service --</option>
          <option *ngFor="let s of subServices" [value]="s.id">{{ s.name }}</option>
        </select>

        <input type="text" [(ngModel)]="weekCode"
               placeholder="ex: 2026-W13"
               (change)="load()" />
      </div>

      <div class="success-msg" *ngIf="successMsg">✅ {{ successMsg }}</div>

      <div class="employees-list" *ngIf="entries.length > 0">
        <div class="emp-row" *ngFor="let e of entries">
          <span class="emp-name">{{ e.fullName }}</span>
          <div class="toggle-group">
            <button class="btn-worked"
                    [class.active]="e.workedSaturday"
                    (click)="e.workedSaturday = true">
              ✅ Travaillé
            </button>
            <button class="btn-off"
                    [class.active]="!e.workedSaturday"
                    (click)="e.workedSaturday = false">
              🔴 OFF
            </button>
          </div>
        </div>

        <button class="btn-save" (click)="save()">
          💾 Sauvegarder
        </button>
      </div>
    </div>
  `,
  styles: [`
  .page-wrapper {
    min-height: 100vh;
    padding: 32px 24px 48px;
    background:
      radial-gradient(circle at top right, rgba(37, 99, 235, 0.06), transparent 18%),
      linear-gradient(180deg, #f8fbff 0%, #f1f5f9 100%);
    font-family: "Inter", "Segoe UI", sans-serif;
    color: #0f172a;
  }

  .page-title {
    margin: 0 0 6px;
    font-size: 2rem;
    font-weight: 700;
    color: #0f172a;
    letter-spacing: -0.02em;
  }

  .page-sub {
    margin: 0 0 24px;
    color: #64748b;
    font-size: 0.92rem;
  }

  .filters {
    display: flex;
    gap: 12px;
    margin-bottom: 24px;
    flex-wrap: wrap;
    padding: 20px;
    background: rgba(255, 255, 255, 0.96);
    border: 1px solid #e2e8f0;
    border-radius: 24px;
    box-shadow: 0 18px 40px rgba(15, 23, 42, 0.07);
  }

  .filters select,
  .filters input {
    min-width: 220px;
    padding: 12px 14px;
    border-radius: 16px;
    background: #ffffff;
    color: #0f172a;
    border: 1px solid #dbe3ee;
    font-size: 0.92rem;
    outline: none;
    transition: all 0.22s ease;
  }

  .filters select:focus,
  .filters input:focus {
    border-color: #60a5fa;
    box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.12);
  }

  .success-msg {
    margin-bottom: 18px;
    padding: 14px 16px;
    background: #ecfdf5;
    border: 1px solid #bbf7d0;
    border-radius: 16px;
    color: #047857;
    font-size: 0.9rem;
    font-weight: 600;
  }

  .employees-list {
    background: rgba(255, 255, 255, 0.96);
    border: 1px solid #e2e8f0;
    border-radius: 24px;
    padding: 24px;
    box-shadow: 0 18px 40px rgba(15, 23, 42, 0.07);
  }

  .emp-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    padding: 16px 18px;
    background: #fbfdff;
    border: 1px solid #edf2f7;
    border-radius: 18px;
    margin-bottom: 12px;
    transition: all 0.22s ease;
  }

  .emp-row:hover {
    background: #f8fbff;
    border-color: #dbeafe;
  }

  .emp-name {
    color: #0f172a;
    font-weight: 700;
    font-size: 0.95rem;
  }

  .toggle-group {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
  }

  .btn-worked,
  .btn-off {
    padding: 10px 16px;
    border-radius: 12px;
    border: 1px solid #dbe3ee;
    cursor: pointer;
    font-weight: 700;
    font-size: 0.84rem;
    background: #ffffff;
    color: #64748b;
    transition: all 0.22s ease;
  }

  .btn-worked:hover,
  .btn-off:hover {
    transform: translateY(-1px);
  }

  .btn-worked.active {
    background: #ecfdf5;
    color: #047857;
    border-color: #86efac;
  }

  .btn-off.active {
    background: #fff1f2;
    color: #b91c1c;
    border-color: #fecdd3;
  }

  .btn-save {
    margin-top: 18px;
    padding: 12px 24px;
    background: linear-gradient(135deg, #2563eb 0%, #3b82f6 100%);
    color: #ffffff;
    border: none;
    border-radius: 16px;
    cursor: pointer;
    font-weight: 700;
    font-size: 0.9rem;
    box-shadow: 0 12px 24px rgba(37, 99, 235, 0.18);
    transition: all 0.22s ease;
  }

  .btn-save:hover {
    transform: translateY(-1px);
    box-shadow: 0 16px 28px rgba(37, 99, 235, 0.22);
  }

  @media (max-width: 768px) {
    .page-wrapper {
      padding: 20px 16px 40px;
    }

    .page-title {
      font-size: 1.5rem;
    }

    .filters,
    .employees-list {
      border-radius: 18px;
      padding: 18px;
    }

    .emp-row {
      flex-direction: column;
      align-items: flex-start;
    }

    .toggle-group {
      width: 100%;
    }

    .btn-worked,
    .btn-off,
    .btn-save {
      width: 100%;
      justify-content: center;
    }
  }
`]
})
export class SaturdayHistoryComponent implements OnInit {
  subServices: any[] = [];
  subServiceId = 0;
  weekCode = '';
  entries: SaturdayHistoryResponse[] = [];
  successMsg = '';

  constructor(private planningService: PlanningService) {}

ngOnInit(): void {
  // ✅ D'abord définir la semaine
  const now      = new Date();
  const week     = this.getWeekNumber(now);
  const prevWeek = week === 1 ? 52 : week - 1;
  const year     = week === 1 ? now.getFullYear() - 1 : now.getFullYear();
  this.weekCode  = `${year}-W${String(prevWeek).padStart(2, '0')}`;

  // ✅ Ensuite charger les sous-services et lancer load()
  this.planningService.getSubServices().subscribe(data => {
    this.subServices = data;
    if (data.length > 0) {
      this.subServiceId = data[0].id;
      this.load(); // ✅ weekCode est déjà défini ici
    }
  });
}

  load(): void {
    if (!this.subServiceId || !this.weekCode) return;
    this.planningService.getSaturdayHistory(this.subServiceId, this.weekCode)
      .subscribe(data => this.entries = data);
  }

  save(): void {
    const dto: SetSaturdayHistoryDto = {
      subServiceId: this.subServiceId,
      weekCode:     this.weekCode,
      entries:      this.entries.map(e => ({
        userId:        e.userId,
        workedSaturday: e.workedSaturday
      }))
    };

    this.planningService.saveSaturdayHistory(dto).subscribe(() => {
      this.successMsg = 'Historique sauvegardé !';
      setTimeout(() => this.successMsg = '', 3000);
    });
  }

  private getWeekNumber(date: Date): number {
    const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
    const dayNum = d.getUTCDay() || 7;
    d.setUTCDate(d.getUTCDate() + 4 - dayNum);
    const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
    return Math.ceil((((d.getTime() - yearStart.getTime()) / 86400000) + 1) / 7);
  }
}