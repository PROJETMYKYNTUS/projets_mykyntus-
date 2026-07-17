import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { PlanningService } from '../../../../core/services/planning.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';

interface DayAssignment {
  assignmentId?: number;
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

interface ChangeRequest {
  id: number;
  weekCode: string;
  assignmentDate: string;
  shiftLabel: string;
  reason: string;
  proposedSwapUserName?: string | null;
  status: string;
  createdAt: string;
  rejectionReason?: string | null;
}

interface SwapCandidate {
  userId: number;
  fullName: string;
  level: number;
  assignmentId: number;
  shiftLabel: string;
}

@Component({
  selector: 'app-mes-plannings',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  templateUrl: './mes-plannings.component.html',
  styleUrls: ['./mes-plannings.component.css'],
})
export class MesPlanningsComponent implements OnInit {
  current: MyPlanning | null = null;
  history: MyPlanning[] = [];
  myRequests: ChangeRequest[] = [];
  loading = true;
  errorMsg = '';
  toast = '';
  authUserId = 0;

  showModal = false;
  saving = false;
  selectedDay: DayAssignment | null = null;
  reason = '';
  proposedSwapUserId: number | null = null;
  candidates: SwapCandidate[] = [];
  modalError = '';

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
    this.authUserId = authUserId;

    this.planningSvc.getMyCurrentPlanning(authUserId).subscribe({
      next: (p) => { this.current = p ?? null; this.loading = false; this.cdr.detectChanges(); },
      error: () => { this.current = null; this.loading = false; this.cdr.detectChanges(); },
    });

    this.planningSvc.getMyHistory(authUserId).subscribe({
      next: (list) => {
        this.history = Array.isArray(list) ? list : [];
        this.cdr.detectChanges();
      },
      error: () => { this.history = []; this.cdr.detectChanges(); },
    });

    this.loadMyRequests();
  }

  loadMyRequests(): void {
    this.planningSvc.getMyChangeRequests(this.authUserId).subscribe({
      next: (list) => {
        this.myRequests = Array.isArray(list) ? list : [];
        this.cdr.detectChanges();
      },
      error: () => { this.myRequests = []; this.cdr.detectChanges(); },
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

  canRequestChange(d: DayAssignment): boolean {
    return !!d.assignmentId && !d.isOnLeave && !d.isHoliday && !!d.shiftLabel && d.shiftLabel !== '—';
  }

  openChangeModal(d: DayAssignment): void {
    if (!this.canRequestChange(d) || !d.assignmentId) return;
    this.selectedDay = d;
    this.reason = '';
    this.proposedSwapUserId = null;
    this.candidates = [];
    this.modalError = '';
    this.showModal = true;
    this.planningSvc.getSwapCandidates(d.assignmentId, this.authUserId).subscribe({
      next: (list) => {
        this.candidates = Array.isArray(list) ? list : [];
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.modalError = err.error?.message ?? 'Impossible de charger les candidats.';
        this.cdr.detectChanges();
      },
    });
  }

  closeModal(): void {
    this.showModal = false;
    this.selectedDay = null;
  }

  submitChangeRequest(): void {
    if (!this.selectedDay?.assignmentId || !this.reason.trim()) {
      this.modalError = 'Le motif est obligatoire.';
      return;
    }
    this.saving = true;
    this.modalError = '';
    this.planningSvc.createChangeRequest(this.authUserId, {
      currentAssignmentId: this.selectedDay.assignmentId,
      reason: this.reason.trim(),
      proposedSwapUserId: this.proposedSwapUserId || null,
    }).subscribe({
      next: () => {
        this.saving = false;
        this.showModal = false;
        this.toast = 'Demande envoyée.';
        this.loadMyRequests();
        this.cdr.detectChanges();
        setTimeout(() => { this.toast = ''; this.cdr.detectChanges(); }, 3000);
      },
      error: (err) => {
        this.saving = false;
        this.modalError = err.error?.message ?? 'Échec de la demande.';
        this.cdr.detectChanges();
      },
    });
  }

  cancelRequest(id: number): void {
    if (!confirm('Annuler cette demande ?')) return;
    this.planningSvc.cancelChangeRequest(id, this.authUserId).subscribe({
      next: () => this.loadMyRequests(),
      error: (err) => {
        this.toast = err.error?.message ?? 'Annulation impossible.';
        this.cdr.detectChanges();
      },
    });
  }

  statusLabel(status: string): string {
    const map: Record<string, string> = {
      Pending: 'En attente',
      Approved: 'Approuvée',
      Rejected: 'Rejetée',
      Cancelled: 'Annulée',
    };
    return map[status] ?? status;
  }
}
