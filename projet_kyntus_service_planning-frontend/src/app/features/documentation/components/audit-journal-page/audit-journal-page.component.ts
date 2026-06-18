import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-audit-journal-page',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="audit-journal"><h2>Audit Journal</h2></div>`,
})
export class AuditJournalPageComponent {}
