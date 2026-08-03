import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { PlanningService } from '../../services/planning.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { KyntusRoleNames } from '../../../../core/org/kyntus-role-names';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import { formatWeekLabel, toApiWeekCode } from '../../utils/week-code.util';
import { BodyPortalDirective } from '../../../../shared/directives/body-portal.directive';

@Component({
  selector: 'app-planning-change-requests',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent, BodyPortalDirective],
  templateUrl: './planning-change-requests.component.html',
  styleUrls: ['./planning-change-requests.component.css'],
})
export class PlanningChangeRequestsComponent implements OnInit {
  requests: any[] = [];
  loading = false;
  error = '';
  toast = '';
  filterStatus = 'PendingSupervisor';
  filterWeek = '';
  authUserId = 0;
  rejectId: number | null = null;
  rejectReason = '';
  /** RH : lecture seule ; Superviseur/Admin : actions. */
  canManage = false;
  isRhReadonly = false;

  historyOpen = false;
  historyLoading = false;
  historyName = '';
  historyError = '';
  historyTotal = 0;
  historyPending = 0;
  historyApproved = 0;
  historyRejected = 0;

  readonly formatWeekLabel = formatWeekLabel;

  constructor(
    private planning: PlanningService,
    private session: KyntusSessionService,
    private cdr: ChangeDetectorRef,
    private router: Router,
    private confirmService: KyntusConfirmService,
  ) {}

  ngOnInit(): void {
    const role = this.session.getRole() ?? '';
    const allowed =
      role === KyntusRoleNames.Admin ||
      role === KyntusRoleNames.RH ||
      role === KyntusRoleNames.Superviseur ||
      role === 'Manager' ||
      role === KyntusRoleNames.ReferentTechnique ||
      role === KyntusRoleNames.Coach ||
      role === KyntusRoleNames.ChefDeProjet ||
      role === KyntusRoleNames.Rp;

    if (!allowed) {
      void this.router.navigate(['/mes-plannings']);
      return;
    }

    this.isRhReadonly = role === KyntusRoleNames.RH;
    this.canManage = !this.isRhReadonly;
    if (this.isRhReadonly) {
      this.filterStatus = '';
    }

    this.authUserId = this.session.getAuthUserId() ?? 0;
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.error = '';
    const weekApi = this.filterWeek ? toApiWeekCode(this.filterWeek) : undefined;
    this.planning
      .getChangeRequests(this.filterStatus || undefined, weekApi || undefined, this.authUserId)
      .subscribe({
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
  }

  canActOn(r: any): boolean {
    return this.canManage && r?.status === 'PendingSupervisor';
  }

  openEmployeeHistory(stat: {
    userId?: number;
    UserId?: number;
    fullName?: string;
    FullName?: string;
    totalRequests?: number;
    TotalRequests?: number;
    pendingCount?: number;
    PendingCount?: number;
    approvedCount?: number;
    ApprovedCount?: number;
    rejectedCount?: number;
    RejectedCount?: number;
  }): void {
    const userId = Number(stat.userId ?? stat.UserId ?? 0);
    if (!userId) return;
    this.historyName = String(stat.fullName ?? stat.FullName ?? `#${userId}`);
    this.historyOpen = true;
    this.historyError = '';

    const hasCounts =
      stat.totalRequests != null ||
      stat.TotalRequests != null ||
      stat.approvedCount != null ||
      stat.ApprovedCount != null;

    if (hasCounts) {
      this.historyLoading = false;
      this.historyTotal = Number(stat.totalRequests ?? stat.TotalRequests ?? 0);
      this.historyPending = Number(stat.pendingCount ?? stat.PendingCount ?? 0);
      this.historyApproved = Number(stat.approvedCount ?? stat.ApprovedCount ?? 0);
      this.historyRejected = Number(stat.rejectedCount ?? stat.RejectedCount ?? 0);
      this.cdr.detectChanges();
      return;
    }

    // Depuis la liste : charger le récap agrégé (sans afficher le détail ligne à ligne)
    this.historyLoading = true;
    this.historyTotal = 0;
    this.historyPending = 0;
    this.historyApproved = 0;
    this.historyRejected = 0;
    this.cdr.detectChanges();

    this.planning.getChangeRequests(undefined, undefined, this.authUserId, userId).subscribe({
      next: (list) => {
        const rows = Array.isArray(list) ? list : [];
        this.historyTotal = rows.length;
        this.historyPending = rows.filter(
          (r) =>
            r.status === 'PendingPartner' ||
            r.status === 'PendingSupervisor' ||
            r.status === 'Pending',
        ).length;
        this.historyApproved = rows.filter((r) => r.status === 'Approved').length;
        this.historyRejected = rows.filter((r) => r.status === 'Rejected').length;
        this.historyLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.historyLoading = false;
        this.historyError = 'Impossible de charger le récapitulatif.';
        this.cdr.detectChanges();
      },
    });
  }

  closeEmployeeHistory(): void {
    this.historyOpen = false;
    this.historyError = '';
  }

  async approve(id: number): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Valider le switch',
      message: 'Valider et appliquer le switch entre les deux employés ?',
      confirmLabel: 'Valider',
      cancelLabel: 'Annuler',
      variant: 'default',
    });
    if (!ok) return;
    this.error = '';
    this.planning.approveChangeRequest(id, this.authUserId).subscribe({
      next: () => {
        this.toast = 'Switch appliqué — demande approuvée.';
        this.reload();
      },
      error: (err) => {
        this.error = err.error?.message ?? 'Échec validation.';
        this.cdr.detectChanges();
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
        this.error = err.error?.message ?? 'Échec du refus.';
        this.cdr.detectChanges();
      },
    });
  }

  statusLabel(s: string): string {
    const map: Record<string, string> = {
      Pending: 'En attente collègue',
      PendingPartner: 'En attente collègue',
      PendingSupervisor: 'En attente superviseur',
      Approved: 'Approuvée',
      Rejected: 'Rejetée',
      Cancelled: 'Annulée',
    };
    return map[s] ?? s;
  }

  get pageSubtitle(): string {
    return this.isRhReadonly
      ? 'Traçabilité des demandes de switch (lecture seule).'
      : 'Validez ou refusez les switches acceptés par le collègue.';
  }
}
