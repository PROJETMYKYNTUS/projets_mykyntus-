export type AuditValidationStepRole = 'Manager' | 'RP' | 'RH';

export interface AuditValidationStep {
  role: AuditValidationStepRole;
  status: 'OK' | 'REJECTED';
  date: string;
}

export interface AuditOperation {
  id: string;
  employeeName: string;
  projectName: string;
  steps: AuditValidationStep[];
  validatedBy: string;
  date: string;
  status: 'Validé' | 'Rejeté' | 'En cours';
}

export interface AuditTrailLog {
  id: string;
  user: string;
  action: string;
  date: string;
  detail: string;
}

export type AuditAnomalyType = 'Incohérence' | 'Erreur de calcul' | 'Validation manquante';

export interface AuditAnomaly {
  id: string;
  type: AuditAnomalyType;
  description: string;
  validationId?: string;
  status: 'Ouverte' | 'Corrigée';
}
