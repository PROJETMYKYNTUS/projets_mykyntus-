import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { KyntusPageHeaderComponent } from '../../../../shared/components/ui/kyntus-page-header.component';
import { PlanningService } from '../../services/planning.service';
import { KyntusSessionService } from '../../../../core/session/kyntus-session.service';
import { formatWeekLabel } from '../../utils/week-code.util';

function apiMessage(err: unknown): string {
  const e = err as { error?: { message?: string } | string; message?: string };
  if (typeof e?.error === 'string' && e.error.trim()) return e.error;
  if (e?.error && typeof e.error === 'object' && e.error.message) return e.error.message;
  if (typeof e?.message === 'string' && e.message.trim()) return e.message;
  return '';
}

@Component({
  selector: 'app-mes-renforts',
  standalone: true,
  imports: [CommonModule, FormsModule, KyntusPageHeaderComponent],
  templateUrl: './mes-renforts.component.html',
  styleUrls: ['./mes-renforts.component.css'],
})
export class MesRenfortsComponent implements OnInit {
  requests: any[] = [];
  loading = false;
  error = '';
  toast = '';
  authUserId = 0;
  readonly formatWeekLabel = formatWeekLabel;

  constructor(
    private planning: PlanningService,
    private session: KyntusSessionService,
    private cdr: ChangeDetectorRef,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.authUserId = this.session.getAuthUserId() ?? 0;
    if (!this.authUserId) {
      void this.router.navigate(['/login']);
      return;
    }
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.error = '';
    this.planning.getMyReinforcementRequests(this.authUserId).subscribe({
      next: (list) => {
        this.requests = list ?? [];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loading = false;
        this.error = 'Impossible de charger les renforts.';
        this.cdr.detectChanges();
      },
    });
  }

  myStatus(r: any): string {
    return String(r.myVolunteerStatus ?? '');
  }

  canRespond(r: any): boolean {
    return r.status === 'Open' && (this.myStatus(r) === 'Pending' || this.myStatus(r) === '');
  }

  statusLabel(s: string): string {
    const map: Record<string, string> = {
      Open: 'Ouvert',
      Filled: 'Pourvu',
      Cancelled: 'Annulé',
      Pending: 'En attente de votre réponse',
      Accepted: 'Vous avez accepté',
      Declined: 'Vous avez refusé',
      Selected: 'Vous êtes sélectionné(e)',
      Rejected: 'Non retenu',
    };
    return map[s] ?? s;
  }

  accept(id: number): void {
    this.planning.volunteerAcceptReinforcement(id, this.authUserId).subscribe({
      next: () => {
        this.toast = 'Acceptation enregistrée.';
        this.reload();
      },
      error: (err) => {
        this.toast = apiMessage(err) || 'Acceptation impossible.';
        this.cdr.detectChanges();
      },
    });
  }

  decline(id: number): void {
    this.planning.volunteerDeclineReinforcement(id, this.authUserId).subscribe({
      next: () => {
        this.toast = 'Refus enregistré.';
        this.reload();
      },
      error: (err) => {
        this.toast = apiMessage(err) || 'Refus impossible.';
        this.cdr.detectChanges();
      },
    });
  }
}
