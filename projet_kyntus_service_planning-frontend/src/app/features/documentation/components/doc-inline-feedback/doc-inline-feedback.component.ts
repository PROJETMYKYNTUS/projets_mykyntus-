import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type DocInlineFeedbackTone = 'success' | 'info' | 'warning' | 'error';

@Component({
  selector: 'app-doc-inline-feedback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="message" class="inline-feedback" [class]="'tone-' + tone">
      <p>{{ message }}</p>
    </div>
  `,
  styles: [`
    .inline-feedback {
      padding: 10px;
      border-radius: 4px;
      margin: 10px 0;
    }
    .tone-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
    .tone-info { background: #d1ecf1; color: #0c5460; border: 1px solid #bee5eb; }
    .tone-warning { background: #fff3cd; color: #856404; border: 1px solid #ffeaa7; }
    .tone-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
  `],
})
export class DocInlineFeedbackComponent {
  @Input() message: string | null = null;
  @Input() tone: DocInlineFeedbackTone = 'info';
}
