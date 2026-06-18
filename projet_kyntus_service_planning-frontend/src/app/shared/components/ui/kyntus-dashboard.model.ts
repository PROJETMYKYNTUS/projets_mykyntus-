export type KyntusDashboardAlertSeverity = 'info' | 'warn' | 'error';

export interface KyntusDashboardAlert {
  severity: KyntusDashboardAlertSeverity;
  message: string;
  title?: string;
  route?: string;
  queryParams?: Record<string, string>;
  actionLabel?: string;
  action?: () => void;
}
