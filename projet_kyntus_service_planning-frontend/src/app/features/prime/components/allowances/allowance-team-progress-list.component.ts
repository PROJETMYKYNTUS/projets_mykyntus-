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
      border-radius: var(--radius-md);
      border: 1px solid color-mix(in srgb, var(--border-color) 90%, transparent);
      background: var(--bg-card);
      cursor: pointer;
      transition: border-color 0.12s, box-shadow 0.12s, background 0.12s;
    }
    .team-progress-item:hover {
      border-color: color-mix(in srgb, var(--electric-blue) 35%, transparent);
      box-shadow: 0 2px 8px color-mix(in srgb, var(--electric-blue) 8%, transparent);
    }
    .team-progress-item--active {
      border-color: var(--electric-blue);
      background: color-mix(in srgb, var(--electric-blue) 6%, var(--bg-card));
      box-shadow: 0 0 0 2px color-mix(in srgb, var(--electric-blue) 12%, transparent);
    }
    .team-progress-item__avatar {
      width: 2rem; height: 2rem; border-radius: var(--radius-pill);
      display: flex; align-items: center; justify-content: center;
      font-size: 0.6875rem; font-weight: 700;
      background: color-mix(in srgb, var(--electric-blue) 12%, transparent);
      color: var(--electric-blue); flex-shrink: 0;
    }
    .team-progress-item__body { flex: 1; min-width: 0; }
    .team-progress-item__name {
      display: block; font-size: 0.875rem; font-weight: 600;
      color: var(--text-primary);
    }
    .team-progress-item__meta {
      display: block; font-size: 0.75rem; color: var(--text-muted);
    }
    .allowance-badge {
      font-size: 0.625rem; font-weight: 700; text-transform: uppercase;
      letter-spacing: 0.04em; padding: 0.2rem 0.45rem; border-radius: var(--radius-pill); flex-shrink: 0;
    }
    .allowance-badge--pending { background: var(--warning-bg); color: var(--warning-text); }
    .allowance-badge--draft { background: color-mix(in srgb, var(--electric-blue) 12%, var(--bg-card)); color: var(--electric-blue); }
    .allowance-badge--submitted { background: var(--info-bg); color: var(--info-text); }
    .allowance-badge--validated { background: var(--success-bg); color: var(--success-text); }
    .allowance-badge--rejected { background: var(--danger-bg); color: var(--danger-text); }
    .allowance-badge--none { background: var(--surface-3); color: var(--text-muted); }
    .team-progress-list__empty {
      padding: 1.5rem; text-align: center; font-size: 0.875rem; color: var(--text-muted);
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
