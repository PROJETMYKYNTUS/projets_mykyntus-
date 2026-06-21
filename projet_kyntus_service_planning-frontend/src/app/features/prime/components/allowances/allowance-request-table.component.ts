import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { AllowanceRequestDto } from '../../services/allowance-api.service';
import { AllowanceStatusBadgeComponent } from './allowance-status-badge.component';
import { PrimeCardComponent } from '../prime-card.component';
import { ALLOWANCE_STATUSES, allowanceSourceLabel } from '../../lib/allowance-status';

function defaultDepartmentLabel(_id: string): string {
  return '—';
}

@Component({
  selector: 'app-allowance-request-table',
  standalone: true,
  imports: [CommonModule, AllowanceStatusBadgeComponent, PrimeCardComponent],
  template: `
    <app-prime-card className="ky-table-card p-0">
      <div class="overflow-x-auto">
        <table class="prime-table w-full text-sm">
        <thead>
          <tr class="text-left text-muted border-b border-default">
            <th class="p-3 font-medium">Collaborateur</th>
            <th class="p-3">Type</th>
            <th class="p-3">Période</th>
            <th class="p-3">Montant</th>
            <th class="p-3">Motif</th>
            @if (showDepartment()) {
              <th class="p-3">Département</th>
            }
            <th class="p-3">Source</th>
            <th class="p-3">Statut</th>
            @if (showDraftActions()) {
              <th class="p-3"></th>
            }
          </tr>
        </thead>
        <tbody>
          @for (r of rows(); track r.id) {
            <tr class="border-b border-default/50">
              <td class="p-3">{{ employeeLabel()(r.employeeId) }}</td>
              <td class="p-3">{{ r.typeLabel }}</td>
              <td class="p-3">{{ r.period }}</td>
              <td class="p-3">{{ r.amount | number:'1.0-2' }} {{ r.currency }}</td>
              <td class="p-3 max-w-[220px] truncate" [title]="r.reason">{{ r.reason || '—' }}</td>
              @if (showDepartment()) {
                <td class="p-3">{{ departmentLabel()(r.businessDepartmentId) }}</td>
              }
              <td class="p-3">{{ sourceLabel(r.source) }}</td>
              <td class="p-3">
                <app-allowance-status-badge [status]="r.status" [viewer]="statusViewer()" />
              </td>
              @if (showDraftActions()) {
                <td class="p-3 whitespace-nowrap space-x-2">
                  @if (r.status === draftStatus) {
                    <button type="button" class="text-indigo-400 text-xs" (click)="editDraft.emit(r)">
                      Modifier
                    </button>
                    <button type="button" class="text-emerald-400 text-xs" (click)="submitDraft.emit(r.id)">
                      Soumettre au RH
                    </button>
                  }
                </td>
              }
            </tr>
          } @empty {
            <tr>
              <td class="p-4 text-muted" [attr.colspan]="colSpan()">Aucune demande.</td>
            </tr>
          }
        </tbody>
        </table>
      </div>
    </app-prime-card>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowanceRequestTableComponent {
  readonly rows = input.required<AllowanceRequestDto[]>();
  readonly employeeLabel = input.required<(id: string) => string>();
  readonly departmentLabel = input<(id: string) => string>(defaultDepartmentLabel);
  readonly showDepartment = input(false);
  readonly showDraftActions = input(false);
  readonly statusViewer = input<'manager' | 'stakeholder'>('stakeholder');
  readonly submitDraft = output<string>();
  readonly editDraft = output<AllowanceRequestDto>();

  readonly draftStatus = ALLOWANCE_STATUSES.Draft;

  sourceLabel(source: string): string {
    return allowanceSourceLabel(source);
  }

  colSpan(): number {
    let n = 7;
    if (this.showDepartment()) n++;
    if (this.showDraftActions()) n++;
    return n;
  }
}
