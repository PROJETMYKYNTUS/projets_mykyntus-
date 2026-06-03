import {
  flattenDynamicLigneForPayload,
  ligneDynamicFromTemplateLine,
  type PrimeFicheTemplateSchema,
} from '../models/prime-fiche-template.schema';

/** Construit le même objet `lignes` que la saisie, à partir des valeurs déjà présentes dans le schéma (import Excel). */
export function buildTemplatePayloadFromSchemaDefaults(schema: PrimeFicheTemplateSchema): Record<string, unknown> {
  const lignes: Record<string, unknown> = {};
  for (const ln of schema.lines) {
    const row = ligneDynamicFromTemplateLine(ln);
    lignes[ln.stableId] = flattenDynamicLigneForPayload(ln.stableId, row);
  }
  return {
    mode: 'template',
    templateFormatVersion: schema.templateFormatVersion,
    fileName: schema.fileName,
    contractsOrder: schema.contractsOrder,
    lignes,
  };
}
