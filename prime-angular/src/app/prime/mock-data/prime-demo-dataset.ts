import type { PrimeResult, PrimeRule, PrimeType } from '../models';
import type { EmployeePrimeServiceFicheValidationDto } from '../services/prime-fiche-result.service';
import type {
  CellPilotageSummaryDto,
  EmployeePrimeCellFicheListItemDto,
  ServicePrimeIndicatorDto,
  SupervisorPolePrimeDraftListItemDto,
} from '../services/prime-cell-prime-api.service';
import type { PrimeDashboardStats } from '../services/prime.service';
import { PRIME_DEMO_PERIOD } from './prime-demo-config';
import {
  DEMO_DEPARTMENTS,
  DEMO_EMPLOYEES,
  demoCelluleLabel,
  demoEmployeeById,
  demoPilotes,
  demoPoleLabel,
  demoServiceLabel,
} from './prime-demo-org';

export const DEMO_PRIME_TYPES: PrimeType[] = [
  {
    id: 'pt-performance',
    name: 'Prime performance trimestrielle',
    type: 'Performance',
    poleId: 'pole-apps',
    status: 'Active',
    description: 'Basée sur le taux de réalisation des engagements KPI service.',
  },
  {
    id: 'pt-challenge',
    name: 'Challenge delivery',
    type: 'Challenge',
    poleId: 'pole-cloud',
    status: 'Active',
    description: 'Bonus lié aux jalons de mise en production et SLA incidents.',
  },
];

export const DEMO_PRIME_RULES: PrimeRule[] = [
  {
    id: 'rule-kpi-85',
    primeTypeId: 'pt-performance',
    poleId: 'pole-apps',
    celluleId: 'cell-crm',
    serviceId: 'svc-crm-core',
    roleId: 'Pilote',
    conditionField: 'scoreKpi',
    conditionType: '>=',
    targetValue: 85,
    calculationMethod: 'Percentage',
    amount: 12,
    period: 'Monthly',
  },
  {
    id: 'rule-kpi-90',
    primeTypeId: 'pt-performance',
    serviceId: 'svc-billing',
    roleId: 'Pilote',
    conditionField: 'scoreKpi',
    conditionType: '>=',
    targetValue: 90,
    calculationMethod: 'Tiered',
    amount: 18,
    period: 'Monthly',
  },
];

const PILOTES = demoPilotes();

function ficheRow(
  idx: number,
  empId: string,
  validationStatus: string,
  primeAmount: number,
  totalAmount: number,
  fillingStatus = 'Complete',
): EmployeePrimeServiceFicheValidationDto {
  const emp = demoEmployeeById(empId)!;
  const sup = emp.parentId ? demoEmployeeById(emp.parentId)?.parentId ?? 'e-sup-nadia' : 'e-sup-nadia';
  const supervisor = demoEmployeeById(sup) ?? demoEmployeeById('e-sup-nadia')!;
  return {
    id: `fiche-demo-${idx}`,
    employeeId: empId,
    employeeDisplayName: `${emp.firstName} ${emp.lastName}`,
    employeeRole: emp.role,
    supervisorUserId: supervisor.id,
    serviceId: emp.serviceId,
    serviceName: demoServiceLabel(emp.serviceId),
    celluleId: emp.celluleId,
    celluleName: demoCelluleLabel(emp.celluleId),
    poleName: demoPoleLabel(emp.poleId),
    period: PRIME_DEMO_PERIOD,
    fillingStatus,
    validationStatus,
    commonPartStatus: 'Validated',
    isReadyForValidation: fillingStatus === 'Complete',
    lastApproverUserId:
      validationStatus === 'Pending' ? null : validationStatus === 'Référent technique Approved' ? 'e-rt-kenza' : 'e-sup-nadia',
    lastApprovedAt: validationStatus === 'Pending' ? null : '2026-05-18T10:30:00.000Z',
    primeAmount,
    challengeAmount: Math.round(primeAmount * 0.15),
    totalAmount,
    updatedAt: '2026-05-19T14:00:00.000Z',
  };
}

export const DEMO_VALIDATION_FICHES: EmployeePrimeServiceFicheValidationDto[] = [
  ficheRow(1, 'e-pil-salma', 'Pending', 4200, 92),
  ficheRow(2, 'e-pil-mehdi', 'Référent technique Approved', 3800, 88),
  ficheRow(3, 'e-pil-hind', 'Superviseur Approved', 5100, 94),
  ficheRow(4, 'e-pil-karim', 'Chef de projet Approved', 2900, 81),
  ficheRow(5, 'e-pil-fatima', 'RH Approved', 6100, 96),
  ficheRow(6, 'e-pil-amine', 'Rejected', 1200, 62),
];

export const DEMO_PRIME_RESULTS: PrimeResult[] = DEMO_VALIDATION_FICHES.map((f, i) => ({
  id: f.id,
  employeeId: f.employeeId,
  primeTypeId: i % 2 === 0 ? 'pt-performance' : 'pt-challenge',
  score: f.totalAmount ?? 0,
  amount: f.primeAmount ?? 0,
  status: f.validationStatus as PrimeResult['status'],
  period: f.period,
  approvedBy: f.lastApproverUserId ?? undefined,
  date: '2026-05-19',
}));

export const DEMO_DASHBOARD_STATS: PrimeDashboardStats = {
  totalPrimesThisMonth: 47_850,
  budgetConsumption: 72,
  topTeams: [
    { name: 'Service CRM Core', amount: 18_400 },
    { name: 'Service Facturation', amount: 12_200 },
    { name: 'Service DevOps Oujda', amount: 9_800 },
  ],
  topEmployees: [
    { name: 'Fatima Zahra Ouazzani', amount: 6_100 },
    { name: 'Hind Alaoui', amount: 5_100 },
    { name: 'Salma Bennani', amount: 4_200 },
  ],
  primeByDepartment: [
    { name: 'Pôle Delivery & Engineering', value: 32_500 },
    { name: 'Pôle Support & Qualité', value: 8_200 },
    { name: 'Cloud & DevOps', value: 7_150 },
  ],
  primeEvolution: [
    { month: '2026-01', amount: 38_200 },
    { month: '2026-02', amount: 41_500 },
    { month: '2026-03', amount: 44_100 },
    { month: '2026-04', amount: 46_300 },
    { month: '2026-05', amount: 47_850 },
  ],
};

export const DEMO_VALIDATION_SUMMARY = {
  statusCounts: [
    { status: 'Pending', count: 8 },
    { status: 'Référent technique Approved', count: 6 },
    { status: 'Superviseur Approved', count: 5 },
    { status: 'Chef de projet Approved', count: 4 },
    { status: 'RH Approved', count: 18 },
    { status: 'Rejected', count: 2 },
  ],
  terminalStatuses: ['RH Approved', 'Rejected'],
  total: 43,
  pending: 8,
  referentTechniqueApproved: 14,
  superviseurApproved: 11,
  chefDeProjetApproved: 9,
  rhApproved: 18,
  rejected: 2,
};

export const DEMO_WORKFLOW_META = {
  steps: [
    {
      id: 'wf-1',
      sortOrder: 1,
      approverRole: 'Référent technique',
      fromStatus: 'Pending',
      toStatus: 'Référent technique Approved',
      isActive: true,
      slaHours: 24,
      capturesAmountsOnApproval: true,
      terminalApproved: false,
    },
    {
      id: 'wf-2',
      sortOrder: 2,
      approverRole: 'Superviseur',
      fromStatus: 'Référent technique Approved',
      toStatus: 'Superviseur Approved',
      isActive: true,
      slaHours: 24,
      capturesAmountsOnApproval: false,
      terminalApproved: false,
    },
    {
      id: 'wf-3',
      sortOrder: 3,
      approverRole: 'Chef de projet',
      fromStatus: 'Superviseur Approved',
      toStatus: 'Chef de projet Approved',
      isActive: true,
      slaHours: 48,
      capturesAmountsOnApproval: false,
      terminalApproved: false,
    },
    {
      id: 'wf-4',
      sortOrder: 4,
      approverRole: 'RH',
      fromStatus: 'Chef de projet Approved',
      toStatus: 'RH Approved',
      isActive: true,
      slaHours: 72,
      capturesAmountsOnApproval: true,
      terminalApproved: true,
    },
  ],
  terminalStatuses: ['RH Approved', 'Rejected'],
  actionableFromStatuses: [
    'Pending',
    'Référent technique Approved',
    'Superviseur Approved',
    'Chef de projet Approved',
  ],
};

export const DEMO_PERIODS = ['2026-05', '2026-04', '2026-03', '2026-02'];

export const DEMO_DRAFT_ID = 'draft-demo-cell-crm-202605';

export const DEMO_ACTIVE_DRAFTS: SupervisorPolePrimeDraftListItemDto[] = [
  {
    id: DEMO_DRAFT_ID,
    supervisorUserId: 'e-sup-nadia',
    celluleId: 'cell-crm',
    period: PRIME_DEMO_PERIOD,
    templateId: 'tpl-fiche-commune-v2',
    templateDisplayName: 'Fiche PRIME commune — CRM & Billing',
    templateFormatVersion: 2,
    status: 'InProgress',
    totalEmployees: 6,
    completeEmployees: 4,
    inProgressEmployees: 1,
    notStartedEmployees: 1,
    isFullyComplete: false,
    updatedAt: '2026-05-20T09:15:00.000Z',
    hasGlobalPoolFile: true,
    poolDistributionUnlocked: false,
  },
  {
    id: 'draft-demo-devops-202605',
    supervisorUserId: 'e-sup-rachid',
    celluleId: 'cell-devops',
    period: PRIME_DEMO_PERIOD,
    templateId: 'tpl-fiche-commune-v2',
    templateDisplayName: 'Fiche PRIME commune — DevOps',
    templateFormatVersion: 2,
    status: 'Validated',
    totalEmployees: 1,
    completeEmployees: 1,
    inProgressEmployees: 0,
    notStartedEmployees: 0,
    isFullyComplete: true,
    updatedAt: '2026-05-17T16:40:00.000Z',
    hasGlobalPoolFile: true,
    poolDistributionUnlocked: true,
  },
];

export const DEMO_INDICATORS: ServicePrimeIndicatorDto[] = [
  {
    id: 'ind-1',
    serviceId: 'svc-crm-core',
    sortOrder: 1,
    label: 'Taux de résolution J+1 (%)',
    ponderationPrimePct: 35,
    ponderationChallengePct: 10,
    isActive: true,
    templateStableId: 'cell:auto:ind-1',
    createdAt: '2026-01-10T08:00:00.000Z',
    updatedAt: '2026-05-01T08:00:00.000Z',
  },
  {
    id: 'ind-2',
    serviceId: 'svc-crm-core',
    sortOrder: 2,
    label: 'Satisfaction client (NPS)',
    ponderationPrimePct: 25,
    ponderationChallengePct: 15,
    isActive: true,
    templateStableId: 'cell:auto:ind-2',
    createdAt: '2026-01-10T08:00:00.000Z',
    updatedAt: null,
  },
  {
    id: 'ind-3',
    serviceId: 'svc-crm-core',
    sortOrder: 3,
    label: 'Respect des délais de livraison',
    ponderationPrimePct: 40,
    ponderationChallengePct: 5,
    isActive: true,
    templateStableId: 'cell:auto:ind-3',
    createdAt: '2026-02-01T08:00:00.000Z',
    updatedAt: null,
  },
];

export function buildDemoEmployeeFicheList(serviceId?: string): EmployeePrimeCellFicheListItemDto[] {
  const pilotes = PILOTES.filter((p) => !serviceId || p.serviceId === serviceId);
  return pilotes.map((p, i) => ({
    employeeId: p.id,
    firstName: p.firstName,
    lastName: p.lastName,
    email: p.email,
    serviceId: p.serviceId,
    celluleId: p.celluleId,
    ficheId: `fiche-pilote-${p.id}`,
    cellulePrimeDraftId: DEMO_DRAFT_ID,
    fillingStatus: i === pilotes.length - 1 ? 'NotStarted' : i === 0 ? 'InProgress' : 'Complete',
    validationStatus: DEMO_VALIDATION_FICHES.find((f) => f.employeeId === p.id)?.validationStatus ?? 'Pending',
    isReadyForValidation: i > 0 && i < pilotes.length - 1,
    serviceSaisieJson: '{}',
    updatedAt: '2026-05-19T12:00:00.000Z',
  }));
}

export const DEMO_CELLS_SUMMARY: CellPilotageSummaryDto[] = [
  {
    serviceId: 'svc-crm-core',
    serviceName: 'Service CRM Core',
    celluleId: 'cell-crm',
    celluleName: 'Cellule CRM & Billing',
    poleName: 'Applications Métier',
    totalEmployees: 2,
    notStarted: 0,
    inProgress: 1,
    complete: 1,
    readyForValidation: 1,
    commonPartStatus: 'Validated',
    serviceAggregateState: 'InProgress',
    linkedCellulePrimeDraftId: DEMO_DRAFT_ID,
    linkedTemplateDisplayName: 'Fiche PRIME commune — CRM & Billing',
    poolDistributionUnlocked: false,
  },
  {
    serviceId: 'svc-billing',
    serviceName: 'Service Facturation',
    celluleId: 'cell-crm',
    celluleName: 'Cellule CRM & Billing',
    poleName: 'Applications Métier',
    totalEmployees: 2,
    notStarted: 0,
    inProgress: 0,
    complete: 2,
    readyForValidation: 2,
    commonPartStatus: 'Validated',
    serviceAggregateState: 'Done',
    linkedCellulePrimeDraftId: DEMO_DRAFT_ID,
    linkedTemplateDisplayName: 'Fiche PRIME commune — CRM & Billing',
    poolDistributionUnlocked: false,
  },
];

export const DEMO_GLOBAL_POOL_STATE = {
  draftId: DEMO_DRAFT_ID,
  celluleId: 'cell-crm',
  period: PRIME_DEMO_PERIOD,
  hasFile: true,
  fileName: `Synthese_PRIME_${PRIME_DEMO_PERIOD}_CRM_Billing.xlsx`,
  uploadedAt: '2026-05-20T08:00:00.000Z',
  managerApprovedAt: null as string | null,
  rhApprovedAt: null as string | null,
  comptaAckAt: null as string | null,
  poolDistributionUnlocked: false,
};

export const DEMO_SUPERVISOR_SCOPE = [
  {
    id: 'pole-apps',
    name: 'Applications Métier',
    cellules: [
      {
        id: 'cell-crm',
        name: 'Cellule CRM & Billing',
        rootPoleId: 'pole-apps',
        services: [
          { id: 'svc-crm-core', name: 'Service CRM Core' },
          { id: 'svc-billing', name: 'Service Facturation' },
        ],
      },
      {
        id: 'cell-integration',
        name: 'Cellule Intégration SI',
        rootPoleId: 'pole-apps',
        services: [
          { id: 'svc-api', name: 'Service API & Middleware' },
          { id: 'svc-edi', name: 'Service EDI B2B' },
        ],
      },
    ],
  },
];

export const DEMO_ORG_OVERVIEW = {
  etages: [
    { id: 'etage-oujda-siege', name: 'Site Oujda — Siège Kyntus Maroc' },
    { id: 'etage-oujda-angad', name: 'Site Oujda — Plateau Angad' },
  ],
  services: [
    { id: 'svc-crm-core', name: 'Service CRM Core', etageId: 'etage-oujda-siege' },
    { id: 'svc-billing', name: 'Service Facturation', etageId: 'etage-oujda-siege' },
    { id: 'svc-devops', name: 'Service DevOps Oujda', etageId: 'etage-oujda-angad' },
    { id: 'svc-cc-oujda-n1', name: 'Service N1 Oujda — relation client', etageId: 'etage-oujda-siege' },
    { id: 'svc-cc-oujda-n2', name: 'Service N2 Oujda — réclamations', etageId: 'etage-oujda-angad' },
  ],
  sousServices: [
    { id: 'svc-crm-core', name: 'Équipe CRM Core', serviceId: 'svc-crm-core' },
    { id: 'svc-billing', name: 'Équipe Facturation', serviceId: 'svc-billing' },
  ],
  employees: DEMO_EMPLOYEES,
  departments: DEMO_DEPARTMENTS,
  managerEtage: [
    { id: 'a-mgr-1', userId: 'e-cdp-omar', etageId: 'dept-delivery' },
    { id: 'a-mgr-2', userId: 'e-mgr-laila', etageId: 'dept-support' },
  ],
  supervisorService: [
    { id: 'a-sup-1', userId: 'e-sup-nadia', celluleId: 'cell-crm', serviceId: 'cell-crm' },
    { id: 'a-sup-2', userId: 'e-sup-rachid', celluleId: 'cell-devops', serviceId: 'cell-devops' },
  ],
  coachSousService: [
    { id: 'a-rt-1', userId: 'e-rt-kenza', serviceId: 'svc-crm-core', sousServiceId: 'svc-crm-core' },
    { id: 'a-rt-2', userId: 'e-rt-youssef', serviceId: 'svc-billing', sousServiceId: 'svc-billing' },
    { id: 'a-rt-3', userId: 'e-rt-sanae', serviceId: 'svc-devops', sousServiceId: 'svc-devops' },
  ],
  coachPilot: [
    { id: 'l-1', coachUserId: 'e-rt-kenza', pilotUserId: 'e-pil-salma' },
    { id: 'l-2', coachUserId: 'e-rt-kenza', pilotUserId: 'e-pil-mehdi' },
    { id: 'l-3', coachUserId: 'e-rt-youssef', pilotUserId: 'e-pil-hind' },
    { id: 'l-4', coachUserId: 'e-rt-youssef', pilotUserId: 'e-pil-karim' },
    { id: 'l-5', coachUserId: 'e-rt-kenza', pilotUserId: 'e-pil-fatima' },
    { id: 'l-6', coachUserId: 'e-rt-sanae', pilotUserId: 'e-pil-amine' },
  ],
};

export const DEMO_ADMIN_ANOMALIES = [
  {
    id: 'anom-demo-1',
    type: 'ComputationMismatch',
    description: 'Écart de 120 MAD entre total calculé et montant saisi — fiche Hind Alaoui (mai 2026).',
    status: 'Open',
    detectedAt: '2026-05-19T11:22:00.000Z',
    severity: 'High',
  },
  {
    id: 'anom-demo-2',
    type: 'MissingValidation',
    description: 'Validation superviseur absente alors que la fiche est marquée prête — Mehdi Tazi.',
    status: 'Open',
    detectedAt: '2026-05-18T09:05:00.000Z',
    severity: 'Medium',
  },
  {
    id: 'anom-demo-3',
    type: 'WorkflowBlocked',
    description: 'SLA dépassé de 36 h sur le lot CRM & Billing — attente Chef de projet.',
    status: 'InReview',
    detectedAt: '2026-05-17T14:30:00.000Z',
    severity: 'Critical',
  },
  {
    id: 'anom-demo-4',
    type: 'ComputationMismatch',
    description: 'Pondération challenge > 100 % sur indicateur NPS — corrigé manuellement.',
    status: 'Resolved',
    detectedAt: '2026-05-10T08:00:00.000Z',
    severity: 'Low',
  },
];

export const DEMO_ADMIN_AUDIT_LOGS = [
  {
    id: 'log-1',
    userDisplayName: 'Kenza Alami',
    action: 'Validation référent technique',
    at: '2026-05-19T10:15:00.000Z',
    entityType: 'Fiche PRIME — Mehdi Tazi',
    detailJson: 'Approbation niveau 1, montant prime 3 800 MAD.',
  },
  {
    id: 'log-2',
    userDisplayName: 'Nadia Benjelloun',
    action: 'Validation superviseur',
    at: '2026-05-19T11:40:00.000Z',
    entityType: 'Fiche PRIME — Hind Alaoui',
    detailJson: 'Approbation niveau 2 après contrôle des KPI.',
  },
  {
    id: 'log-3',
    userDisplayName: 'Omar Chraibi',
    action: 'Validation chef de projet',
    at: '2026-05-18T16:20:00.000Z',
    entityType: 'Fiche PRIME — Karim Berrada',
    detailJson: 'Validation opérationnelle lot Facturation.',
  },
  {
    id: 'log-4',
    userDisplayName: 'Inès Bouazza',
    action: 'Validation RH finale',
    at: '2026-05-17T09:00:00.000Z',
    entityType: 'Fiche PRIME — Fatima Zahra Ouazzani',
    detailJson: 'Clôture workflow, export paie prévu.',
  },
  {
    id: 'log-5',
    userDisplayName: 'Siham Lahlou',
    action: 'Consultation audit',
    at: '2026-05-16T14:55:00.000Z',
    entityType: 'Module PRIME',
    detailJson: 'Export journal des opérations période 2026-05.',
  },
  {
    id: 'log-6',
    userDisplayName: 'Yassine Touimi',
    action: 'Modification workflow',
    at: '2026-05-15T08:30:00.000Z',
    entityType: 'Configuration',
    detailJson: 'SLA superviseur porté à 24 h.',
  },
];

export const DEMO_WORKFLOW_STEPS = DEMO_WORKFLOW_META.steps.map((s, i) => ({
  ...s,
  approverRole: s.approverRole,
  fromStatus: s.fromStatus,
  toStatus: s.toStatus,
}));

export const DEMO_WORKFLOW_GLOBAL = {
  id: 'global-1',
  notificationsEnabled: true,
  globalSlaHours: 48,
  allowBulkApprove: true,
  requireRejectReason: true,
  updatedAt: '2026-05-01T00:00:00.000Z',
};

export const DEMO_RBAC_CATALOG = {
  actions: ['Read', 'Edit', 'Validate', 'Configure'],
  scopes: ['Global', 'Pole', 'Cellule', 'Service', 'Self'],
  roles: [
    'Admin',
    'RH',
    'Manager',
    'Comptabilité',
    'Chef de projet',
    'Superviseur',
    'Référent technique',
    'Pilote',
    'Audit',
  ],
};

export const DEMO_RBAC_PERMISSIONS = [
  { id: 'rbac-1', role: 'Admin', action: 'Configure', scope: 'Global', isAllowed: true },
  { id: 'rbac-2', role: 'Superviseur', action: 'Validate', scope: 'Cellule', isAllowed: true },
  { id: 'rbac-3', role: 'Référent technique', action: 'Validate', scope: 'Service', isAllowed: true },
  { id: 'rbac-4', role: 'Pilote', action: 'Edit', scope: 'Self', isAllowed: true },
  { id: 'rbac-5', role: 'RH', action: 'Validate', scope: 'Global', isAllowed: true },
  { id: 'rbac-6', role: 'Audit', action: 'Read', scope: 'Global', isAllowed: true },
];

export const DEMO_RP_DASHBOARD = {
  projectProgress: 78,
  completedTasks: 124,
  averageTeamPerformance: 87,
  pendingValidations: 4,
  performanceEvolution: [
    { month: '2026-01', score: 82 },
    { month: '2026-02', score: 84 },
    { month: '2026-03', score: 86 },
    { month: '2026-04', score: 85 },
    { month: '2026-05', score: 87 },
  ],
  memberPerformance: [
    { name: 'Salma Bennani', score: 92, status: 'Excellent' },
    { name: 'Mehdi Tazi', score: 88, status: 'Excellent' },
    { name: 'Hind Alaoui', score: 81, status: 'Moyen' },
    { name: 'Karim Berrada', score: 76, status: 'Moyen' },
  ],
};

const RP_TASK_COUNTS = [22, 20, 21, 19];
export const DEMO_RP_TEAM = PILOTES.slice(0, 4).map((p, i) => ({
  employeeId: p.id,
  employeeName: `${p.firstName} ${p.lastName}`,
  projectId: 'proj-crm-2026',
  projectName: 'Lot CRM & Billing — T2 2026',
  completedTasks: RP_TASK_COUNTS[i] ?? 20,
  totalTasks: 24,
  objectivesReached: 7,
  totalObjectives: 8,
  monthlyPerformance: [
    { month: '2026-03', score: 84 },
    { month: '2026-04', score: 86 },
    { month: '2026-05', score: 88 },
  ],
}));

export const DEMO_RP_VALIDATIONS = [
  {
    id: 'rp-val-1',
    employeeId: 'e-pil-mehdi',
    employeeName: 'Mehdi Tazi',
    projectId: 'proj-crm-2026',
    projectName: 'Lot CRM & Billing — T2 2026',
    performanceScore: 88,
    superviseurValidated: true,
    status: 'Manager Approved',
    period: PRIME_DEMO_PERIOD,
  },
  {
    id: 'rp-val-2',
    employeeId: 'e-pil-hind',
    employeeName: 'Hind Alaoui',
    projectId: 'proj-crm-2026',
    projectName: 'Lot CRM & Billing — T2 2026',
    performanceScore: 94,
    superviseurValidated: true,
    status: 'Manager Approved',
    period: PRIME_DEMO_PERIOD,
  },
];

export function demoMyResults(employeeId: string): PrimeResult[] {
  return DEMO_PRIME_RESULTS.filter((r) => r.employeeId === employeeId);
}
