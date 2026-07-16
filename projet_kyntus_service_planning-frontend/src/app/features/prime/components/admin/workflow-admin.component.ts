import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import type { AdminWorkflowConfig, WorkflowAction } from '../../mock-data/admin';
import { WORKFLOW_ACTIONS } from '../../mock-data/admin';

const roleDetails: Record<string, { description: string; responsibilities: string }> = {
  Coach: { description: 'Encadrement direct des pilotes.', responsibilities: 'Valider/Rejeter en premier niveau.' },
  Manager: { description: "Supervision de l'équipe.", responsibilities: 'Contrôler la cohérence métier et approuver.' },
  RP: { description: 'Pilotage projet.', responsibilities: 'Arbitrer la validation finale projet avant RH.' },
  RH: { description: 'Validation finale obligatoire.', responsibilities: 'Décision finale et archivage.' },
  Superviseur: { description: 'Supervision.', responsibilities: 'Validation.' },
};

@Component({
  selector: 'app-workflow-admin',
  standalone: true,
  imports: [],
  template: `
    <div class="card-navy p-6 space-y-6">
      <div class="space-y-3">
        @for (step of workflow().steps; track step.id; let index = $index) {
          <div class="flex items-center justify-between p-3 rounded-lg border border-default bg-card/40">
            <span class="text-primary">{{ index + 1 }}. {{ step.role }}</span>
            <div class="flex gap-2 items-center">
              <button
                type="button"
                (click)="moveStep(index, -1)"
                [disabled]="step.role === 'RH'"
                class="px-2 py-1 rounded border border-default bg-input text-primary hover:bg-input disabled:cursor-not-allowed disabled:border-default disabled:bg-card disabled:text-muted"
              >
                ↑
              </button>
              <button
                type="button"
                (click)="moveStep(index, 1)"
                [disabled]="step.role === 'RH'"
                class="px-2 py-1 rounded border border-default bg-input text-primary hover:bg-input disabled:cursor-not-allowed disabled:border-default disabled:bg-card disabled:text-muted"
              >
                ↓
              </button>
              <button
                type="button"
                (click)="openConfig(step.id)"
                class="px-2 py-1 rounded border border-default bg-input text-primary hover:bg-input"
              >
                Modifier
              </button>
              <button
                type="button"
                (click)="openDetails(step.id)"
                class="px-2 py-1 rounded border border-default bg-input text-primary hover:bg-input"
              >
                Détails
              </button>
            </div>
          </div>
        }
      </div>

      <div class="grid md:grid-cols-2 gap-4">
        <label class="text-muted text-sm"
          >SLA global (heures)
          <input
            type="number"
            min="0"
            class="mt-1 w-full bg-input border border-default rounded-lg p-3 text-sm text-primary"
            [value]="globalSla()"
            (input)="globalSla.set(+$any($event.target).value || 0)"
          />
        </label>
        <label class="text-muted text-sm flex items-center gap-2 mt-7">
          <input
            type="checkbox"
            [checked]="globalNotifications()"
            (change)="globalNotifications.set($any($event.target).checked)"
          />
          Activer notifications
        </label>
      </div>

      <div class="border border-default rounded-xl p-4 bg-card/30">
        <h5 class="text-primary font-bold">Audit & accès</h5>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-3 mt-3">
          <label class="text-sm text-muted flex items-center gap-2">
            <input
              type="checkbox"
              [checked]="workflow().auditAccess.enabled"
              (change)="patchAudit({ enabled: $any($event.target).checked })"
            />
            Activer Audit
          </label>
          <label class="text-sm text-muted flex items-center gap-2">
            <input type="checkbox" checked disabled />
            Lecture seule (fixe)
          </label>
          <label class="text-sm text-muted flex items-center gap-2">
            <input
              type="checkbox"
              [checked]="workflow().auditAccess.logs"
              (change)="patchAudit({ logs: $any($event.target).checked })"
            />
            Accès logs
          </label>
          <label class="text-sm text-muted flex items-center gap-2">
            <input
              type="checkbox"
              [checked]="workflow().auditAccess.history"
              (change)="patchAudit({ history: $any($event.target).checked })"
            />
            Accès historique
          </label>
        </div>
      </div>

      <div class="flex justify-end">
        <button
          type="button"
          (click)="save.emit()"
          [disabled]="saving()"
          class="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white disabled:cursor-not-allowed disabled:bg-blue-600/55 disabled:text-white"
        >
          {{ saving() ? 'Enregistrement...' : 'Enregistrer workflow' }}
        </button>
      </div>

      @if (mode(); as m) {
        @if (selectedStep(); as sel) {
          <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
            <button type="button" class="absolute inset-0 bg-input/80" (click)="closeModal()"></button>
            <div class="relative card-navy max-w-xl w-full p-6 border border-default">
              @if (m === 'config') {
                <div class="space-y-4">
                  <h3 class="text-primary font-semibold text-lg">Modifier: {{ sel.role }}</h3>
                  <label class="text-muted text-sm"
                    >Rôle
                    <input
                      readonly
                      [value]="sel.role"
                      class="mt-1 w-full bg-input border border-default rounded-lg p-3 text-sm text-primary"
                    />
                  </label>
                  <label class="text-muted text-sm"
                    >SLA spécifique
                    <input
                      type="number"
                      min="0"
                      class="mt-1 w-full bg-input border border-default rounded-lg p-3 text-sm text-primary"
                      [value]="sel.slaHours"
                      (input)="updateStepSla(sel.id, +$any($event.target).value || 0)"
                    />
                  </label>
                  <div class="flex flex-wrap gap-3">
                    @for (a of workflowActions; track a) {
                      <label class="text-sm text-muted flex items-center gap-2">
                        <input
                          type="checkbox"
                          [checked]="sel.actions.includes(a)"
                          (change)="toggleAction(sel.id, a, $any($event.target).checked)"
                        />
                        {{ a }}
                      </label>
                    }
                  </div>
                  <label class="text-muted text-sm"
                    >Type notification
                    <select
                      class="mt-1 w-full bg-input border border-default rounded-lg p-3 text-sm text-primary"
                      [value]="sel.notificationType"
                      (change)="updateNotifyType(sel.id, $any($event.target).value)"
                    >
                      <option value="email">Email</option>
                      <option value="in-app">InApp</option>
                    </select>
                  </label>
                </div>
              } @else {
                <div class="space-y-3">
                  <h3 class="text-primary font-semibold text-lg">Détails: {{ sel.role }}</h3>
                  <p class="text-muted text-sm">
                    {{ roleDetails[sel.role].description }}
                  </p>
                  <p class="text-muted text-sm">Hiérarchie: Pilote → Coach → Manager → RP → RH</p>
                </div>
              }
            </div>
          </div>
        }
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkflowAdminComponent {
  readonly workflow = input.required<AdminWorkflowConfig>();
  readonly workflowChange = output<AdminWorkflowConfig>();
  readonly saving = input(false);
  readonly save = output<void>();

  readonly globalSla = signal(48);
  readonly globalNotifications = signal(true);
  readonly selectedStepId = signal<string | null>(null);
  readonly mode = signal<'config' | 'details' | null>(null);

  readonly workflowActions = WORKFLOW_ACTIONS;
  readonly roleDetails = roleDetails;

  constructor() {
    effect(() => {
      const steps = this.workflow().steps;
      const avg = steps.length
        ? Math.round(steps.reduce((a, s) => a + s.slaHours, 0) / steps.length)
        : 48;
      this.globalSla.set(avg);
      this.globalNotifications.set(steps.some((s) => s.notificationEnabled));
    });
  }

  private emitWf(next: AdminWorkflowConfig): void {
    this.workflowChange.emit(next);
  }

  readonly selectedStep = computed(() => {
    const id = this.selectedStepId();
    if (!id) return null;
    return this.workflow().steps.find((s) => s.id === id) ?? null;
  });

  moveStep(index: number, direction: -1 | 1): void {
    const steps = [...this.workflow().steps];
    const curr = steps[index];
    if (!curr || curr.role === 'RH') return;
    const target = index + direction;
    if (target < 0 || target >= steps.length || steps[target].role === 'RH') return;
    [steps[index], steps[target]] = [steps[target], steps[index]];
    this.emitWf({ ...this.workflow(), steps });
  }

  patchAudit(partial: Partial<AdminWorkflowConfig['auditAccess']>): void {
    this.emitWf({
      ...this.workflow(),
      auditAccess: { ...this.workflow().auditAccess, ...partial },
    });
  }

  openConfig(id: string): void {
    this.selectedStepId.set(id);
    this.mode.set('config');
  }

  openDetails(id: string): void {
    this.selectedStepId.set(id);
    this.mode.set('details');
  }

  closeModal(): void {
    this.mode.set(null);
    this.selectedStepId.set(null);
  }

  updateStepSla(id: string, sla: number): void {
    this.emitWf({
      ...this.workflow(),
      steps: this.workflow().steps.map((s) => (s.id === id ? { ...s, slaHours: sla } : s)),
    });
  }

  toggleAction(id: string, action: WorkflowAction, checked: boolean): void {
    this.emitWf({
      ...this.workflow(),
      steps: this.workflow().steps.map((s) => {
        if (s.id !== id) return s;
        const next = checked
          ? [...new Set([...s.actions, action])]
          : s.actions.filter((x) => x !== action);
        return { ...s, actions: next };
      }),
    });
  }

  updateNotifyType(id: string, t: string): void {
    const notificationType = t as 'email' | 'in-app';
    this.emitWf({
      ...this.workflow(),
      steps: this.workflow().steps.map((s) => (s.id === id ? { ...s, notificationType } : s)),
    });
  }
}
