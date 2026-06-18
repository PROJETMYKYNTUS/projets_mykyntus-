export interface AdminSystemKpi {
  totalGeneratedPrimes: number;
  validationsInProgress: number;
  errorCount: number;
  avgProcessingTimeSec: number;
}

export interface AdminDashboardCharts {
  volumeByMonth: { month: string; value: number }[];
  validationRate: { month: string; value: number }[];
  byDepartment: { name: string; value: number }[];
}

export interface AdminRbacRow {
  role: string;
  read: boolean;
  edit: boolean;
  validate: boolean;
  configure: boolean;
}

export interface AdminSystemAlert {
  id: string;
  type: 'Erreur systeme' | 'Incoherence' | 'Workflow bloque';
  message: string;
  severity: 'Haute' | 'Moyenne' | 'Faible';
  date: string;
}

export interface AdminAuditLog {
  id: string;
  user: string;
  action: string;
  date: string;
}

export interface AdminAnomaly {
  id: string;
  type: 'Erreur de calcul' | 'Donnee manquante';
  description: string;
  status: 'Ouverte' | 'Corrigee' | 'Ignoree';
}

export const WORKFLOW_ACTIONS = ['Validate', 'Reject', 'Approve', 'Archive'] as const;
export type WorkflowAction = (typeof WORKFLOW_ACTIONS)[number];

export const WORKFLOW_STEP_ROLES = ['Coach', 'Superviseur', 'Manager', 'RP'] as const;
export type WorkflowStepRole = (typeof WORKFLOW_STEP_ROLES)[number];

export interface AdminWorkflowStepConfig {
  id: string;
  role: WorkflowStepRole | 'RH';
  slaHours: number;
  actions: WorkflowAction[];
  notificationType: 'email' | 'in-app';
  notificationEnabled: boolean;
}

export interface AdminWorkflowConfig {
  steps: AdminWorkflowStepConfig[];
  auditAccess: {
    enabled: boolean;
    readOnly: boolean;
    logs: boolean;
    history: boolean;
    export: boolean;
  };
}
