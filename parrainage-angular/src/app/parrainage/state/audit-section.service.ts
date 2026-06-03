import { Injectable, signal } from '@angular/core';

export type AuditSectionId = 'dashboard' | 'journal' | 'access-history' | 'anomalies' | 'reporting';

@Injectable({ providedIn: 'root' })
export class AuditSectionService {
  readonly section = signal<AuditSectionId>('journal');

  setSection(id: AuditSectionId): void {
    this.section.set(id);
  }
}
