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
