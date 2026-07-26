/** Verrous métier — alignés sur EmployeeImportFieldRegistry (backend). */

const IDENTITY_LOCKED = new Set(['email', 'firstname', 'lastname', 'role']);
const ORG_ACTIVE_LOCKED = new Set([
  'operationaldepartment',
  'pole',
  'cellule',
  'service',
]);

function normKey(fieldKey: string): string {
  return (fieldKey ?? '').trim().toLowerCase();
}

/** email / firstName / lastName / role : Actif + Obligatoire figés. */
export function isIdentityFieldLocked(fieldKey: string): boolean {
  return IDENTITY_LOCKED.has(normKey(fieldKey));
}

/** orga : Actif figé ; Obligatoire reste piloté par le rôle métier. */
export function isOrgActiveFieldLocked(fieldKey: string): boolean {
  return ORG_ACTIVE_LOCKED.has(normKey(fieldKey));
}

export function isEnabledCheckboxLocked(fieldKey: string): boolean {
  return isIdentityFieldLocked(fieldKey) || isOrgActiveFieldLocked(fieldKey);
}

export function isRequiredCheckboxLocked(fieldKey: string): boolean {
  return isIdentityFieldLocked(fieldKey);
}

export function lockHint(fieldKey: string): string {
  if (isIdentityFieldLocked(fieldKey)) {
    return 'Champ critique identité — Actif et Obligatoire verrouillés.';
  }
  if (isOrgActiveFieldLocked(fieldKey)) {
    return 'Champ organisation — doit rester Actif ; l’obligation suit le rôle.';
  }
  return '';
}

/** Force les valeurs avant envoi API. */
export function applyFieldLockToPayload(field: {
  fieldKey: string;
  isEnabled: boolean;
  isRequiredOnCreate: boolean;
}): { isEnabled: boolean; isRequiredOnCreate: boolean } {
  if (isIdentityFieldLocked(field.fieldKey)) {
    return { isEnabled: true, isRequiredOnCreate: true };
  }
  if (isOrgActiveFieldLocked(field.fieldKey)) {
    return { isEnabled: true, isRequiredOnCreate: field.isRequiredOnCreate };
  }
  return {
    isEnabled: field.isEnabled,
    isRequiredOnCreate: field.isRequiredOnCreate,
  };
}
