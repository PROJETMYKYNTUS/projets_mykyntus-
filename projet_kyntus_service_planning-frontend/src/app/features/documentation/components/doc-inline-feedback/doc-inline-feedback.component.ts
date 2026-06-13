import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

export type DocInlineFeedbackTone = 'success' | 'error' | 'info';

@Component({
  standalone: true,
  selector: 'app-doc-inline-feedback',
  imports: [CommonModule],
  template: `
    @if (message?.trim()) {
      <p
        class="doc-inline-feedback"
        [class.doc-inline-feedback--success]="tone === 'success'"
        [class.doc-inline-feedback--error]="tone === 'error'"
        [class.doc-inline-feedback--info]="tone === 'info'"
        role="status"
        aria-live="polite"
      >
        {{ message }}
      </p>
    }
  `,
  styles: [
    `
      .doc-inline-feedback {
        margin-top: 0.5rem;
        font-size: 0.75rem;
        line-height: 1.4;
        border-radius: 0.5rem;
        border: 1px solid transparent;
        padding: 0.45rem 0.65rem;
      }

      .doc-inline-feedback--success {
        border-color: color-mix(in srgb, #10b981 35%, transparent);
        background: color-mix(in srgb, #064e3b 40%, transparent);
        color: #d1fae5;
      }

      .doc-inline-feedback--error {
        border-color: color-mix(in srgb, #ef4444 40%, transparent);
        background: color-mix(in srgb, #450a0a 35%, transparent);
        color: #fecaca;
      }

      .doc-inline-feedback--info {
        border-color: color-mix(in srgb, #f59e0b 35%, transparent);
        background: color-mix(in srgb, #451a03 35%, transparent);
        color: #fde68a;
      }
    `,
  ],
})
export class DocInlineFeedbackComponent {
  @Input() message: string | null = null;
  @Input() tone: DocInlineFeedbackTone = 'info';
}
