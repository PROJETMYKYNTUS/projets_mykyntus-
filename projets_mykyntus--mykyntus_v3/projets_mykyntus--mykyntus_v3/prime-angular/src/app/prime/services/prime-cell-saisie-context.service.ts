import { Injectable, signal } from '@angular/core';



/** Paramètres pour la vue « saisie partie cellule » (navigation sans query string). */

@Injectable({ providedIn: 'root' })

export class PrimeCellSaisieContextService {

  readonly employeeId = signal<string | null>(null);

  readonly period = signal<string | null>(null);

  readonly templateId = signal<string | null>(null);

  readonly poleId = signal<string | null>(null);

  readonly celluleName = signal<string | null>(null);



  setContext(

    employeeId: string,

    period: string,

    opts?: { templateId?: string | null; poleId?: string | null; celluleName?: string | null },

  ): void {

    this.employeeId.set(employeeId);

    this.period.set(period);

    this.templateId.set(opts?.templateId?.trim() ? opts.templateId.trim() : null);

    this.poleId.set(opts?.poleId?.trim() ? opts.poleId.trim() : null);

    this.celluleName.set(opts?.celluleName?.trim() ? opts.celluleName.trim() : null);

  }



  clear(): void {

    this.employeeId.set(null);

    this.period.set(null);

    this.templateId.set(null);

    this.poleId.set(null);

    this.celluleName.set(null);

  }

}

