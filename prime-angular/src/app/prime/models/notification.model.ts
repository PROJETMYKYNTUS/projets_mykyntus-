export type PrimeNotificationType =
  | 'primeValidated'
  | 'primeRejected'
  | 'newPrimeRule'
  | 'teamPerformanceUpdated';

export interface PrimeNotification {
  id: number;
  type: PrimeNotificationType;
  createdAt: Date;
  read: boolean;
}
