import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-generated-document-preview-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="open" class="preview-modal">
      <h2>{{ subtitle || 'Preview' }}</h2>
      <p *ngIf="generatedDocumentId">Doc ID: {{ generatedDocumentId }}</p>
      <p *ngIf="exportFileNameBase">Export: {{ exportFileNameBase }}</p>
      <button (click)="onClose()">Close</button>
    </div>
  `,
})
export class GeneratedDocumentPreviewModalComponent {
  @Input() open = false;
  @Input() subtitle: string | null = null;
  @Input() generatedDocumentId: string | null = null;
  @Input() exportFileNameBase: string | null = null;
  @Output() close = new EventEmitter<void>();

  onClose() {
    this.close.emit();
  }
}
