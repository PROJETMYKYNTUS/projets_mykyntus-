import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { Check, Circle, Lock } from 'lucide';

import { LucideIconComponent } from '@/shared/lucide-icon.component';

import type { CampaignStepStatusDto } from '../../features/prime/services/prime-cell-prime-api.service';



@Component({

  selector: 'app-prime-campaign-stepper',

  standalone: true,

  imports: [LucideIconComponent],

  template: `

    <ol class="flex flex-wrap items-stretch gap-1 sm:gap-2" role="list">

      @for (step of steps(); track step.key; let last = $last) {

        <li class="flex min-w-0 flex-1 items-center gap-1 sm:gap-2">

          <button

            type="button"

            (click)="onStepClick(step)"

            [disabled]="isStepDisabled(step)"

            [class]="stepButtonClass(step)"

            [title]="step.reason ?? step.label"

          >

            <span [class]="stepIconClass(step)">

              @if (step.state === 'done') {

                <app-lucide-icon [icon]="icons.check" className="w-3.5 h-3.5" />

              } @else if (step.state === 'blocked' && !step.actionPath) {

                <app-lucide-icon [icon]="icons.lock" className="w-3.5 h-3.5" />

              } @else {

                <app-lucide-icon [icon]="icons.circle" className="w-3.5 h-3.5" />

              }

            </span>

            <span class="min-w-0 truncate text-left text-[11px] font-semibold leading-tight sm:text-xs">

              {{ step.label }}

            </span>

          </button>

          @if (!last) {

            <span class="hidden h-px min-w-[0.5rem] flex-1 bg-default sm:block" aria-hidden="true"></span>

          }

        </li>

      }

    </ol>

  `,

  changeDetection: ChangeDetectionStrategy.OnPush,

})

export class PrimeCampaignStepperComponent {

  readonly steps = input.required<readonly CampaignStepStatusDto[]>();

  readonly stepClick = output<CampaignStepStatusDto>();



  readonly icons = { check: Check, circle: Circle, lock: Lock };



  isStepDisabled(step: CampaignStepStatusDto): boolean {

    if (step.state === 'blocked' && !(step.actionPath ?? '').trim()) return true;

    return false;

  }



  onStepClick(step: CampaignStepStatusDto): void {

    if (this.isStepDisabled(step)) return;

    this.stepClick.emit(step);

  }



  stepButtonClass(step: CampaignStepStatusDto): string {

    const base =

      'flex w-full min-w-0 items-center gap-1.5 rounded-lg border px-2 py-1.5 transition-colors disabled:cursor-not-allowed';

    if (step.state === 'done') {

      return `${base} border-[color:var(--success-border)] bg-[color:var(--success-bg)] text-[color:var(--success-text)] hover:brightness-105`;

    }

    if (step.state === 'blocked' && !(step.actionPath ?? '').trim()) {

      return `${base} border-default bg-input text-primary opacity-80`;

    }

    return `${base} border-[color:var(--info-border)] bg-[color:var(--info-bg)] text-primary hover:brightness-105`;

  }



  stepIconClass(step: CampaignStepStatusDto): string {

    const base = 'inline-flex shrink-0 items-center justify-center rounded-full';

    if (step.state === 'done') return `${base} text-[color:var(--success-text)]`;

    if (step.state === 'blocked' && !(step.actionPath ?? '').trim()) return `${base} text-primary opacity-70`;

    return `${base} text-[color:var(--info-text)]`;

  }

}


