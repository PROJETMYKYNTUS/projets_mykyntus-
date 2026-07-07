export const HR_MARITAL_STATUS_OPTIONS = [
  { value: 'CELIBATAIRE', label: 'Célibataire' },
  { value: 'MARIE', label: 'Marié(e)' },
  { value: 'DIVORCE', label: 'Divorcé(e)' },
  { value: 'VEUF', label: 'Veuf / Veuve' },
] as const;

export const HR_NATIONALITY_OPTIONS = [
  { value: 'MAROCAIN', label: 'Marocain' },
  { value: 'MAROCAINE', label: 'Marocaine' },
  { value: 'AUTRE', label: 'Autre' },
] as const;

export function defaultNationalityCode(sexe?: string): 'MAROCAIN' | 'MAROCAINE' {
  return sexe === 'M' ? 'MAROCAIN' : 'MAROCAINE';
}

export function nationalityLabelForCode(code: string): string {
  return HR_NATIONALITY_OPTIONS.find((o) => o.value === code)?.label ?? '';
}

export function syncNationalityCodeFromLabel(label: string, sexe?: string): { code: string; autre: string } {
  const trimmed = label.trim();
  const known = HR_NATIONALITY_OPTIONS.find((o) => o.label.toLowerCase() === trimmed.toLowerCase());
  if (known && known.value !== 'AUTRE') {
    return { code: known.value, autre: '' };
  }
  if (trimmed) {
    return { code: 'AUTRE', autre: trimmed };
  }
  const code = defaultNationalityCode(sexe);
  return { code, autre: '' };
}

export const HR_MARITAL_STATUS_WITH_CHILDREN = new Set(['MARIE', 'DIVORCE', 'VEUF']);

export const HR_EDUCATION_LEVEL_OPTIONS = [
  { value: 'CAP_BEP', label: 'CAP / BEP' },
  { value: 'BAC', label: 'Baccalauréat' },
  { value: 'BAC_PLUS_2', label: 'Bac +2 (BTS, DUT, etc.)' },
  { value: 'BAC_PLUS_3', label: 'Bac +3 (Licence)' },
  { value: 'BAC_PLUS_5', label: 'Bac +5 (Master, ingénieur)' },
  { value: 'BAC_PLUS_8', label: 'Bac +8 (Doctorat)' },
  { value: 'AUTRE', label: 'Autre' },
] as const;

export function maritalStatusLabel(code: string): string {
  return HR_MARITAL_STATUS_OPTIONS.find((o) => o.value === code)?.label ?? code;
}
