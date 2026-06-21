import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { AllowanceRequestDto, AllowanceTypeDto } from '../../services/allowance-api.service';
import { AllowanceStatusBadgeComponent } from './allowance-status-badge.component';
import { ALLOWANCE_STATUSES } from '../../lib/allowance-status';

@Component({
  selector: 'app-allowance-employee-allocations-panel',
  standalone: true,
  imports: [CommonModule, AllowanceStatusBadgeComponent],
  template: `
    <div class="alloc-panel">
      @if (!employeeId()) {
        <div class="alloc-panel__placeholder">
          <p class="alloc-panel__placeholder-title">Sélectionnez un collaborateur</p>
          <p class="alloc-panel__placeholder-text">Choisissez un membre de votre équipe à gauche pour voir ou créer ses primes.</p>
        </div>
      } @else {
        <header class="alloc-panel__header">
          <div>
            <h2 class="alloc-panel__title">{{ employeeName() }}</h2>
            <p class="alloc-panel__period">Période {{ period() }}</p>
          </div>
          @if (availableTypes().length > 0) {
            <button type="button" class="alloc-panel__add" (click)="addType.emit()">
              + Ajouter un type
            </button>
          }
        </header>

        @if (loading()) {
          <div class="alloc-panel__loading">
            <span class="alloc-spinner"></span>
          </div>
        } @else if (requests().length === 0 && noBonusMarked()) {
          <div class="alloc-panel__empty">
            <p class="alloc-panel__nobonus-title">Traité — pas de prime ce mois</p>
            @if (noBonusComment()) {
              <p class="alloc-panel__nobonus-comment">{{ noBonusComment() }}</p>
            }
            <button type="button" class="alloc-panel__secondary" (click)="clearNoBonus.emit()">
              Annuler et affecter une prime
            </button>
          </div>
        } @else if (requests().length === 0) {
          <div class="alloc-panel__empty">
            <p>Aucune prime affectée pour cette période.</p>
            @if (availableTypes().length > 0) {
              <button type="button" class="alloc-panel__add alloc-panel__add--lg" (click)="addType.emit()">
                + Ajouter un type de prime
              </button>
            }
            <button type="button" class="alloc-panel__nobonus" (click)="markNoBonus.emit()">
              Aucune prime ce mois
            </button>
          </div>
        } @else {
          <div class="alloc-table-wrap">
            <table class="alloc-table">
              <thead>
                <tr>
                  <th>Type</th>
                  <th>Montant</th>
                  <th>Motif</th>
                  <th>Statut</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (r of requests(); track r.id) {
                  <tr>
                    <td class="alloc-table__type">{{ r.typeLabel }}</td>
                    <td class="alloc-table__amount">{{ r.amount | number:'1.0-0' }} MAD</td>
                    <td class="alloc-table__reason" [title]="r.reason">{{ r.reason || '—' }}</td>
                    <td><app-allowance-status-badge [status]="r.status" [viewer]="'manager'" /></td>
                    <td class="alloc-table__actions">
                      @if (r.status === draftStatus) {
                        <button type="button" class="alloc-link" (click)="editRequest.emit(r)">Modifier</button>
                        <button type="button" class="alloc-link alloc-link--success" (click)="submitRequest.emit(r.id)">Soumettre</button>
                      } @else {
                        <button type="button" class="alloc-link" (click)="viewRequest.emit(r)">Détail</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (availableTypes().length > 0) {
            <button type="button" class="alloc-panel__add-secondary" (click)="addType.emit()">
              + Ajouter un autre type
            </button>
          }
        }
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .alloc-panel { min-height: 20rem; }
    .alloc-panel__placeholder {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      min-height: 18rem; text-align: center; padding: 2rem;
      border: 1px dashed color-mix(in srgb, var(--border-default, #d1d5db) 80%, transparent);
      border-radius: 0.75rem;
    }
    .alloc-panel__placeholder-title { font-weight: 700; color: var(--text-primary, #111827); margin: 0 0 0.35rem; }
    .alloc-panel__placeholder-text { font-size: 0.875rem; color: var(--text-muted, #6b7280); margin: 0; max-width: 20rem; }
    .alloc-panel__header {
      display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-start;
      gap: 0.75rem; margin-bottom: 1rem;
    }
    .alloc-panel__title { margin: 0; font-size: 1.125rem; font-weight: 700; color: var(--text-primary, #111827); }
    .alloc-panel__period { margin: 0.15rem 0 0; font-size: 0.8125rem; color: var(--text-muted, #6b7280); }
    .alloc-panel__add, .alloc-panel__add--lg {
      padding: 0.5rem 1rem; border: none; border-radius: 0.5rem;
      background: #4F46E5; color: #fff; font-size: 0.8125rem; font-weight: 700; cursor: pointer;
      box-shadow: 0 4px 12px rgba(79, 70, 229, 0.25);
    }
    .alloc-panel__add--lg { margin-top: 0.75rem; }
    .alloc-panel__add-secondary {
      margin-top: 0.75rem; background: none; border: none;
      color: #4F46E5; font-size: 0.8125rem; font-weight: 600; cursor: pointer;
    }
    .alloc-panel__empty {
      padding: 2rem 1rem; text-align: center; font-size: 0.875rem; color: var(--text-muted, #6b7280);
      display: flex; flex-direction: column; align-items: center; gap: 0.65rem;
    }
    .alloc-panel__nobonus-title { font-weight: 700; color: #4B5563; margin: 0; }
    .alloc-panel__nobonus-comment { font-size: 0.8125rem; margin: 0; max-width: 20rem; }
    .alloc-panel__nobonus, .alloc-panel__secondary {
      margin-top: 0.25rem; padding: 0.45rem 0.875rem; border-radius: 0.5rem;
      font-size: 0.8125rem; font-weight: 600; cursor: pointer;
      border: 1px solid var(--border-default, #d1d5db); background: var(--bg-card, #fff);
      color: var(--text-muted, #6b7280);
    }
    .alloc-panel__secondary { color: #4F46E5; border-color: color-mix(in srgb, #4F46E5 30%, transparent); }
    .alloc-panel__loading { display: flex; justify-content: center; padding: 3rem; }
    .alloc-spinner {
      width: 1.5rem; height: 1.5rem;
      border: 3px solid rgba(79, 70, 229, 0.2); border-top-color: #4F46E5;
      border-radius: 50%; animation: spin 0.6s linear infinite;
    }
    .alloc-table-wrap {
      border: 1px solid color-mix(in srgb, var(--border-default, #e5e7eb) 90%, transparent);
      border-radius: 0.625rem; overflow: hidden;
    }
    .alloc-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
    .alloc-table th {
      text-align: left; padding: 0.625rem 0.875rem;
      font-size: 0.6875rem; text-transform: uppercase; letter-spacing: 0.04em;
      color: var(--text-muted, #6b7280);
      background: color-mix(in srgb, #4F46E5 4%, var(--bg-card, #fff));
      border-bottom: 1px solid var(--border-default, #e5e7eb);
    }
    .alloc-table td { padding: 0.75rem 0.875rem; border-bottom: 1px solid color-mix(in srgb, var(--border-default, #e5e7eb) 60%, transparent); vertical-align: middle; }
    .alloc-table__type { font-weight: 600; }
    .alloc-table__amount { font-weight: 600; color: #4F46E5; }
    .alloc-table__reason { max-width: 160px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-muted, #6b7280); }
    .alloc-table__actions { white-space: nowrap; text-align: right; }
    .alloc-link {
      background: none; border: none; padding: 0.2rem 0.4rem;
      font-size: 0.75rem; font-weight: 600; color: #4F46E5; cursor: pointer;
    }
    .alloc-link--success { color: #16A34A; }
    @keyframes spin { to { transform: rotate(360deg); } }
  `],
})
export class AllowanceEmployeeAllocationsPanelComponent {
  readonly employeeId = input<string | null>(null);
  readonly employeeName = input('');
  readonly period = input('');
  readonly loading = input(false);
  readonly requests = input<AllowanceRequestDto[]>([]);
  readonly availableTypes = input<AllowanceTypeDto[]>([]);
  readonly noBonusMarked = input(false);
  readonly noBonusComment = input<string | undefined>(undefined);

  readonly addType = output<void>();
  readonly markNoBonus = output<void>();
  readonly clearNoBonus = output<void>();
  readonly editRequest = output<AllowanceRequestDto>();
  readonly submitRequest = output<string>();
  readonly viewRequest = output<AllowanceRequestDto>();

  readonly draftStatus = ALLOWANCE_STATUSES.Draft;
}
