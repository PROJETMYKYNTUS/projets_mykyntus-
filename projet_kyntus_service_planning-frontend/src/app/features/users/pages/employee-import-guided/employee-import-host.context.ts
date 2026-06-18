import { InjectionToken } from '@angular/core';

/** Branchez le contexte hôte (rôle utilisateur, invalidation cache après import). */
export interface EmployeeImportHostContext {
  getRole(): string | null;
  onImportCompleted?(): void;
}

export const EMPLOYEE_IMPORT_HOST = new InjectionToken<EmployeeImportHostContext>(
  'EMPLOYEE_IMPORT_HOST',
);
