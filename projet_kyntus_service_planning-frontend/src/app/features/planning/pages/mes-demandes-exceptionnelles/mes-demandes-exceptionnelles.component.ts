import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { PlanningService } from '../../../../core/services/planning.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { KyntusConfirmService } from '../../../../shared/components/kyntus-confirm/kyntus-confirm.service';
import { formatWeekLabel } from '../../utils/week-code.util';
import { forkJoin } from 'rxjs';

interface ExceptionalRequest {
  id: number;
  weekCode: string;
  requestedDate: string;
  shiftLabel: string;
  shiftStartTime: string;
  reason: string;
  status: string;
  createdAt: string;
  rejectionReason?: string | null;
  justificationRequired?: boolean;
  hasJustification?: boolean;
  viewerIsRequester?: boolean;
}

interface ShiftOption {
  id: number;
  label: string;
  startTime: string;
}

interface WeekOption {
  weekCode: string;
  weekStartDate: string;
  weekEndDate: string;
  kind: string;
  isPreferred: boolean;
}

function apiMessage(err: unknown): string {
  const e = err as { error?: { message?: string } | string; message?: string };
  if (typeof e?.error === 'string' && e.error.trim()) return e.error;
  if (e?.error && typeof e.error === 'object' && e.error.message) return e.error.message;
  if (typeof e?.message === 'string' && e.message.trim()) return e.message;
  return '';
}

@Component({
  selector: 'app-mes-demandes-exceptionnelles',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  templateUrl: './mes-demandes-exceptionnelles.component.html',
  styleUrls: ['./mes-demandes-exceptionnelles.component.css'],
})
export class MesDemandesExceptionnellesComponent implements OnInit {
  requests: ExceptionalRequest[] = [];
  shifts: ShiftOption[] = [];
  dateOptions: string[] = [];
  loading = true;
  submitting = false;
  errorMsg = '';
  toast = '';
  authUserId = 0;

  weekCode = '';
  weekLabel = '';
  weekOptions: WeekOption[] = [];
  deadlineLabel = '';
  deadlinePassed = false;

  quotaUsed = 0;
  quotaFree = 3;
  justificationRequiredNext = false;

  formDate = '';
  formShiftId: number | null = null;
  formReason = '';
  formFile: File | null = null;

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
    this.reloadAll();
  }

  reloadAll(): void {
    this.loading = true;
    this.errorMsg = '';
    forkJoin({
      list: this.planningSvc.getMyExceptionalRequests(this.authUserId),
      quota: this.planningSvc.getExceptionalQuota(this.authUserId),
      shifts: this.planningSvc.getExceptionalAvailableShifts(this.authUserId),
      target: this.planningSvc.getExceptionalTargetWeek(),
    }).subscribe({
      next: ({ list, quota, shifts, target }) => {
        this.requests = (Array.isArray(list) ? list : []).map((raw) => this.mapRequest(raw));
        this.shifts = (Array.isArray(shifts) ? shifts : []).map((s) => {
          const r = s as Record<string, unknown>;
          return {
            id: Number(r['id'] ?? r['Id'] ?? 0),
            label: String(r['label'] ?? r['Label'] ?? ''),
            startTime: String(r['startTime'] ?? r['StartTime'] ?? ''),
          };
        });

        const q = quota as Record<string, unknown>;
        this.quotaUsed = Number(q['approvedCount'] ?? q['ApprovedCount'] ?? 0);
        this.quotaFree = Number(q['freeRemaining'] ?? q['FreeRemaining'] ?? 0);
        this.justificationRequiredNext = Boolean(
          q['justificationRequiredNext'] ?? q['JustificationRequiredNext'],
        );

        const t = target as Record<string, unknown>;
        this.deadlinePassed = Boolean(t['deadlinePassed'] ?? t['DeadlinePassed']);
        const deadline = String(t['deadlineLocal'] ?? t['DeadlineLocal'] ?? '');
        this.deadlineLabel = deadline ? deadline.replace('T', ' ').slice(0, 16) : '';

        const rawWeeks = (t['availableWeeks'] ?? t['AvailableWeeks'] ?? []) as unknown[];
        this.weekOptions = (Array.isArray(rawWeeks) ? rawWeeks : []).map((w) => {
          const r = w as Record<string, unknown>;
          return {
            weekCode: String(r['weekCode'] ?? r['WeekCode'] ?? ''),
            weekStartDate: String(r['weekStartDate'] ?? r['WeekStartDate'] ?? '').slice(0, 10),
            weekEndDate: String(r['weekEndDate'] ?? r['WeekEndDate'] ?? '').slice(0, 10),
            kind: String(r['kind'] ?? r['Kind'] ?? ''),
            isPreferred: Boolean(r['isPreferred'] ?? r['IsPreferred']),
          };
        }).filter((w) => !!w.weekCode);

        const preferred =
          this.weekOptions.find((w) => w.isPreferred)?.weekCode
          || String(t['weekCode'] ?? t['WeekCode'] ?? '')
          || this.weekOptions[0]?.weekCode
          || '';
        this.applyWeek(preferred || String(t['weekCode'] ?? t['WeekCode'] ?? ''), t);

        if (!this.formShiftId && this.shifts.length) {
          this.formShiftId = this.shifts[0].id;
        }

        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loading = false;
        this.errorMsg = apiMessage(err) || 'Impossible de charger les demandes exceptionnelles.';
        this.cdr.detectChanges();
      },
    });
  }

  onWeekChange(weekCode: string): void {
    this.applyWeek(weekCode);
  }

  weekOptionLabel(w: WeekOption): string {
    const base = formatWeekLabel(w.weekCode);
    const start = (w.weekStartDate || '').slice(0, 10);
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(start);
    const ddmm = m ? `${m[3]}/${m[2]}` : '';
    return ddmm ? `${base} (${ddmm})` : base;
  }

  private applyWeek(weekCode: string, targetFallback?: Record<string, unknown>): void {
    this.weekCode = weekCode;
    this.weekLabel = formatWeekLabel(weekCode);

    const opt = this.weekOptions.find((w) => w.weekCode === weekCode);
    const start = opt?.weekStartDate
      || String(targetFallback?.['weekStartDate'] ?? targetFallback?.['WeekStartDate'] ?? '');
    const end = opt?.weekEndDate
      || String(targetFallback?.['weekEndDate'] ?? targetFallback?.['WeekEndDate'] ?? '');
    this.dateOptions = this.buildWorkDates(start.slice(0, 10), end.slice(0, 10));

    if (!this.dateOptions.includes(this.formDate)) {
      this.formDate = this.dateOptions[0] ?? '';
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.formFile = input.files?.[0] ?? null;
  }

  canCancel(r: ExceptionalRequest): boolean {
    return r.status === 'PendingSupervisor' && !!r.viewerIsRequester;
  }

  submit(): void {
    if (!this.formDate || !this.formShiftId || !this.formReason.trim()) {
      this.toast = 'Date, shift et motif sont obligatoires.';
      return;
    }
    if (this.justificationRequiredNext && !this.formFile) {
      this.toast = 'Justificatif obligatoire (4ᵉ demande et suivantes).';
      return;
    }

    this.submitting = true;
    this.planningSvc
      .createExceptionalRequest(this.authUserId, {
        requestedDate: this.formDate,
        requestedShiftTemplateId: this.formShiftId,
        reason: this.formReason.trim(),
        file: this.formFile,
      })
      .subscribe({
        next: () => {
          this.submitting = false;
          this.formReason = '';
          this.formFile = null;
          this.toast = 'Demande envoyée — en attente du superviseur.';
          this.reloadAll();
        },
        error: (err) => {
          this.submitting = false;
          this.toast = apiMessage(err) || 'Création impossible.';
          this.cdr.detectChanges();
        },
      });
  }

  async cancelRequest(id: number): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: 'Annuler la demande',
      message: 'Annuler cette demande exceptionnelle ?',
      confirmLabel: 'Annuler la demande',
      cancelLabel: 'Retour',
      variant: 'warning',
    });
    if (!ok) return;
    this.planningSvc.cancelExceptionalRequest(id, this.authUserId).subscribe({
      next: () => this.reloadAll(),
      error: (err) => {
        this.toast = apiMessage(err) || 'Annulation impossible.';
        this.cdr.detectChanges();
      },
    });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'PendingSupervisor':
        return 'En attente superviseur';
      case 'PendingRh':
        return 'En attente RH';
      case 'Approved':
        return 'Approuvée';
      case 'Rejected':
        return 'Refusée';
      case 'Cancelled':
        return 'Annulée';
      default:
        return status;
    }
  }

  private mapRequest(raw: unknown): ExceptionalRequest {
    const r = raw as Record<string, unknown>;
    return {
      id: Number(r['id'] ?? r['Id'] ?? 0),
      weekCode: String(r['weekCode'] ?? r['WeekCode'] ?? ''),
      requestedDate: String(r['requestedDate'] ?? r['RequestedDate'] ?? ''),
      shiftLabel: String(r['shiftLabel'] ?? r['ShiftLabel'] ?? ''),
      shiftStartTime: String(r['shiftStartTime'] ?? r['ShiftStartTime'] ?? ''),
      reason: String(r['reason'] ?? r['Reason'] ?? ''),
      status: String(r['status'] ?? r['Status'] ?? ''),
      createdAt: String(r['createdAt'] ?? r['CreatedAt'] ?? ''),
      rejectionReason: (r['rejectionReason'] ?? r['RejectionReason'] ?? null) as string | null,
      justificationRequired: Boolean(r['justificationRequired'] ?? r['JustificationRequired']),
      hasJustification: Boolean(r['hasJustification'] ?? r['HasJustification']),
      viewerIsRequester: Boolean(r['viewerIsRequester'] ?? r['ViewerIsRequester']),
    };
  }

  private buildWorkDates(start: string, end: string): string[] {
    if (!start || !end) return [];
    const from = new Date(start + 'T00:00:00');
    const to = new Date(end + 'T00:00:00');
    const out: string[] = [];
    for (let d = new Date(from); d <= to; d.setDate(d.getDate() + 1)) {
      if (d.getDay() === 0) continue; // dimanche exclu
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      out.push(`${y}-${m}-${day}`);
    }
    return out;
  }
}
