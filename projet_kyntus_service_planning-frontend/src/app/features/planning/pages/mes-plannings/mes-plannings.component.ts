import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { PlanningService } from '../../../../core/services/planning.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';

interface DayAssignment {
  day: string;
  assignedDate: string;
  shiftLabel: string;
  startTime: string;
  endTime: string;
  breakTime?: string | null;
  isSaturday: boolean;
  isOnLeave: boolean;
  isHoliday: boolean;
  holidayName: string;
  absenceType?: string | null;
  slotLabel: string;
}

interface MyPlanning {
  weekCode: string;
  weekStartDate: string;
  subServiceName: string;
  days: DayAssignment[];
}

@Component({
  selector: 'app-mes-plannings',
  standalone: true,
  imports: [CommonModule, KyntusPageHeaderComponent],
  templateUrl: './mes-plannings.component.html',
  styleUrls: ['./mes-plannings.component.css'],
})
export class MesPlanningsComponent implements OnInit {
  current: MyPlanning | null = null;
  history: MyPlanning[] = [];
  loading = true;
  errorMsg = '';

  constructor(
    private planningSvc: PlanningService,
    private session: KyntusSessionService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const authUserId = this.session.getAuthUserId();
    if (!authUserId) {
      this.loading = false;
      this.errorMsg = 'Impossible d’identifier l’utilisateur connecté.';
      return;
    }

    this.planningSvc.getMyCurrentPlanning(authUserId).subscribe({
      next: (p) => { this.current = p ?? null; this.loading = false; this.cdr.detectChanges(); },
      // 404 = pas de planning publié cette semaine : ce n'est pas une erreur.
      error: () => { this.current = null; this.loading = false; this.cdr.detectChanges(); },
    });

    this.planningSvc.getMyHistory(authUserId).subscribe({
      next: (list) => {
        const all: MyPlanning[] = Array.isArray(list) ? list : [];
        // On retire la semaine courante de l'historique pour éviter le doublon.
        this.history = all;
        this.cdr.detectChanges();
      },
      error: () => { this.history = []; this.cdr.detectChanges(); },
    });
  }

  get pastWeeks(): MyPlanning[] {
    if (!this.current) return this.history;
    return this.history.filter((p) => p.weekCode !== this.current!.weekCode);
  }

  shiftClass(d: DayAssignment): string {
    if (d.isHoliday) return 'cell-holiday';
    if (d.isOnLeave) return 'cell-leave';
    if (d.isSaturday) return 'cell-saturday';
    return 'cell-work';
  }

  cellLabel(d: DayAssignment): string {
    if (d.isHoliday) return d.holidayName || 'Férié';
    if (d.isOnLeave) return d.absenceType || 'Congé';
    if (d.shiftLabel) return d.shiftLabel;
    return '—';
  }
}
