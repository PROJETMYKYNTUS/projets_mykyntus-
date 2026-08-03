import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { PlanningService } from '../../../../core/services/planning.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import { formatWeekLabel } from '../../utils/week-code.util';

interface ChangeRequest {
  id: number;
  weekCode: string;
  assignmentDate: string;
  shiftLabel: string;
  reason: string;
  requesterUserId?: number;
  proposedSwapUserId?: number | null;
  proposedSwapUserName?: string | null;
  status: string;
  createdAt: string;
  rejectionReason?: string | null;
  viewerIsPartner?: boolean;
  viewerIsRequester?: boolean;
}

function apiMessage(err: unknown): string {
  const e = err as { error?: { message?: string } | string; message?: string };
  if (typeof e?.error === 'string' && e.error.trim()) return e.error;
  if (e?.error && typeof e.error === 'object' && e.error.message) return e.error.message;
  if (typeof e?.message === 'string' && e.message.trim()) return e.message;
  return '';
}

@Component({
  selector: 'app-mes-demandes-changement',
  standalone: true,
  imports: [CommonModule, KyntusPageHeaderComponent],
  templateUrl: './mes-demandes-changement.component.html',
  styleUrls: ['./mes-demandes-changement.component.css'],
})
export class MesDemandesChangementComponent implements OnInit {
  requests: ChangeRequest[] = [];
  loading = true;
  errorMsg = '';
  toast = '';
  authUserId = 0;

  readonly formatWeekLabel = formatWeekLabel;

  constructor(
    private planningSvc: PlanningService,
    private session: KyntusSessionService,
    private cdr: ChangeDetectorRef,
    private confirmService: KyntusConfirmService,
  ) {}

  ngOnInit(): void {
    const authUserId = this.session.getAuthUserId();
    if (!authUserId) {
      this.loading = false;
      this.errorMsg = 'Impossible d’identifier l’utilisateur connecté.';
      return;
    }
    this.authUserId = authUserId;
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.errorMsg = '';
    this.planningSvc.getMyChangeRequests(this.authUserId).subscribe({
      next: (list) => {
        this.requests = (Array.isArray(list) ? list : []).map((raw) => {
          const r = raw as Record<string, unknown>;
          return {
            id: Number(r['id'] ?? r['Id'] ?? 0),
            weekCode: String(r['weekCode'] ?? r['WeekCode'] ?? ''),
            assignmentDate: String(r['assignmentDate'] ?? r['AssignmentDate'] ?? ''),
            shiftLabel: String(r['shiftLabel'] ?? r['ShiftLabel'] ?? ''),
            reason: String(r['reason'] ?? r['Reason'] ?? ''),
            requesterUserId: Number(r['requesterUserId'] ?? r['RequesterUserId'] ?? 0),
            proposedSwapUserId: (r['proposedSwapUserId'] ?? r['ProposedSwapUserId'] ?? null) as number | null,
            proposedSwapUserName: (r['proposedSwapUserName'] ?? r['ProposedSwapUserName'] ?? null) as string | null,
            status: String(r['status'] ?? r['Status'] ?? ''),
            createdAt: String(r['createdAt'] ?? r['CreatedAt'] ?? ''),
            rejectionReason: (r['rejectionReason'] ?? r['RejectionReason'] ?? null) as string | null,
            viewerIsPartner: Boolean(r['viewerIsPartner'] ?? r['ViewerIsPartner']),
            viewerIsRequester: Boolean(r['viewerIsRequester'] ?? r['ViewerIsRequester']),
          };
        });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.errorMsg = 'Impossible de charger vos demandes.';
        this.cdr.detectChanges();
      },
    });
  }

  canCancel(r: ChangeRequest): boolean {
    return (r.status === 'PendingPartner' || r.status === 'Pending') && !!r.viewerIsRequester;
  }

  canPartnerRespond(r: ChangeRequest): boolean {
    return (r.status === 'PendingPartner' || r.status === 'Pending') && !!r.viewerIsPartner;
  }

  async cancelRequest(id: number): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Annuler la demande',
      message: 'Annuler cette demande de switch ?',
      confirmLabel: 'Annuler la demande',
      cancelLabel: 'Retour',
      variant: 'warning',
    });
    if (!ok) return;
    this.planningSvc.cancelChangeRequest(id, this.authUserId).subscribe({
      next: () => this.reload(),
      error: (err) => {
        this.toast = apiMessage(err) || 'Annulation impossible.';
        this.cdr.detectChanges();
      },
    });
  }

  async partnerAccept(id: number): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Accepter le switch',
      message: 'Accepter ce switch ? La demande passera au superviseur.',
      confirmLabel: 'Accepter',
      cancelLabel: 'Annuler',
      variant: 'default',
    });
    if (!ok) return;
    this.planningSvc.partnerAcceptChangeRequest(id, this.authUserId).subscribe({
      next: () => {
        this.toast = 'Switch accepté — en attente du superviseur.';
        this.reload();
      },
      error: (err) => {
        this.toast = apiMessage(err) || 'Acceptation impossible.';
        this.cdr.detectChanges();
      },
    });
  }

  async partnerReject(id: number): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Refuser le switch',
      message: 'Refuser ce switch ? Le demandeur sera notifié.',
      confirmLabel: 'Refuser',
      cancelLabel: 'Annuler',
      variant: 'danger',
    });
    if (!ok) return;
    this.planningSvc.partnerRejectChangeRequest(id, this.authUserId).subscribe({
      next: () => {
        this.toast = 'Switch refusé.';
        this.reload();
      },
      error: (err) => {
        this.toast = apiMessage(err) || 'Refus impossible.';
        this.cdr.detectChanges();
      },
    });
  }

  statusLabel(status: string): string {
    const map: Record<string, string> = {
      Pending: 'En attente collègue',
      PendingPartner: 'En attente collègue',
      PendingSupervisor: 'En attente superviseur',
      Approved: 'Approuvée',
      Rejected: 'Rejetée',
      Cancelled: 'Annulée',
    };
    return map[status] ?? status;
  }
}
