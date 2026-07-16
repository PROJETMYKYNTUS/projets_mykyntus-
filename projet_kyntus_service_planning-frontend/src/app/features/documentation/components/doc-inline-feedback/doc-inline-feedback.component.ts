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
      border-radius: var(--radius-md, 0.5rem);
      margin: 10px 0;
    }
    .tone-success { background: var(--success-bg); color: var(--success-text); border: 1px solid var(--success-border); }
    .tone-info { background: var(--info-bg); color: var(--info-text); border: 1px solid var(--info-border); }
    .tone-warning { background: var(--warning-bg); color: var(--warning-text); border: 1px solid var(--warning-border); }
    .tone-error { background: var(--danger-bg); color: var(--danger-text); border: 1px solid var(--danger-border); }
  `],
})
export class DocInlineFeedbackComponent {
  @Input() message: string | null = null;
  @Input() tone: DocInlineFeedbackTone = 'info';
}
