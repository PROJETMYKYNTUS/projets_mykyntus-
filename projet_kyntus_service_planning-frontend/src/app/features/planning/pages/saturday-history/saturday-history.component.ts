// planning/pages/saturday-history/saturday-history.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Calendar, CheckCircle, Circle, Save } from 'lucide';
import { LucideIconComponent } from '../../../../shared/lucide-icon.component';
import { PlanningService, SaturdayHistoryResponse, SetSaturdayHistoryDto } from '../../services/planning.service';

@Component({
  selector: 'app-saturday-history',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideIconComponent],
  template: `
    <div class="page-wrapper">
      <h1 class="page-title">
        <app-lucide-icon [icon]="icons.calendar" className="w-7 h-7" />
        Historique Samedis
      </h1>
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

      <div class="success-msg" *ngIf="successMsg">
        <app-lucide-icon [icon]="icons.success" className="w-4 h-4" />
        {{ successMsg }}
      </div>

      <div class="employees-list" *ngIf="entries.length > 0">
        <div class="emp-row" *ngFor="let e of entries">
          <span class="emp-name">{{ e.fullName }}</span>
          <div class="toggle-group">
            <button class="btn-worked"
                    [class.active]="e.workedSaturday"
                    (click)="e.workedSaturday = true">
              <app-lucide-icon [icon]="icons.worked" className="w-4 h-4" />
              Travaillé
            </button>
            <button class="btn-off"
                    [class.active]="!e.workedSaturday"
                    (click)="e.workedSaturday = false">
              <app-lucide-icon [icon]="icons.off" className="w-4 h-4" />
              OFF
            </button>
          </div>
        </div>

        <button class="btn-save" (click)="save()">
          <app-lucide-icon [icon]="icons.save" className="w-4 h-4" />
          Sauvegarder
        </button>
      </div>
    </div>
  `,
  styles: [`
  .page-wrapper {
    min-height: 100vh;
    padding: 32px 24px 48px;
    background:
      radial-gradient(circle at top right, color-mix(in srgb, var(--soft-blue) 8%, transparent), transparent 18%),
      linear-gradient(180deg, var(--bg-primary) 0%, var(--bg-primary) 100%);
    font-family: "Inter", "Segoe UI", sans-serif;
    color: var(--text-primary);
  }

  .page-title {
    display: flex;
    align-items: center;
    gap: 10px;
    margin: 0 0 6px;
    font-size: 2rem;
    font-weight: 700;
    color: var(--text-primary);
    letter-spacing: -0.02em;
  }

  .page-sub {
    margin: 0 0 24px;
    color: var(--text-muted);
    font-size: 0.92rem;
  }

  .filters {
    display: flex;
    gap: 12px;
    margin-bottom: 24px;
    flex-wrap: wrap;
    padding: 20px;
    background: var(--bg-card);
    border: 1px solid var(--border-color);
    border-radius: 24px;
    box-shadow: 0 18px 40px color-mix(in srgb, var(--navy-950) 10%, transparent);
  }

  .filters select,
  .filters input {
    min-width: 220px;
    padding: 12px 14px;
    border-radius: 16px;
    background: var(--bg-input);
    color: var(--text-primary);
    border: 1px solid var(--border-color);
    font-size: 0.92rem;
    outline: none;
    transition: all 0.22s ease;
  }

  .filters select:focus,
  .filters input:focus {
    border-color: var(--soft-blue);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--soft-blue) 20%, transparent);
  }

  .success-msg {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 18px;
    padding: 14px 16px;
    background: color-mix(in srgb, #22c55e 12%, var(--bg-card));
    border: 1px solid color-mix(in srgb, #22c55e 30%, var(--border-color));
    border-radius: 16px;
    color: var(--text-primary);
    font-size: 0.9rem;
    font-weight: 600;
  }

  .employees-list {
    background: var(--bg-card);
    border: 1px solid var(--border-color);
    border-radius: 24px;
    padding: 24px;
    box-shadow: 0 18px 40px color-mix(in srgb, var(--navy-950) 10%, transparent);
  }

  .emp-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    padding: 16px 18px;
    background: var(--bg-input);
    border: 1px solid var(--border-color);
    border-radius: 18px;
    margin-bottom: 12px;
    transition: all 0.22s ease;
  }

  .emp-row:hover {
    background: var(--navy-700);
    border-color: var(--border-color);
  }

  .emp-name {
    color: var(--text-primary);
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
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 10px 16px;
    border-radius: 12px;
    border: 1px solid var(--border-color);
    cursor: pointer;
    font-weight: 700;
    font-size: 0.84rem;
    background: var(--bg-card);
    color: var(--text-muted);
    transition: all 0.22s ease;
  }

  .btn-worked:hover,
  .btn-off:hover {
    transform: translateY(-1px);
    background: var(--navy-700);
    color: var(--text-primary);
  }

  .btn-worked.active {
    background: color-mix(in srgb, #22c55e 16%, var(--bg-card));
    color: var(--text-primary);
    border-color: color-mix(in srgb, #22c55e 40%, var(--border-color));
  }

  .btn-off.active {
    background: color-mix(in srgb, #ef4444 16%, var(--bg-card));
    color: var(--text-primary);
    border-color: color-mix(in srgb, #ef4444 40%, var(--border-color));
  }

  .btn-save {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    margin-top: 18px;
    padding: 12px 24px;
    background: linear-gradient(135deg, var(--blue-600) 0%, var(--blue-500) 100%);
    color: #ffffff;
    border: none;
    border-radius: 16px;
    cursor: pointer;
    font-weight: 700;
    font-size: 0.9rem;
    box-shadow: 0 12px 24px color-mix(in srgb, var(--blue-600) 25%, transparent);
    transition: all 0.22s ease;
  }

  .btn-save:hover {
    transform: translateY(-1px);
    box-shadow: 0 16px 28px color-mix(in srgb, var(--blue-600) 30%, transparent);
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
  readonly icons = {
    calendar: Calendar,
    success: CheckCircle,
    worked: CheckCircle,
    off: Circle,
    save: Save,
  };

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