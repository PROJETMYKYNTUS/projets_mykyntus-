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
    .page-wrapper { padding: 24px; }
    .page-title { font-size: 24px; font-weight: 700; color: #fff; }
    .page-sub { color: #94a3b8; margin-bottom: 24px; }
    .filters { display: flex; gap: 12px; margin-bottom: 24px; }
    .filters select, .filters input {
      padding: 8px 12px; border-radius: 8px;
      background: #1e293b; color: #fff; border: 1px solid #334155;
    }
    .emp-row {
      display: flex; align-items: center; justify-content: space-between;
      padding: 12px 16px; background: #1e293b;
      border-radius: 8px; margin-bottom: 8px;
    }
    .emp-name { color: #fff; font-weight: 600; }
    .toggle-group { display: flex; gap: 8px; }
    .btn-worked, .btn-off {
      padding: 6px 16px; border-radius: 6px;
      border: none; cursor: pointer; font-weight: 600;
      background: #334155; color: #94a3b8;
    }
    .btn-worked.active { background: #10b981; color: #fff; }
    .btn-off.active { background: #ef4444; color: #fff; }
    .btn-save {
      margin-top: 16px; padding: 10px 24px;
      background: #6366f1; color: #fff;
      border: none; border-radius: 8px;
      cursor: pointer; font-weight: 700;
    }
    .success-msg { color: #10b981; margin-bottom: 16px; }
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