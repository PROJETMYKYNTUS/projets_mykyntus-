import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-generated-document-format-menu',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="format-menu">
      <h2>Format Menu</h2>
      <p *ngIf="generatedDocumentId">Doc ID: {{ generatedDocumentId }}</p>
      <p *ngIf="fileNameBase">File: {{ fileNameBase }}</p>
    </div>
  `,
})
export class GeneratedDocumentFormatMenuComponent {
  @Input() generatedDocumentId: string | null = null;
  @Input() fileNameBase: string | null = null;
  @Output() formatSelected = new EventEmitter<string>();
}
