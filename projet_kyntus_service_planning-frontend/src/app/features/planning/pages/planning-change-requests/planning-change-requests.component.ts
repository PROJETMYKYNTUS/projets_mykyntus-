import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { PlanningService } from '../../services/planning.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';

@Component({
  selector: 'app-planning-change-requests',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  templateUrl: './planning-change-requests.component.html',
  styleUrls: ['./planning-change-requests.component.css'],
})
export class PlanningChangeRequestsComponent implements OnInit {
  requests: any[] = [];
  stats: any[] = [];
  loading = false;
  error = '';
  toast = '';
  filterStatus = '';
  filterWeek = '';
  authUserId = 0;
  rejectId: number | null = null;
  rejectReason = '';

  constructor(
    private planning: PlanningService,
    private session: KyntusSessionService,
    private cdr: ChangeDetectorRef,
    private router: Router,
  ) {}

  ngOnInit(): void {
    const role = this.session.getRole();
    if (role !== 'Admin' && role !== 'RH') {
      void this.router.navigate(['/mes-plannings']);
      return;
    }
    this.authUserId = this.session.getAuthUserId() ?? 0;
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.error = '';
    this.planning.getChangeRequests(this.filterStatus || undefined, this.filterWeek || undefined).subscribe({
      next: (list) => {
        this.requests = list ?? [];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.error = 'Impossible de charger les demandes.';
        this.cdr.detectChanges();
      },
    });
    this.planning.getChangeRequestStats(this.filterWeek || undefined).subscribe({
      next: (s) => { this.stats = s ?? []; this.cdr.detectChanges(); },
      error: () => { this.stats = []; },
    });
  }

  hasSwap(r: any): boolean {
    return r?.proposedSwapUserId != null && Number(r.proposedSwapUserId) > 0;
  }

  /** Avec switch proposé : appliquer l’échange après validation RH. */
  approve(id: number): void {
    if (!confirm('Approuver et appliquer le switch entre les deux employés ?')) return;
    this.error = '';
    this.planning.approveChangeRequest(id, this.authUserId).subscribe({
      next: () => {
        this.toast = 'Switch appliqué — demande approuvée.';
        this.reload();
      },
      error: (err) => {
        this.error = err.error?.message ?? 'Échec approve.';
        this.cdr.detectChanges();
      },
    });
  }

  /**
   * Sans switch : ouvrir le planning de la semaine avec la cellule employé/shift surlignée
   * pour que le RH réaffecte manuellement.
   */
  openPlanningToTreat(r: any): void {
    const planningId = Number(r?.weeklyPlanningId);
    if (!Number.isFinite(planningId) || planningId <= 0) {
      this.error = 'Planning de la demande introuvable.';
      this.cdr.detectChanges();
      return;
    }
    void this.router.navigate(['/planning/view', planningId], {
      queryParams: {
        from: 'change-request',
        changeRequestId: r.id,
        highlightAssignmentId: r.currentAssignmentId,
        highlightUserId: r.requesterUserId,
        highlightDay: r.assignmentDay,
      },
    });
  }

  openReject(id: number): void {
    this.rejectId = id;
    this.rejectReason = '';
  }

  confirmReject(): void {
    if (this.rejectId == null) return;
    this.planning.rejectChangeRequest(this.rejectId, this.authUserId, this.rejectReason).subscribe({
      next: () => {
        this.rejectId = null;
        this.toast = 'Demande rejetée.';
        this.reload();
      },
      error: (err) => {
        this.error = err.error?.message ?? 'Échec reject.';
        this.cdr.detectChanges();
      },
    });
  }

  statusLabel(s: string): string {
    const map: Record<string, string> = {
      Pending: 'En attente', Approved: 'Approuvée', Rejected: 'Rejetée', Cancelled: 'Annulée',
    };
    return map[s] ?? s;
  }
}
