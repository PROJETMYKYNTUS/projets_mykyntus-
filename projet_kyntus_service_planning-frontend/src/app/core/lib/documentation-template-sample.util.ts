import type { TemplateVariableDto } from '../models/documentation.models';

/** Valeur fictive conforme aux règles de validation backend (CIN, RIB, dates FR, etc.). */
export function sampleValueForVariableName(name: string, _type?: string | null): string {
  const k = (name ?? '').trim().toLowerCase();
  if (!k) return 'Exemple';

  if (k.includes('cin')) return 'AB123456';
  if (k.includes('rib') || k.includes('compte_bancaire')) return '007840001234567890123456';
  if (k.includes('telephone') || k.includes('phone') || k === 'tel') return '+212612345678';
  if (k.includes('email') || k.includes('courriel')) return 'employe@kyntus.ma';
  if (k === 'nom' || k.endsWith('_nom')) return 'Alaoui';
  if (k === 'prenom' || k.includes('prénom') || k.endsWith('_prenom')) return 'Fatima';
  if (k.includes('date')) return '13/06/2024';
  if (k.includes('contrat')) return 'CDI';
  if (k.includes('adresse')) return '12 Rue Example, Casablanca';
  if (k.includes('ville')) return 'Casablanca';
  if (k.includes('salaire') || k.includes('montant')) return '8500';
  if (k.includes('info_document')) return 'Attestation de travail';
  if (k.includes('poste') || k.includes('fonction')) return 'Pilote commercial';

  return `Exemple ${name}`;
}

export function buildSampleValuesFromVariables(vars: TemplateVariableDto[]): Record<string, string> {
  const o: Record<string, string> = {};
  for (const v of vars) {
    if (!v.name?.trim()) continue;
    o[v.name.trim()] = sampleValueForVariableName(v.name, v.type);
  }
  return o;
}

export function buildSampleJsonFromVariables(vars: TemplateVariableDto[]): string {
  const values = buildSampleValuesFromVariables(vars);
  return Object.keys(values).length === 0 ? '{}' : `${JSON.stringify(values, null, 2)}`;
}
