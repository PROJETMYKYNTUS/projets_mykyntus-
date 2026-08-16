import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AllowanceRejectDialogService } from './allowance-reject-dialog.service';
import { BodyPortalDirective } from '../../../../shared/directives/body-portal.directive';

@Component({
  selector: 'app-allowance-reject-dialog',
  standalone: true,
  imports: [FormsModule, BodyPortalDirective],
  template: `
    @if (dialog.visible()) {
      <div class="fixed inset-0 z-[200] flex items-center justify-center p-4 bg-black/50" appBodyPortal (click)="dialog.cancel()">
        <div class="ky-card w-full max-w-md p-5 space-y-4 shadow-xl" role="dialog" aria-modal="true" (click)="$event.stopPropagation()">
          <h2 class="text-lg font-semibold text-primary">{{ dialog.title() }}</h2>
          <p class="text-sm text-muted">Le motif est obligatoire pour rejeter une demande.</p>
          <textarea class="doc-field w-full" rows="3" [(ngModel)]="reason" placeholder="Saisir le motif…" autofocus></textarea>
          <div class="flex justify-end gap-2">
            <button type="button" class="prime-btn-secondary" (click)="dialog.cancel()">Annuler</button>
            <button type="button" class="btn-danger" [disabled]="!reason.trim()" (click)="dialog.confirm(reason)">Rejeter</button>
          </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowanceRejectDialogComponent {
  readonly dialog = inject(AllowanceRejectDialogService);
  reason = '';
}
