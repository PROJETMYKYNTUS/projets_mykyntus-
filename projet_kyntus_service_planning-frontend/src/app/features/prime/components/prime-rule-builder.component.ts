import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  signal,
} from '@angular/core';
import type { Department, PrimeType } from '../models';

interface RuleDraft {
  primeTypeId: string;
  departmentId: string;
  conditionField: string;
  conditionType: string;
  targetValue: string;
  calculationMethod: string;
  amount: string;
  period: string;
}

@Component({
  selector: 'app-prime-rule-builder',
  standalone: true,
  template: `
    <form (ngSubmit)="submit($event)" class="space-y-6">
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label class="block text-sm font-medium text-muted mb-1">Prime Type</label>
          <select
            class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-app text-primary"
            required
            [value]="rule().primeTypeId"
            (change)="patch('primeTypeId', $any($event.target).value)"
          >
            <option value="">Select a type</option>
            @for (t of types; track t.id) {
              <option [value]="t.id">{{ t.name }}</option>
            }
          </select>
        </div>
        <div>
          <label class="block text-sm font-medium text-muted mb-1">Department</label>
          <select
            class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-app text-primary"
            [value]="rule().departmentId"
            (change)="patch('departmentId', $any($event.target).value)"
          >
            <option value="">All Departments</option>
            @for (d of departments; track d.id) {
              <option [value]="d.id">{{ d.name }}</option>
            }
          </select>
        </div>
      </div>

      <div class="bg-app p-4 rounded-xl border border-default space-y-4">
        <h4 class="text-sm font-semibold text-primary">Condition</h4>
        <div class="flex flex-col sm:flex-row gap-3 items-center">
          <div class="w-full sm:w-1/3">
            <select
              class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-card text-primary"
              required
              [value]="rule().conditionField"
              (change)="patch('conditionField', $any($event.target).value)"
            >
              <option value="">Select Field</option>
              <option value="tickets_resolved">Tickets Resolved</option>
              <option value="csat_score">CSAT Score</option>
              <option value="errors">Errors</option>
              <option value="attendance_rate">Attendance Rate</option>
            </select>
          </div>
          <div class="w-full sm:w-1/4">
            <select
              class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-card text-primary"
              required
              [value]="rule().conditionType"
              (change)="patch('conditionType', $any($event.target).value)"
            >
              <option value=">">Greater than (&gt;)</option>
              <option value="<">Less than (&lt;)</option>
              <option value=">=">Greater or equal (&gt;=)</option>
              <option value="<=">Less or equal (&lt;=)</option>
              <option value="==">Equals (==)</option>
            </select>
          </div>
          <div class="w-full sm:w-1/3">
            <input
              type="number"
              class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-card text-primary placeholder:text-muted"
              placeholder="Target value"
              required
              [value]="rule().targetValue"
              (input)="patch('targetValue', $any($event.target).value)"
            />
          </div>
        </div>
      </div>

      <div class="bg-app p-4 rounded-xl border border-default space-y-4">
        <h4 class="text-sm font-semibold text-primary">Reward</h4>
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div>
            <label class="block text-sm font-medium text-muted mb-1">Method</label>
            <select
              class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-card text-primary"
              required
              [value]="rule().calculationMethod"
              (change)="patch('calculationMethod', $any($event.target).value)"
            >
              <option value="Fixed">Fixed Amount</option>
              <option value="Percentage">Percentage</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-muted mb-1">Amount / %</label>
            <input
              type="number"
              class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-card text-primary placeholder:text-muted"
              placeholder="e.g. 300"
              required
              [value]="rule().amount"
              (input)="patch('amount', $any($event.target).value)"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-muted mb-1">Period</label>
            <select
              class="w-full px-3 py-2 border border-default rounded-lg focus:ring-blue-500 focus:border-blue-500 bg-card text-primary"
              required
              [value]="rule().period"
              (change)="patch('period', $any($event.target).value)"
            >
              <option value="Monthly">Monthly</option>
              <option value="Quarterly">Quarterly</option>
              <option value="Yearly">Yearly</option>
            </select>
          </div>
        </div>
      </div>

      <div class="flex justify-end gap-3 pt-2">
        <button
          type="button"
          (click)="cancel.emit()"
          class="px-4 py-2 text-primary hover:bg-app rounded-lg font-medium transition-colors border border-default"
        >
          Cancel
        </button>
        <button
          type="submit"
          class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium transition-colors shadow-sm"
        >
          Save Rule
        </button>
      </div>
    </form>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrimeRuleBuilderComponent {
  @Input() types: PrimeType[] = [];
  @Input() departments: Department[] = [];
  @Output() save = new EventEmitter<RuleDraft>();
  @Output() cancel = new EventEmitter<void>();

  readonly rule = signal<RuleDraft>({
    primeTypeId: '',
    departmentId: '',
    conditionField: '',
    conditionType: '>',
    targetValue: '',
    calculationMethod: 'Fixed',
    amount: '',
    period: 'Monthly',
  });

  patch(key: keyof RuleDraft, value: string): void {
    this.rule.update((current) => ({ ...current, [key]: value }));
  }

  submit(event: Event): void {
    event.preventDefault();
    this.save.emit(this.rule());
  }
}
