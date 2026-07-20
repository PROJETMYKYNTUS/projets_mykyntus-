import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { Router } from '@angular/router';
import {
  CoverageDayShift,
  DayAssignment,
  PlanningService,
  WeeklyPlanningResponse,
} from '../../services/planning.service';

@Component({
  selector: 'app-planning-equipe',
  standalone: true,
  imports: [CommonModule, KyntusPageHeaderComponent],
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

  selectPlanning(p: WeeklyPlanningResponse): void {
    this.selectedEquipePlanning = p;
  }

  getEmpDay(emp: { days?: DayAssignment[] }, day: string): DayAssignment | null {
    return emp.days?.find(d => d.day === day) ?? null;
  }

  dayCoverageClass(day: string): string {
    const syn = this.daySynthesisFor(day);
    const items = this.coverageForDay(day);
    if (syn?.hasAnyAnomaly || items.some(i => i.hasLevelBalanceAnomaly)) return 'cov-bad';
    if (items.length === 0) return '';
    if (items.some(i => i.isUnderstaffed)) return 'cov-bad';
    return 'cov-ok';
  }

  coverageForDay(day: string): CoverageDayShift[] {
    const report = this.selectedEquipePlanning?.coverageReport;
    if (!report?.items?.length) return [];
    return report.items.filter(i => i.day === day);
  }

  dayCoverageLabel(day: string): string {
    const syn = this.daySynthesisFor(day);
    if (syn) {
      const parts: string[] = [];
      if (syn.leaveCount) parts.push(`${syn.leaveCount} abs`);

      if (day === 'Saturday') {
        const d = syn.saturdayBeginners ?? 0;
        const c = syn.saturdaySeniors ?? 0;
        if (d > 0 && c === 0) parts.push('Débutant seul · samedi');
      } else if (syn.shifts?.length) {
        for (const s of syn.shifts) {
          const delta = s.delta ?? 0;
          if (delta < 0) parts.push(`Manque ${Math.abs(delta)} · ${s.shiftLabel}`);
          else if (delta > 0) parts.push(`Excédent ${delta} · ${s.shiftLabel}`);
          if (s.hasLevelBalanceAnomaly) {
            parts.push(`Débutant seul · ${this.shiftKindContext(s.shiftKind)}`);
          }
        }
      }

      if (parts.length) return parts.join(' · ');
    }
    const items = this.coverageForDay(day);
    if (items.length === 0) return '';
    const assigned = items.reduce((s, i) => s + i.assignedCount, 0);
    const required = items.reduce((s, i) => s + i.requiredCount, 0);
    return `${assigned}/${required}`;
  }

  private shiftKindContext(kind?: string): string {
    switch ((kind ?? '').toLowerCase()) {
      case 'opening':
        return 'ouverture';
      case 'closing':
        return 'fermeture';
      default:
        return 'milieu';
    }
  }

  daySynthesisFor(day: string) {
    return this.selectedEquipePlanning?.coverageReport?.daySynthesis?.find(d => d.day === day) ?? null;
  }

  get hasLevelBalanceAnomaly(): boolean {
    return !!this.selectedEquipePlanning?.coverageReport?.hasLevelBalanceAnomaly;
  }

  goBack(): void {
    void this.router.navigate(['/planning']);
  }
}
