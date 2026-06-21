import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { AllowanceRequestDto } from '../../services/allowance-api.service';
import { AllowanceStatusBadgeComponent } from './allowance-status-badge.component';
import { ALLOWANCE_STATUSES, allowanceSourceLabel } from '../../lib/allowance-status';

function defaultDepartmentLabel(_id: string): string {
  return '—';
}

@Component({
  selector: 'app-allowance-request-table',
  standalone: true,
  imports: [CommonModule, AllowanceStatusBadgeComponent],
  template: `
    <div class="allowance-table-wrap">
      <table class="allowance-table">
        <thead>
          <tr>
            <th>Collaborateur</th>
            <th>Type</th>
            @if (!compact()) {
              <th>Période</th>
            }
            <th>Montant</th>
            @if (!compact()) {
              <th>Motif</th>
              @if (showDepartment()) {
                <th>Département</th>
              }
              <th>Source</th>
            }
            <th>Statut</th>
            @if (showActionsColumn()) {
              <th class="allowance-table__actions-col"></th>
            }
          </tr>
        </thead>
        <tbody>
          @for (r of rows(); track r.id) {
            <tr class="allowance-table__row">
              <td class="allowance-table__name">{{ employeeLabel()(r.employeeId) }}</td>
              <td>{{ r.typeLabel }}</td>
              @if (!compact()) {
                <td class="allowance-table__muted">{{ r.period }}</td>
              }
              <td class="allowance-table__amount">{{ r.amount | number:'1.0-0' }} MAD</td>
              @if (!compact()) {
                <td class="allowance-table__reason" [title]="r.reason">{{ r.reason || '—' }}</td>
                @if (showDepartment()) {
                  <td>{{ departmentLabel()(r.businessDepartmentId) }}</td>
                }
                <td class="allowance-table__muted">{{ sourceLabel(r.source) }}</td>
              }
              <td>
                <app-allowance-status-badge [status]="r.status" [viewer]="statusViewer()" />
              </td>
              @if (showActionsColumn()) {
                <td class="allowance-table__actions">
                  @if (showDraftActions() && r.status === draftStatus) {
                    <button type="button" class="allowance-link" (click)="editDraft.emit(r)">Modifier</button>
                    <button type="button" class="allowance-link allowance-link--success" (click)="submitDraft.emit(r.id)">Soumettre</button>
                  } @else if (r.status !== draftStatus) {
                    <button type="button" class="allowance-link" (click)="viewDetail.emit(r)">Détail</button>
                  }
                </td>
              }
            </tr>
          } @empty {
            <tr>
              <td [attr.colspan]="colSpan()">
                <div class="allowance-empty">
                  <div class="allowance-empty__icon" aria-hidden="true">
                    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2"/><rect x="9" y="3" width="6" height="4" rx="1"/></svg>
                  </div>
                  <p class="allowance-empty__title">Aucune demande</p>
                  <p class="allowance-empty__text">Créez une prime pour un membre de votre équipe N-1.</p>
                  @if (showEmptyCreateAction()) {
                    <button type="button" class="allowance-btn allowance-btn--primary" (click)="createRequest.emit()">
                      + Créer une demande
                    </button>
                  }
                </div>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .allowance-table-wrap {
      background: var(--bg-card, #fff);
      border: 1px solid color-mix(in srgb, var(--border-default, #e5e7eb) 90%, transparent);
      border-radius: 0.75rem;
      overflow: hidden;
    }
    .allowance-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }
    .allowance-table thead {
      background: color-mix(in srgb, #4F46E5 4%, var(--bg-card, #fff));
    }
    .allowance-table th {
      padding: 0.75rem 1rem;
      text-align: left;
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--text-muted, #6b7280);
      border-bottom: 1px solid var(--border-default, #e5e7eb);
    }
    .allowance-table__row {
      transition: background 0.12s;
    }
    .allowance-table__row:hover {
      background: color-mix(in srgb, #4F46E5 3%, var(--bg-card, #fff));
    }
    .allowance-table td {
      padding: 0.875rem 1rem;
      border-bottom: 1px solid color-mix(in srgb, var(--border-default, #e5e7eb) 60%, transparent);
      vertical-align: middle;
    }
    .allowance-table__name { font-weight: 600; color: var(--text-primary, #111827); }
    .allowance-table__amount { font-weight: 600; color: #4F46E5; }
    .allowance-table__muted { color: var(--text-muted, #9ca3af); font-size: 0.8125rem; }
    .allowance-table__reason { max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-muted, #6b7280); }
    .allowance-table__actions { white-space: nowrap; text-align: right; }
    .allowance-table__actions-col { width: 1%; }
    .allowance-link {
      background: none;
      border: none;
      padding: 0.25rem 0.5rem;
      font-size: 0.8125rem;
      font-weight: 600;
      color: #4F46E5;
      cursor: pointer;
      border-radius: 0.25rem;
      transition: background 0.12s;
    }
    .allowance-link:hover { background: color-mix(in srgb, #4F46E5 10%, transparent); }
    .allowance-link--success { color: #22C55E; }
    .allowance-link--success:hover { background: color-mix(in srgb, #22C55E 12%, transparent); }
    .allowance-empty {
      padding: 3rem 1.5rem;
      text-align: center;
    }
    .allowance-empty__icon {
      display: inline-flex;
      color: #4F46E5;
      opacity: 0.5;
      margin-bottom: 0.75rem;
    }
    .allowance-empty__title {
      font-weight: 700;
      font-size: 1rem;
      color: var(--text-primary, #111827);
      margin: 0 0 0.25rem;
    }
    .allowance-empty__text {
      font-size: 0.875rem;
      color: var(--text-muted, #6b7280);
      margin: 0 0 1rem;
    }
    .allowance-btn {
      display: inline-flex;
      align-items: center;
      padding: 0.625rem 1.125rem;
      border-radius: 0.5rem;
      font-size: 0.875rem;
      font-weight: 600;
      cursor: pointer;
      border: none;
      transition: transform 0.1s, box-shadow 0.15s;
    }
    .allowance-btn--primary {
      background: #4F46E5;
      color: #fff;
      box-shadow: 0 4px 14px rgba(79, 70, 229, 0.3);
    }
    .allowance-btn--primary:hover { transform: translateY(-1px); box-shadow: 0 6px 20px rgba(79, 70, 229, 0.4); }
  `],
})
export class AllowanceRequestTableComponent {
  readonly rows = input.required<AllowanceRequestDto[]>();
  readonly employeeLabel = input.required<(id: string) => string>();
  readonly departmentLabel = input<(id: string) => string>(defaultDepartmentLabel);
  readonly showDepartment = input(false);
  readonly showDraftActions = input(false);
  readonly showEmptyCreateAction = input(false);
  readonly compact = input(false);
  readonly statusViewer = input<'manager' | 'stakeholder'>('stakeholder');
  readonly submitDraft = output<string>();
  readonly editDraft = output<AllowanceRequestDto>();
  readonly viewDetail = output<AllowanceRequestDto>();
  readonly createRequest = output<void>();

  readonly draftStatus = ALLOWANCE_STATUSES.Draft;

  sourceLabel(source: string): string {
    return allowanceSourceLabel(source);
  }

  showActionsColumn(): boolean {
    return this.showDraftActions() || this.showEmptyCreateAction();
  }

  colSpan(): number {
    if (this.compact()) {
      let n = 4;
      if (this.showActionsColumn()) n++;
      return n;
    }
    let n = 7;
    if (this.showDepartment()) n++;
    if (this.showActionsColumn()) n++;
    return n;
  }
}
