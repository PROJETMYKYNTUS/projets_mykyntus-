import {
  EmployeeImportColumnMapping,
  EmployeeImportFieldConfig,
  EmployeeImportMappingItem,
} from '../../services/employee-import.service';
import { isIgnorableHeader } from './employee-import-column.utils';

export type MappingValidationSeverity = 'error' | 'warning';

export interface MappingValidationIssue {
  severity: MappingValidationSeverity;
  message: string;
}

const FIELD_HEADER_HINTS: Record<string, string[]> = {
  email: ['email', 'mail', 'courriel', 'e-mail'],
  firstName: ['prenom', 'prénom', 'firstname', 'first name'],
  lastName: ['nom de famille', 'nomdefamille', 'nom famille', 'lastname', 'last name', 'famille', 'nom'],
  role: ['role', 'rôle', 'fonction'],
  pole: ['pole', 'pôle', 'etage', 'étage'],
  cellule: ['cellule'],
  service: ['service', 'equipe', 'équipe', 'sous-service'],
  password: ['password', 'mot de passe', 'mdp', 'pwd'],
  hireDate: ['embauche', 'hire'],
  isActive: ['actif', 'active', 'statut'],
  level: ['niveau', 'level', 'debutant', 'intermediaire', 'expert'],
};

function normalizeHeader(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '');
}

function hintMatchesHeader(normalizedHeader: string, hint: string): boolean {
  const normalizedHint = normalizeHeader(hint);
  if (!normalizedHint) return false;
  if (normalizedHeader === normalizedHint) return true;

  // « nom » seul ne doit pas matcher « prénom » (prenom contient nom).
  if (normalizedHint === 'nom') return false;

  return normalizedHeader.includes(normalizedHint);
}

function headerMatchesField(header: string, fieldKey: string): boolean {
  const normalized = normalizeHeader(header);
  const hints = FIELD_HEADER_HINTS[fieldKey] ?? [fieldKey];
  return hints.some((hint) => hintMatchesHeader(normalized, hint));
}

/** Ordre de priorité pour éviter les ambiguïtés (ex. prénom vs nom). */
const FIELD_MATCH_PRIORITY = [
  'email',
  'firstName',
  'lastName',
  'role',
  'pole',
  'cellule',
  'service',
  'password',
  'hireDate',
  'isActive',
  'level',
] as const;

function bestFieldForHeader(
  header: string,
  activeFields: EmployeeImportFieldConfig[],
): string | null {
  const enabledKeys = new Set(activeFields.map((f) => f.fieldKey));
  for (const fieldKey of FIELD_MATCH_PRIORITY) {
    if (!enabledKeys.has(fieldKey)) continue;
    if (headerMatchesField(header, fieldKey)) return fieldKey;
  }
  return null;
}

function fieldLabel(activeFields: EmployeeImportFieldConfig[], fieldKey: string): string {
  return activeFields.find((f) => f.fieldKey === fieldKey)?.label ?? fieldKey;
}

export function validateEmployeeImportMappings(
  mappings: EmployeeImportMappingItem[],
  headers: string[],
  suggestedMappings: EmployeeImportColumnMapping[],
  activeFields: EmployeeImportFieldConfig[],
): MappingValidationIssue[] {
  const issues: MappingValidationIssue[] = [];
  const enabled = activeFields.filter((f) => f.isEnabled);

  for (const field of enabled.filter((f) => f.isRequiredOnCreate)) {
    if (!mappings.some((m) => m.fieldKey === field.fieldKey)) {
      issues.push({
        severity: 'error',
        message: `Champ obligatoire non mappé : ${field.label}.`,
      });
    }
  }

  const fieldUsage = new Map<string, string[]>();
  for (const mapping of mappings) {
    if (!mapping.fieldKey) continue;
    const header = headers[mapping.columnIndex] ?? `Colonne ${mapping.columnIndex + 1}`;
    const used = fieldUsage.get(mapping.fieldKey) ?? [];
    used.push(header);
    fieldUsage.set(mapping.fieldKey, used);
  }

  for (const [fieldKey, columns] of fieldUsage) {
    if (columns.length > 1) {
      issues.push({
        severity: 'error',
        message: `Le champ « ${fieldLabel(enabled, fieldKey)} » est mappé sur plusieurs colonnes (${columns.join(', ')}).`,
      });
    }
  }

  mappings.forEach((mapping) => {
    if (!mapping.fieldKey) return;

    const header = headers[mapping.columnIndex] ?? '';
    if (isIgnorableHeader(header)) return;

    const suggested = suggestedMappings.find((s) => s.columnIndex === mapping.columnIndex);
    const mappedLabel = fieldLabel(enabled, mapping.fieldKey);

    if (
      suggested?.suggestedFieldKey &&
      mapping.fieldKey !== suggested.suggestedFieldKey &&
      suggested.confidence === 'high'
    ) {
      const suggestedLabel = fieldLabel(enabled, suggested.suggestedFieldKey);
      issues.push({
        severity: 'warning',
        message: `Colonne « ${header} » : « ${mappedLabel} » est choisi alors que « ${suggestedLabel} » semble plus logique.`,
      });
    }

    const best = bestFieldForHeader(header, enabled);
    if (best && best !== mapping.fieldKey && !headerMatchesField(header, mapping.fieldKey)) {
      issues.push({
        severity: 'warning',
        message: `Colonne « ${header} » : le titre correspond plutôt à « ${fieldLabel(enabled, best)} » qu'à « ${mappedLabel} ».`,
      });
    }

    if (
      mapping.fieldKey === 'firstName' &&
      headerMatchesField(header, 'lastName') &&
      !headerMatchesField(header, 'firstName')
    ) {
      issues.push({
        severity: 'warning',
        message: `Colonne « ${header} » ressemble à un nom de famille mais est mappée « Prénom ».`,
      });
    }

    if (
      mapping.fieldKey === 'lastName' &&
      headerMatchesField(header, 'firstName') &&
      !headerMatchesField(header, 'lastName')
    ) {
      issues.push({
        severity: 'warning',
        message: `Colonne « ${header} » ressemble à un prénom mais est mappée « Nom ».`,
      });
    }
  });

  const seen = new Set<string>();
  return issues.filter((issue) => {
    const key = `${issue.severity}:${issue.message}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

export function hasMappingErrors(issues: MappingValidationIssue[]): boolean {
  return issues.some((i) => i.severity === 'error');
}
