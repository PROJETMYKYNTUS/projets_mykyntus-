import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import type { AllowanceTeamMemberDto, AllowanceTypeDto } from '../../services/allowance-api.service';
import { BodyPortalDirective } from '../../../../shared/directives/body-portal.directive';

@Component({
  selector: 'app-allowance-request-form-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, BodyPortalDirective],
  template: `
    @if (open()) {
      <div class="allowance-modal-backdrop" appBodyPortal (click)="cancelled.emit()">
        <div class="allowance-modal" role="dialog" aria-modal="true" aria-labelledby="allowance-modal-title" (click)="$event.stopPropagation()">
          <header class="allowance-modal__header">
            <div>
              <h2 id="allowance-modal-title" class="allowance-modal__title">{{ title() }}</h2>
              <p class="allowance-modal__hint">Collaborateur → Type → Montant → Motif → Soumettre au RH</p>
            </div>
            <button type="button" class="allowance-modal__close" aria-label="Fermer" (click)="cancelled.emit()">×</button>
          </header>

          <form class="allowance-modal__form" (ngSubmit)="submitted.emit()">
            @if (!hideEmployeePicker() && !editingId()) {
              <label class="allowance-field">
                <span class="allowance-field__label">Collaborateur</span>
                <select class="allowance-field__input" [ngModel]="employeeId()" (ngModelChange)="employeeIdChange.emit($event)" name="emp" required>
                  <option value="">Choisir un collaborateur</option>
                  @for (m of team(); track m.id) {
                    <option [value]="m.id">{{ memberLabel(m) }}</option>
                  }
                </select>
              </label>
            } @else if (hideEmployeePicker() || editingId()) {
              <div class="allowance-field allowance-field--readonly">
                <span class="allowance-field__label">Collaborateur</span>
                <span class="allowance-field__value">{{ employeeName() || '—' }}</span>
              </div>
            }

            <div class="allowance-field-row">
              <label class="allowance-field">
                <span class="allowance-field__label">Type de prime</span>
                <select class="allowance-field__input" [ngModel]="typeId()" (ngModelChange)="typeIdChange.emit($event)" name="type" required>
                  <option value="">Choisir un type</option>
                  @for (t of types(); track t.id) {
                    <option [value]="t.id">{{ t.label }}</option>
                  }
                </select>
              </label>
              <label class="allowance-field">
                <span class="allowance-field__label">Période</span>
                <input class="allowance-field__input" type="month" [ngModel]="period()" (ngModelChange)="periodChange.emit($event)" name="period" required />
              </label>
            </div>

            <label class="allowance-field">
              <span class="allowance-field__label">Montant (MAD)</span>
              <input type="number" class="allowance-field__input allowance-field__input--amount" [ngModel]="amount()" (ngModelChange)="amountChange.emit($event)" name="amount" required min="0" step="1" />
            </label>

            <label class="allowance-field">
              <span class="allowance-field__label">Motif</span>
              <textarea class="allowance-field__input allowance-field__input--textarea" rows="3" [ngModel]="reason()" (ngModelChange)="reasonChange.emit($event)" name="reason" placeholder="Expliquez la raison de cette prime…"></textarea>
            </label>

            @if (error()) {
              <div class="allowance-modal__error">{{ error() }}</div>
            }

            <footer class="allowance-modal__footer">
              <button type="button" class="allowance-btn allowance-btn--ghost" (click)="cancelled.emit()">Annuler</button>
              <button type="button" class="allowance-btn allowance-btn--ghost" (click)="resetRequested.emit()" [disabled]="saving()">Réinitialiser</button>
              <button type="submit" class="allowance-btn allowance-btn--primary" [disabled]="saving()">
                @if (saving()) {
                  <span class="allowance-spinner" aria-hidden="true"></span>
                }
                {{ submitLabel() }}
              </button>
            </footer>
          </form>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .allowance-modal-backdrop {
      position: fixed;
      inset: 0;
      z-index: 60;
      background: color-mix(in srgb, var(--navy-950) 45%, transparent);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      animation: allowance-fade-in 0.15s ease-out;
    }
    .allowance-modal {
      width: 100%;
      max-width: 28rem;
      background: var(--bg-card);
      border: 1px solid color-mix(in srgb, var(--border-color) 90%, transparent);
      border-radius: var(--radius-card, 0.875rem);
      box-shadow: var(--shadow-3);
      overflow: hidden;
      animation: allowance-slide-up 0.2s ease-out;
    }
    .allowance-modal__header {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      padding: 1.25rem 1.25rem 0.75rem;
      border-bottom: 1px solid color-mix(in srgb, var(--border-color) 70%, transparent);
    }
    .allowance-modal__title {
      font-size: 1.125rem;
      font-weight: 700;
      color: var(--text-primary);
      margin: 0;
    }
    .allowance-modal__hint {
      font-size: 0.75rem;
      color: var(--text-muted);
      margin: 0.25rem 0 0;
    }
    .allowance-modal__close {
      border: none;
      background: transparent;
      font-size: 1.5rem;
      line-height: 1;
      color: var(--text-muted);
      cursor: pointer;
      padding: 0.125rem 0.375rem;
      border-radius: var(--radius-md, 0.5rem);
    }
    .allowance-modal__close:hover { background: color-mix(in srgb, var(--border-color) 50%, transparent); }
    .allowance-modal__form { padding: 1rem 1.25rem 1.25rem; display: flex; flex-direction: column; gap: 0.875rem; }
    .allowance-field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    @media (max-width: 480px) { .allowance-field-row { grid-template-columns: 1fr; } }
    .allowance-field { display: flex; flex-direction: column; gap: 0.35rem; }
    .allowance-field--readonly { padding: 0.5rem 0.75rem; background: color-mix(in srgb, var(--electric-blue) 6%, var(--bg-card)); border-radius: var(--radius-md, 0.5rem); }
    .allowance-field__label { font-size: 0.75rem; font-weight: 600; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.03em; }
    .allowance-field__value { font-size: 0.9375rem; font-weight: 600; color: var(--text-primary); }
    .allowance-field__input {
      width: 100%;
      padding: 0.625rem 0.75rem;
      border-radius: var(--radius-md, 0.5rem);
      border: 1px solid var(--border-color);
      background: var(--bg-input);
      color: var(--text-primary);
      font-size: 0.9375rem;
      transition: border-color 0.15s, box-shadow 0.15s;
    }
    .allowance-field__input:focus {
      outline: none;
      border-color: var(--electric-blue);
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--electric-blue) 15%, transparent);
    }
    .allowance-field__input--amount { font-size: 1.125rem; font-weight: 600; }
    .allowance-field__input--textarea { resize: vertical; min-height: 4.5rem; }
    .allowance-modal__error {
      padding: 0.625rem 0.75rem;
      border-radius: var(--radius-md, 0.5rem);
      background: var(--danger-bg);
      color: var(--danger-text);
      font-size: 0.8125rem;
    }
    .allowance-modal__footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
      padding-top: 0.25rem;
    }
    .allowance-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.625rem 1rem;
      border-radius: var(--radius-md, 0.5rem);
      font-size: 0.875rem;
      font-weight: 600;
      cursor: pointer;
      border: none;
      transition: transform 0.1s, box-shadow 0.15s, opacity 0.15s;
    }
    .allowance-btn:hover:not(:disabled) { transform: translateY(-1px); }
    .allowance-btn:disabled { opacity: 0.65; cursor: not-allowed; }
    .allowance-btn--primary {
      background: var(--ky-gradient);
      color: white;
      box-shadow: var(--shadow-2);
    }
    .allowance-btn--primary:hover:not(:disabled) { box-shadow: var(--shadow-3); }
    .allowance-btn--ghost {
      background: transparent;
      color: var(--text-muted);
      border: 1px solid var(--border-color);
    }
    .allowance-spinner {
      width: 0.875rem;
      height: 0.875rem;
      border: 2px solid color-mix(in srgb, white 35%, transparent);
      border-top-color: white;
      border-radius: 50%;
      animation: allowance-spin 0.6s linear infinite;
    }
    @keyframes allowance-fade-in { from { opacity: 0; } to { opacity: 1; } }
    @keyframes allowance-slide-up { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
    @keyframes allowance-spin { to { transform: rotate(360deg); } }
  `],
})
export class AllowanceRequestFormModalComponent {
  readonly open = input(false);
  readonly title = input('Créer une demande');
  readonly submitLabel = input('Créer brouillon');
  readonly saving = input(false);
  readonly error = input('');
  readonly editingId = input<string | null>(null);
  readonly hideEmployeePicker = input(false);
  readonly employeeName = input('');
  readonly team = input<AllowanceTeamMemberDto[]>([]);
  readonly types = input<AllowanceTypeDto[]>([]);

  readonly employeeId = input('');
  readonly typeId = input('');
  readonly period = input('');
  readonly amount = input(0);
  readonly reason = input('');

  readonly employeeIdChange = output<string>();
  readonly typeIdChange = output<string>();
  readonly periodChange = output<string>();
  readonly amountChange = output<number>();
  readonly reasonChange = output<string>();
  readonly submitted = output<void>();
  readonly cancelled = output<void>();
  readonly resetRequested = output<void>();

  memberLabel(m: AllowanceTeamMemberDto): string {
    return `${m.firstName} ${m.lastName}`.trim() || m.email || m.id;
  }
}
