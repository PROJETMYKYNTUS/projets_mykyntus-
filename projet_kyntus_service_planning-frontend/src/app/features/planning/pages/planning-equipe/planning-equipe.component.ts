import { Component, OnInit, ViewEncapsulation, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { Router } from '@angular/router';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import {
  PlanningService,
  EquipePlanningSummary,
} from '../../services/planning.service';
import { formatWeekLabel } from '../../utils/week-code.util';

@Component({
  selector: 'app-planning-equipe',
  standalone: true,
  imports: [CommonModule, KyntusPageHeaderComponent],
  templateUrl: './planning-equipe.component.html',
  styleUrls: ['./planning-equipe.component.css'],
  encapsulation: ViewEncapsulation.None,
})
export class PlanningEquipeComponent implements OnInit {
  private readonly planningService = inject(PlanningService);
  private readonly session = inject(KyntusSessionService);
  private readonly router = inject(Router);

  equipePlannings: EquipePlanningSummary[] = [];
  equipeLoading = false;
  authUserId = 0;
  error = '';

  readonly formatWeekLabel = formatWeekLabel;

  ngOnInit(): void {
    this.authUserId = this.session.getAuthUserId();
    if (!this.authUserId) {
      this.error = 'Utilisateur non identifié.';
      return;
    }
    this.loadEquipePlannings();
  }

  loadEquipePlannings(): void {
    this.equipeLoading = true;
    this.error = '';
    this.planningService.getEquipePlannings(this.authUserId).subscribe({
      next: (data) => {
        this.equipePlannings = data ?? [];
        this.equipeLoading = false;
        if (this.equipePlannings.length === 0) {
          this.error = 'Aucun planning publié pour les services que vous gérez.';
        }
      },
      error: () => {
        this.equipePlannings = [];
        this.equipeLoading = false;
        this.error = 'Impossible de charger les plannings équipe.';
      },
    });
  }

  /** Ouvre la même grille RH en lecture seule. */
  openPlanning(p: EquipePlanningSummary): void {
    void this.router.navigate(['/planning/view', p.id], {
      queryParams: {
        from: 'equipe',
        weekCode: p.weekCode,
      },
    });
  }

  statusLabel(status: string): string {
    return status === 'Published' ? 'Publié' : status === 'Draft' ? 'Brouillon' : status;
  }
}
