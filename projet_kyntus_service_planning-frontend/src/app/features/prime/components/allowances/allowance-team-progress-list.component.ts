import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { AllowanceTeamMemberProgressDto } from '../../services/allowance-api.service';
import {
  allowanceTreatmentBadgeClass,
  allowanceTreatmentLabel,
} from '../../lib/allowance-treatment-status';

@Component({
  selector: 'app-allowance-team-progress-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="team-progress-list">
      @for (m of members(); track m.employeeId) {
        <button
          type="button"
          class="team-progress-item"
          [class.team-progress-item--active]="selectedId() === m.employeeId"
          (click)="selectMember.emit(m.employeeId)"
        >
          <div class="team-progress-item__avatar">{{ initials(m) }}</div>
          <div class="team-progress-item__body">
            <span class="team-progress-item__name">{{ memberName(m) }}</span>
            <span class="team-progress-item__meta">
              @if (m.requestCount === 0 && m.treatmentStatus === 'NoBonus') {
                Pas de prime
              } @else if (m.requestCount === 0) {
                Aucune prime
              } @else {
                {{ m.requestCount }} demande(s)
                @if (m.draftCount > 0) {
                  · {{ m.draftCount }} brouillon(s)
                }
              }
            </span>
          </div>
          <span class="allowance-badge" [ngClass]="badgeClass(m.treatmentStatus)">
            {{ statusLabel(m.treatmentStatus) }}
          </span>
        </button>
      } @empty {
        <p class="team-progress-list__empty">Aucun collaborateur N-1.</p>
      }
    </div>
  `,
  styles: [`
    .team-progress-list { display: flex; flex-direction: column; gap: 0.35rem; }
    .team-progress-item {
      display: flex; align-items: center; gap: 0.65rem;
      width: 100%; text-align: left;
      padding: 0.65rem 0.75rem;
      border-radius: 0.625rem;
      border: 1px solid color-mix(in srgb, var(--border-default, #e5e7eb) 90%, transparent);
      background: var(--bg-card, #fff);
      cursor: pointer;
      transition: border-color 0.12s, box-shadow 0.12s, background 0.12s;
    }
    .team-progress-item:hover {
      border-color: color-mix(in srgb, #4F46E5 35%, transparent);
      box-shadow: 0 2px 8px rgba(79, 70, 229, 0.08);
    }
    .team-progress-item--active {
      border-color: #4F46E5;
      background: color-mix(in srgb, #4F46E5 6%, var(--bg-card, #fff));
      box-shadow: 0 0 0 2px rgba(79, 70, 229, 0.12);
    }
    .team-progress-item__avatar {
      width: 2rem; height: 2rem; border-radius: 999px;
      display: flex; align-items: center; justify-content: center;
      font-size: 0.6875rem; font-weight: 700;
      background: color-mix(in srgb, #4F46E5 12%, transparent);
      color: #4F46E5; flex-shrink: 0;
    }
    .team-progress-item__body { flex: 1; min-width: 0; }
    .team-progress-item__name {
      display: block; font-size: 0.875rem; font-weight: 600;
      color: var(--text-primary, #111827);
    }
    .team-progress-item__meta {
      display: block; font-size: 0.75rem; color: var(--text-muted, #6b7280);
    }
    .allowance-badge {
      font-size: 0.625rem; font-weight: 700; text-transform: uppercase;
      letter-spacing: 0.04em; padding: 0.2rem 0.45rem; border-radius: 999px; flex-shrink: 0;
    }
    .allowance-badge--pending { background: #FEF3C7; color: #B45309; }
    .allowance-badge--draft { background: #E0E7FF; color: #4338CA; }
    .allowance-badge--submitted { background: #DBEAFE; color: #1D4ED8; }
    .allowance-badge--validated { background: #DCFCE7; color: #15803D; }
    .allowance-badge--rejected { background: #FEE2E2; color: #B91C1C; }
    .allowance-badge--none { background: #F3F4F6; color: #4B5563; }
    .team-progress-list__empty {
      padding: 1.5rem; text-align: center; font-size: 0.875rem; color: var(--text-muted, #6b7280);
    }
  `],
})
export class AllowanceTeamProgressListComponent {
  readonly members = input.required<AllowanceTeamMemberProgressDto[]>();
  readonly selectedId = input<string | null>(null);
  readonly selectMember = output<string>();

  memberName(m: AllowanceTeamMemberProgressDto): string {
    return `${m.firstName} ${m.lastName}`.trim() || m.email || m.employeeId;
  }

  initials(m: AllowanceTeamMemberProgressDto): string {
    const a = m.firstName?.trim()?.[0] ?? '';
    const b = m.lastName?.trim()?.[0] ?? '';
    return (a + b).toUpperCase() || '?';
  }

  statusLabel(status: string): string {
    return allowanceTreatmentLabel(status);
  }

  badgeClass(status: string): string {
    return allowanceTreatmentBadgeClass(status);
  }
}
