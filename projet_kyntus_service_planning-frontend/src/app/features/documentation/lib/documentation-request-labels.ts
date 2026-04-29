/**
 * LibellÃ©s dâ€™affichage pour les demandes de documents (sans toucher aux donnÃ©es API).
 */

/** Type de document lisible, sans code modÃ¨le / suffixe technique. */
export function cleanDocumentRequestTypeLabel(raw: string | null | undefined): string {
  let s = (raw ?? '').trim().replace(/\s+/g, ' ');
  if (!s) return 'Document';

  const pipeIdx = s.indexOf('|');
  if (pipeIdx !== -1) {
    s = s.slice(0, pipeIdx).trim();
  }

  s = s
    .replace(/\s*\([^)]*mod[eÃ¨]le[^)]*\)/gi, '')
    .replace(/\s*\([^)]*template[^)]*\)/gi, '')
    .replace(/\s*\([^)]*code[^)]*\)/gi, '')
    .replace(/\s*[-:â€“â€”]\s*mod[eÃ¨]le.*$/gi, '')
    .replace(/\s*[-:â€“â€”]\s*template.*$/gi, '')
    .replace(/\s*\(mod[eÃ¨]le.*?\)/gi, '')
    .trim();

  s = s.replace(/\s*[â€“â€”]\s*[A-Z0-9][A-Z0-9_\-.]{2,}\s*$/i, '').trim();
  /* Code modÃ¨le en suffixe avec tiret ASCII (ex. Â« Attestation - ATTESTATION_SALAIRE_1 Â»). */
  s = s.replace(/\s+-\s+[A-Z0-9][A-Z0-9_\-.]{2,}\s*$/i, '').trim();

  return s || 'Document';
}

function normalizePersonLabel(s: string): string {
  return s.trim().replace(/\s+/g, ' ').toLowerCase();
}

/**
 * Champ `employeeName` cÃ´tÃ© API : souvent Â« pilote â†’ bÃ©nÃ©ficiaire Â» ou Â« pilote vers bÃ©nÃ©ficiaire Â».
 * Pour la colonne Pilote, on affiche une seule Ã©tiquette lisible : pas Â« A â†’ A Â», et si le cÃ´tÃ© gauche
 * rÃ©pÃ¨te le nom (ex. code + nom â†’ nom), on garde le nom unique.
 */
export function pilotDisplayNameFromEmployeeName(
  raw: string | null | undefined,
  emptyLabel = 'Collaborateur',
): string {
  const t = (raw ?? '').trim();
  if (!t) return emptyLabel;

  const splitRe = /\s*(?:->|â†’|âž”|vers)\s*/i;
  const parts = t.split(splitRe).map((p) => p.trim()).filter(Boolean);
  if (parts.length < 2) return t;

  const left = parts[0];
  const right = parts[1];

  if (normalizePersonLabel(left) === normalizePersonLabel(right)) {
    return right;
  }

  if (right && left.toLowerCase().endsWith(right.toLowerCase())) {
    return right;
  }

  return left;
}

/**
 * MÃªme champ `employeeName` : cÃ´tÃ© API Â« demandeur â†’ bÃ©nÃ©ficiaire Â» (flÃ¨che ou Â« vers Â»).
 * Pour lâ€™historique / colonnes Â« collaborateur concernÃ© Â», on nâ€™affiche que le bÃ©nÃ©ficiaire (partie droite).
 */
export function beneficiaryDisplayNameFromEmployeeName(
  raw: string | null | undefined,
  emptyLabel = 'Collaborateur',
): string {
  const t = (raw ?? '').trim();
  if (!t) return emptyLabel;

  const splitRe = /\s*(?:->|â†’|âž”|vers)\s*/i;
  const parts = t.split(splitRe).map((p) => p.trim()).filter(Boolean);
  if (parts.length < 2) return t;

  const beneficiary = parts[parts.length - 1];
  if (!beneficiary) return t;

  const requester = parts[0];
  if (requester && normalizePersonLabel(requester) === normalizePersonLabel(beneficiary)) {
    return beneficiary;
  }

  return beneficiary;
}
