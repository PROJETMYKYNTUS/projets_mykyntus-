import { Component, EventEmitter, Input, Output } from '@angular/core';
import { BodyPortalDirective } from '../../../../shared/directives/body-portal.directive';

@Component({
  standalone: true,
  selector: 'app-admin-modal',
  imports: [BodyPortalDirective],
  templateUrl: './admin-modal.component.html',
})
export class AdminModalComponent {
  @Input({ required: true }) open = false;
  @Input({ required: true }) title = '';
  @Input() description?: string;
  @Output() close = new EventEmitter<void>();

  closeModal(): void {
    this.close.emit();
  }
}
