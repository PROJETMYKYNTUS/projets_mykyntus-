/**
 * Libellés d’affichage pour les demandes de documents (sans toucher aux données API).
 */

/** Type de document lisible, sans code modèle / suffixe technique. */
export function cleanDocumentRequestTypeLabel(raw: string | null | undefined): string {
  let s = (raw ?? '').trim().replace(/\s+/g, ' ');
  if (!s) return 'Document';

  const pipeIdx = s.indexOf('|');
  if (pipeIdx !== -1) {
    s = s.slice(0, pipeIdx).trim();
  }

  s = s
    .replace(/\s*\([^)]*mod[eè]le[^)]*\)/gi, '')
    .replace(/\s*\([^)]*template[^)]*\)/gi, '')
    .replace(/\s*\([^)]*code[^)]*\)/gi, '')
    .replace(/\s*[-:–—]\s*mod[eè]le.*$/gi, '')
    .replace(/\s*[-:–—]\s*template.*$/gi, '')
    .replace(/\s*\(mod[eè]le.*?\)/gi, '')
    .trim();

  s = s.replace(/\s*[–—]\s*[A-Z0-9][A-Z0-9_\-.]{2,}\s*$/i, '').trim();
  /* Code modèle en suffixe avec tiret ASCII (ex. « Attestation - ATTESTATION_SALAIRE_1 »). */
  s = s.replace(/\s+-\s+[A-Z0-9][A-Z0-9_\-.]{2,}\s*$/i, '').trim();

  return s || 'Document';
}

function normalizePersonLabel(s: string): string {
  return s.trim().replace(/\s+/g, ' ').toLowerCase();
}

/**
 * Champ `employeeName` côté API : souvent « pilote → bénéficiaire » ou « pilote vers bénéficiaire ».
 * Pour la colonne Pilote, on affiche une seule étiquette lisible : pas « A → A », et si le côté gauche
 * répète le nom (ex. code + nom → nom), on garde le nom unique.
 */
export function pilotDisplayNameFromEmployeeName(
  raw: string | null | undefined,
  emptyLabel = 'Collaborateur',
): string {
  const t = (raw ?? '').trim();
  if (!t) return emptyLabel;

  const splitRe = /\s*(?:->|→|➔|vers)\s*/i;
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
 * Même champ `employeeName` : côté API « demandeur → bénéficiaire » (flèche ou « vers »).
 * Pour l’historique / colonnes « collaborateur concerné », on n’affiche que le bénéficiaire (partie droite).
 */
export function beneficiaryDisplayNameFromEmployeeName(
  raw: string | null | undefined,
  emptyLabel = 'Collaborateur',
): string {
  const t = (raw ?? '').trim();
  if (!t) return emptyLabel;

  const splitRe = /\s*(?:->|→|➔|vers)\s*/i;
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
