import {
  dedupeStoredTemplatesByDisplayName,
  isTemplateDisplayNameTaken,
  normalizeTemplateDisplayName,
  type StoredPrimeTemplate,
} from './prime-template.model';

function tpl(id: string, displayName: string, savedAt: string): StoredPrimeTemplate {
  return {
    id,
    displayName,
    savedAt,
    fileName: 'test.xlsx',
    parsedAt: savedAt,
    sheets: [],
    contractHints: [],
    labelSample: [],
    formulas: [],
    previewRows: [],
    previewSheetName: 'Sheet1',
    validation: { ok: true, errors: [], warnings: [] },
  };
}

describe('prime-template.model — unicité des noms', () => {
  it('normalise casse et espaces', () => {
    expect(normalizeTemplateDisplayName('  Fiche   PRIME  ')).toBe('fiche prime');
  });

  it('détecte un nom déjà pris', () => {
    const list = [tpl('a', 'Fiche PRIME 2026', '2026-01-01T00:00:00Z')];
    expect(isTemplateDisplayNameTaken('fiche prime 2026', list)).toBe(true);
    expect(isTemplateDisplayNameTaken('Autre nom', list)).toBe(false);
  });

  it('déduplique en conservant le plus récent', () => {
    const list = [
      tpl('old', 'Fiche PRIME', '2026-01-01T00:00:00Z'),
      tpl('new', 'FICHE prime', '2026-02-01T00:00:00Z'),
      tpl('other', 'Autre', '2026-01-15T00:00:00Z'),
    ];
    const deduped = dedupeStoredTemplatesByDisplayName(list);
    expect(deduped).toHaveLength(2);
    expect(deduped.find((t) => normalizeTemplateDisplayName(t.displayName) === 'fiche prime')?.id).toBe('new');
  });
});
