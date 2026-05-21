export type AuditValidationStepRole = 'Manager' | 'RP' | 'RH';

export interface AuditValidationStep {
  role: AuditValidationStepRole;
  status: 'OK' | 'REJECTED';
  date: string; // ISO
}

export interface AuditOperation {
  id: string;
  employeeName: string;
  projectName: string;
  steps: AuditValidationStep[];
  validatedBy: string; // dernier valideur
  date: string; // date de l'operation
  status: 'Validé' | 'Rejeté' | 'En cours';
}

export interface AuditTrailLog {
  id: string;
  user: string;
  action: string;
  date: string; // ISO
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

export const mockAuditKpis = {
  totalPrimes: 43,
  validations: 35,
  anomalies: 3,
  conformityRate: 94,
};

export const mockAuditCharts = {
  flowByStep: [
    { step: 'Réf. tech.', value: 14 },
    { step: 'Superviseur', value: 11 },
    { step: 'CdP', value: 9 },
    { step: 'RH', value: 18 },
  ],
  validationVsRejection: [
    { name: 'Validé (RH)', value: 18 },
    { name: 'Rejeté', value: 2 },
  ],
  activityByRole: [
    { role: 'Référent technique', value: 14 },
    { role: 'Superviseur', value: 11 },
    { role: 'Chef de projet', value: 9 },
    { role: 'RH', value: 18 },
  ],
};

export const mockAuditOperations: AuditOperation[] = [
  {
    id: 'op1',
    employeeName: 'Fatima Zahra Ouazzani',
    projectName: 'Lot CRM & Billing — T2 2026',
    steps: [
      { role: 'Manager', status: 'OK', date: '2026-05-10T09:40:00.000Z' },
      { role: 'RP', status: 'OK', date: '2026-05-11T10:05:00.000Z' },
      { role: 'RH', status: 'OK', date: '2026-05-12T11:20:00.000Z' },
    ],
    validatedBy: 'RH',
    date: '2026-05-12',
    status: 'Validé',
  },
  {
    id: 'op2',
    employeeName: 'Amine Fassi',
    projectName: 'Lot DevOps — T2 2026',
    steps: [
      { role: 'Manager', status: 'OK', date: '2026-05-08T08:15:00.000Z' },
      { role: 'RP', status: 'REJECTED', date: '2026-05-09T09:00:00.000Z' },
    ],
    validatedBy: 'RP',
    date: '2026-05-09',
    status: 'Rejeté',
  },
  {
    id: 'op3',
    employeeName: 'Hind Alaoui',
    projectName: 'Lot CRM & Billing — T2 2026',
    steps: [
      { role: 'Manager', status: 'OK', date: '2026-05-07T07:35:00.000Z' },
      { role: 'RP', status: 'OK', date: '2026-05-08T08:25:00.000Z' },
    ],
    validatedBy: 'RP',
    date: '2026-05-08',
    status: 'En cours',
  },
  {
    id: 'op4',
    employeeName: 'Mehdi Tazi',
    projectName: 'Lot CRM & Billing — T2 2026',
    steps: [
      { role: 'Manager', status: 'OK', date: '2026-04-27T12:10:00.000Z' },
      { role: 'RP', status: 'OK', date: '2026-04-28T13:05:00.000Z' },
      { role: 'RH', status: 'REJECTED', date: '2026-04-28T14:55:00.000Z' },
    ],
    validatedBy: 'RH',
    date: '2026-04-28',
    status: 'Rejeté',
  },
  {
    id: 'op5',
    employeeName: 'Salma Bennani',
    projectName: 'Lot CRM & Billing — T2 2026',
    steps: [{ role: 'Manager', status: 'OK', date: '2026-05-18T10:05:00.000Z' }],
    validatedBy: 'Manager',
    date: '2026-05-18',
    status: 'En cours',
  },
];

export const mockAuditTrailLogs: AuditTrailLog[] = [
  {
    id: 'log-a1',
    user: 'siham.lahlou@kyntus.ma',
    action: 'Audit : consultation et export',
    date: '2026-05-20T09:12:00.000Z',
    detail: 'Lecture des opérations — période mai 2026, cellule CRM & Billing.',
  },
  {
    id: 'log-a2',
    user: 'kenza.alami@kyntus.ma',
    action: 'Workflow : validation référent technique',
    date: '2026-05-19T09:40:00.000Z',
    detail: 'Validation niveau 1 — Mehdi Tazi, montant 3 800 MAD.',
  },
  {
    id: 'log-a3',
    user: 'nadia.benjelloun@kyntus.ma',
    action: 'Workflow : validation superviseur',
    date: '2026-05-19T11:40:00.000Z',
    detail: 'Validation niveau 2 — Hind Alaoui.',
  },
  {
    id: 'log-a4',
    user: 'ines.bouazza@kyntus.ma',
    action: 'Workflow : validation RH finale',
    date: '2026-05-17T09:00:00.000Z',
    detail: 'Clôture workflow — Fatima Zahra Ouazzani.',
  },
];

export const mockAuditAnomalies: AuditAnomaly[] = [
  {
    id: 'anom-1',
    type: 'Incohérence',
    description: 'Écart de 120 MAD entre total calculé et montant saisi — Hind Alaoui (mai 2026).',
    validationId: 'op3',
    status: 'Ouverte',
  },
  {
    id: 'anom-2',
    type: 'Erreur de calcul',
    description: 'Score KPI hors bornes sur la période 2026-04 — Mehdi Tazi.',
    validationId: 'op4',
    status: 'Ouverte',
  },
  {
    id: 'anom-3',
    type: 'Validation manquante',
    description: 'Validation RH non enregistrée — Salma Bennani.',
    validationId: 'op5',
    status: 'Ouverte',
  },
  {
    id: 'anom-4',
    type: 'Incohérence',
    description: 'Rejet DevOps sans motif détaillé — Amine Fassi.',
    validationId: 'op2',
    status: 'Corrigée',
  },
];
