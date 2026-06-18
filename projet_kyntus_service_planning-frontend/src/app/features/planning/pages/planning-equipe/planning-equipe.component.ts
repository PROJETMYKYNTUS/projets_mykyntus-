import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { DayAssignment, PlanningService, WeeklyPlanningResponse } from '../../services/planning.service';

@Component({
  selector: 'app-planning-equipe',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './planning-equipe.component.html',
  styleUrls: ['./planning-equipe.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class PlanningEquipeComponent implements OnInit {
  equipePlannings: WeeklyPlanningResponse[] = [];
  equipeLoading = false;
  selectedEquipePlanning: WeeklyPlanningResponse | null = null;
  userId = 0;
  error = '';

  readonly weekDays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;
  readonly dayLabels: Record<string, string> = {
    Monday: 'Lun', Tuesday: 'Mar', Wednesday: 'Mer',
    Thursday: 'Jeu', Friday: 'Ven', Saturday: 'Sam',
  };

  constructor(
    private readonly planningService: PlanningService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    this.userId = Number(user?.id) || 0;
    if (!this.userId) {
      this.error = 'Utilisateur non identifié.';
      return;
    }
    this.loadEquipePlannings();
  }

  loadEquipePlannings(): void {
    this.equipeLoading = true;
    this.error = '';
    this.planningService.getEquipePlannings(this.userId).subscribe({
      next: (data) => {
        this.equipePlannings = data ?? [];
        this.equipeLoading = false;
        this.selectedEquipePlanning = this.equipePlannings[0] ?? null;
      },
      error: () => {
        this.equipePlannings = [];
        this.equipeLoading = false;
        this.error = 'Impossible de charger les plannings équipe.';
      },
    });
  }

  getEmpDay(emp: { days?: DayAssignment[] }, day: string): DayAssignment | null {
    return emp.days?.find(d => d.day === day) ?? null;
  }

  goBack(): void {
    void this.router.navigate(['/planning']);
  }
}
