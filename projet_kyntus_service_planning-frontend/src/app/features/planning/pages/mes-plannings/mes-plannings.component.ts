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

/** Mercredi 23:59 (jour local) de la semaine du planning — aligné backend Casablanca. */
function changeRequestDeadlineLocal(weekStartDate: string): Date | null {
  const start = parseDateOnly(weekStartDate);
  if (!start) return null;
  const deadline = new Date(start);
  deadline.setDate(deadline.getDate() + 2);
  deadline.setHours(23, 59, 59, 999);
  return deadline;
}

function parseDateOnly(value: string): Date | null {
  if (!value) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value));
  if (m) {
    return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  }
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

function apiMessage(err: unknown): string {
  const e = err as { error?: { message?: string } | string; message?: string };
  if (typeof e?.error === 'string' && e.error.trim()) return e.error;
  if (e?.error && typeof e.error === 'object' && e.error.message) return e.error.message;
  if (typeof e?.message === 'string' && e.message.trim()) return e.message;
  return '';
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
  changeDeadlinePassed = false;
  changeDeadlineLabel = '';

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
      next: (p) => {
        this.current = this.normalizePlanning(p);
        this.refreshDeadlineState();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.current = null;
        this.loading = false;
        this.cdr.detectChanges();
      },
    });

    this.planningSvc.getMyHistory(authUserId).subscribe({
      next: (list) => {
        this.history = Array.isArray(list)
          ? list.map((p) => this.normalizePlanning(p)!).filter(Boolean)
          : [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.history = [];
        this.cdr.detectChanges();
      },
    });

    this.loadMyRequests();
  }

  loadMyRequests(): void {
    this.planningSvc.getMyChangeRequests(this.authUserId).subscribe({
      next: (list) => {
        this.myRequests = Array.isArray(list) ? list : [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.myRequests = [];
        this.cdr.detectChanges();
      },
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
    return (
      !!d.assignmentId &&
      !d.isOnLeave &&
      !d.isHoliday &&
      !!d.shiftLabel &&
      d.shiftLabel !== '—' &&
      !this.changeDeadlinePassed
    );
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
        this.candidates = (Array.isArray(list) ? list : []).map((c) => ({
          userId: Number(c.userId ?? c.UserId ?? 0),
          fullName: String(c.fullName ?? c.FullName ?? ''),
          level: Number(c.level ?? c.Level ?? 0),
          assignmentId: Number(c.assignmentId ?? c.AssignmentId ?? 0),
          shiftLabel: String(c.shiftLabel ?? c.ShiftLabel ?? ''),
        }));
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.modalError = apiMessage(err) || 'Impossible de charger les candidats.';
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
    this.planningSvc
      .createChangeRequest(this.authUserId, {
        currentAssignmentId: this.selectedDay.assignmentId,
        reason: this.reason.trim(),
        proposedSwapUserId: this.proposedSwapUserId || null,
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.showModal = false;
          this.toast = 'Demande envoyée.';
          this.loadMyRequests();
          this.cdr.detectChanges();
          setTimeout(() => {
            this.toast = '';
            this.cdr.detectChanges();
          }, 3000);
        },
        error: (err) => {
          this.saving = false;
          this.modalError = apiMessage(err) || 'Échec de la demande.';
          this.cdr.detectChanges();
        },
      });
  }

  cancelRequest(id: number): void {
    if (!confirm('Annuler cette demande ?')) return;
    this.planningSvc.cancelChangeRequest(id, this.authUserId).subscribe({
      next: () => this.loadMyRequests(),
      error: (err) => {
        this.toast = apiMessage(err) || 'Annulation impossible.';
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

  private refreshDeadlineState(): void {
    const deadline = this.current
      ? changeRequestDeadlineLocal(this.current.weekStartDate)
      : null;
    if (!deadline) {
      this.changeDeadlinePassed = false;
      this.changeDeadlineLabel = '';
      return;
    }
    this.changeDeadlinePassed = Date.now() > deadline.getTime();
    this.changeDeadlineLabel = deadline.toLocaleString('fr-FR', {
      weekday: 'long',
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  private normalizePlanning(raw: unknown): MyPlanning | null {
    if (!raw || typeof raw !== 'object') return null;
    const p = raw as Record<string, unknown>;
    const daysRaw = p['days'] ?? p['Days'];
    const days = Array.isArray(daysRaw)
      ? daysRaw.map((d) => this.normalizeDay(d))
      : [];
    return {
      weekCode: String(p['weekCode'] ?? p['WeekCode'] ?? ''),
      weekStartDate: String(p['weekStartDate'] ?? p['WeekStartDate'] ?? ''),
      subServiceName: String(p['subServiceName'] ?? p['SubServiceName'] ?? ''),
      days,
    };
  }

  private normalizeDay(raw: unknown): DayAssignment {
    const d = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
    const id = Number(d['assignmentId'] ?? d['AssignmentId'] ?? 0);
    return {
      assignmentId: id > 0 ? id : undefined,
      day: String(d['day'] ?? d['Day'] ?? ''),
      assignedDate: String(d['assignedDate'] ?? d['AssignedDate'] ?? ''),
      shiftLabel: String(d['shiftLabel'] ?? d['ShiftLabel'] ?? ''),
      startTime: String(d['startTime'] ?? d['StartTime'] ?? ''),
      endTime: String(d['endTime'] ?? d['EndTime'] ?? ''),
      breakTime: (d['breakTime'] ?? d['BreakTime'] ?? null) as string | null,
      isSaturday: Boolean(d['isSaturday'] ?? d['IsSaturday']),
      isOnLeave: Boolean(d['isOnLeave'] ?? d['IsOnLeave']),
      isHoliday: Boolean(d['isHoliday'] ?? d['IsHoliday']),
      holidayName: String(d['holidayName'] ?? d['HolidayName'] ?? ''),
      absenceType: (d['absenceType'] ?? d['AbsenceType'] ?? null) as string | null,
      slotLabel: String(d['slotLabel'] ?? d['SlotLabel'] ?? ''),
    };
  }
}
