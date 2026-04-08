import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlanningService } from '../../../../core/services/planning.service';

@Component({
  selector: 'app-dashboard-employee',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard-employee.component.html',
  styleUrls: ['./dashboard-employee.component.css']
})
export class DashboardEmployeeComponent implements OnInit {

  planning: any = null;
  history: any[] = [];
  loading = true;
  currentWeek = '';
  userId = 0;

  constructor(private planningService: PlanningService) {}

  ngOnInit(): void {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    this.userId = user.id;
    this.currentWeek = this.getCurrentWeekCode();
    this.loadWeek(this.currentWeek);
    this.loadHistory();
  }

  loadWeek(weekCode: string): void {
    this.loading = true;
    this.currentWeek = weekCode;
    this.planningService.getMyPlanning(weekCode, this.userId).subscribe({
      next: (data) => { this.planning = data; this.loading = false; },
      error: () => { this.planning = null; this.loading = false; }
    });
  }

  loadHistory(): void {
    this.planningService.getMyHistory(this.userId).subscribe({
      next: (data) => this.history = data,
      error: () => this.history = []
    });
  }

  getShiftColor(type: string): string {
    const colors: any = {
      'Matin': '#dbeafe',
      'Après-midi': '#fef3c7',
      'Nuit': '#ede9fe',
      'OFF': '#f1f5f9'
    };
    return colors[type] || '#f0f0f0';
  }

  getCurrentWeekCode(): string {
    const now = new Date();
    const start = new Date(now.getFullYear(), 0, 1);
    const week = Math.ceil(((now.getTime() - start.getTime()) / 86400000 + start.getDay() + 1) / 7);
    return `${now.getFullYear()}-W${week.toString().padStart(2, '0')}`;
  }
}