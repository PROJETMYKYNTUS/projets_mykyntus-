export type SeverityLevel = 'INFO' | 'WARNING' | 'CRITICAL';

export type SortKey =
  | 'datetime'
  | 'employee'
  | 'action'
  | 'item'
  | 'status'
  | 'departement'
  | 'pole'
  | 'cellule'
  | 'roleMetier'
  | 'severity';

export interface JournalRow {
  id: string;
  datetime: string;
  employee: string;
  action: string;
  item: string;
  status: string;
  departement: string;
  pole: string;
  cellule: string;
  roleMetier: string;
  ip: string;
  device: string;
  severity: SeverityLevel;
  actionCode: string;
  beforeState: Record<string, unknown>;
  afterState: Record<string, unknown>;
  metadata: Record<string, unknown>;
}

export type AccessEventType = 'LOGIN_SUCCESS' | 'LOGIN_FAILURE' | 'LOGOUT' | 'SUSPICIOUS';

export interface AccessLogRow {
  id: string;
  user: string;
  datetime: string;
  ip: string;
  location: string;
  success: boolean;
  eventType: AccessEventType;
  label: string;
  detail?: string;
  securityFlag?: 'NONE' | 'WARNING';
}

export type AnomalyPriority = 'P1' | 'P2' | 'P3';

export interface AnomalyRow {
  id: string;
  title: string;
  description: string;
  priority: AnomalyPriority;
  detectedAt: string;
  category: string;
  relatedUserLabel?: string;
  searchHints?: string[];
  severityUi: 'CRITICAL' | 'WARNING';
}

export interface ReportingKpis {
  actionsPerDay: number;
  criticalPercent: number;
  topUser: string;
  topUserActions: number;
  totalActions: number;
  activeUsers: number;
  anomaliesCount: number;
}

export const EMPTY_REPORTING_KPIS: ReportingKpis = {
  actionsPerDay: 0,
  criticalPercent: 0,
  topUser: '—',
  topUserActions: 0,
  totalActions: 0,
  activeUsers: 0,
  anomaliesCount: 0,
};
