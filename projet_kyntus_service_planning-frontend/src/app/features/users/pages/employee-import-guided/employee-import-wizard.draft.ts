import {
  AcceptedFuzzyOrgMatch,
  EmployeeImportAnalyzeResponse,
  EmployeeImportMappingItem,
  PendingOrgCreation,
} from '../../services/employee-import.service';

export type EmployeeImportWizardStep =
  | 'config'
  | 'file'
  | 'mapping'
  | 'preview'
  | 'org'
  | 'confirm'
  | 'report'
  | 'history';

const STORAGE_KEY = 'employeeImportWizard.v1';

export interface EmployeeImportWizardDraft {
  version: 1;
  savedAt: string;
  currentStep: EmployeeImportWizardStep;
  analyzeResult: EmployeeImportAnalyzeResponse;
  mappings: EmployeeImportMappingItem[];
  furthestStepIndex: number;
  approvedOrgCreations: PendingOrgCreation[];
  acceptedFuzzyMatches: AcceptedFuzzyOrgMatch[];
}

export function loadEmployeeImportWizardDraft(): EmployeeImportWizardDraft | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const draft = JSON.parse(raw) as EmployeeImportWizardDraft;
    if (draft.version !== 1 || !draft.analyzeResult?.importSessionId) {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
    if (typeof draft.furthestStepIndex !== 'number') {
      draft.furthestStepIndex = 1;
    }
    draft.approvedOrgCreations ??= [];
    draft.acceptedFuzzyMatches ??= [];
    return draft;
  } catch {
    sessionStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

export function saveEmployeeImportWizardDraft(draft: EmployeeImportWizardDraft): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft));
}

export function clearEmployeeImportWizardDraft(): void {
  sessionStorage.removeItem(STORAGE_KEY);
}
