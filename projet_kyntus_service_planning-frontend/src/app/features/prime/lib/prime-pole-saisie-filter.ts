import type { PrimeFicheTemplateSchema } from '../models/prime-fiche-template.schema';

/** Contrats considérés comme « partie pôle » (RACC / SAV), aligné sur le parseur grille. */
export function isPoleContract(contract: string): boolean {
  const c = (contract || '').trim().toUpperCase();
  return c === 'RACC' || c === 'SAV';
}

/** Contrat SAV : la colonne « répartition RDV » n’est pas exigée à la saisie. */
export function isSavContract(contract: string): boolean {
  return (contract || '').trim().toUpperCase() === 'SAV';
}

/** Filtre le payload de saisie template pour ne garder que les lignes RACC/SAV. */
export function filterTemplatePayloadToPoleContracts(
  schema: PrimeFicheTemplateSchema,
  payload: { lignes?: Record<string, unknown> } & Record<string, unknown>,
): Record<string, unknown> {
  const allowed = new Set(
    schema.lines.filter((ln) => isPoleContract(ln.contract)).map((ln) => ln.stableId),
  );
  const lignes = payload.lignes ?? {};
  const filtered: Record<string, unknown> = {};
  for (const id of allowed) {
    if (lignes[id] !== undefined) filtered[id] = lignes[id] as unknown;
  }
  return {
    ...payload,
    lignes: filtered,
    poleContractsOnly: true,
  };
}
