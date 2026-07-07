import type { ContractType } from '../../../features/contract/services/contract.service';

export interface ContractFieldsModel {
  type: ContractType;
  startDate: string;
  endDate: string;
  probationDays: number | null;
  alertThresholdDays: number;
  notes: string;
  status: number;
}

export const CONTRACT_STATUS_OPTIONS = [
  { label: "En période d'essai", value: 0 },
  { label: 'Actif', value: 1 },
  { label: 'Expiré', value: 2 },
  { label: 'Résilié', value: 3 },
] as const;

export const DEFAULT_PROBATION_DAYS: Record<string, number> = {
  CDI: 90,
  CDD: 30,
  Stage: 15,
  ANAPEC: 0,
  Interim: 0,
};

export function defaultContractStatus(probationDays: number | null | undefined, type: ContractType): number {
  const days = probationDays ?? DEFAULT_PROBATION_DAYS[type] ?? 0;
  return days > 0 ? 0 : 1;
}

export function createEmptyContractFields(type: ContractType = 'CDI'): ContractFieldsModel {
  const probation = DEFAULT_PROBATION_DAYS[type] ?? 0;
  return {
    type,
    startDate: '',
    endDate: '',
    probationDays: null,
    alertThresholdDays: 15,
    notes: '',
    status: defaultContractStatus(null, type),
  };
}

export function statusLabelToValue(status: string): number {
  return CONTRACT_STATUS_OPTIONS.find((s) => s.label === status)?.value ?? 0;
}
