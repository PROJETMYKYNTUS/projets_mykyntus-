import {
  archiveStoredTemplate,
  dedupeStoredTemplatesByDisplayName,
  isActiveStoredTemplate,
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

  it('ignore les templates archivés pour l’unicité des noms actifs', () => {
    const list = [
      tpl('a', 'Fiche PRIME', '2026-01-01T00:00:00Z'),
      { ...tpl('b', 'Fiche PRIME', '2026-02-01T00:00:00Z'), archivedAt: '2026-03-01T00:00:00Z' },
    ];
    expect(isTemplateDisplayNameTaken('Fiche PRIME', list)).toBe(true);
    expect(isTemplateDisplayNameTaken('Fiche PRIME', list, 'a')).toBe(false);
    const onlyArchived = [{ ...tpl('b', 'Fiche PRIME', '2026-02-01T00:00:00Z'), archivedAt: '2026-03-01T00:00:00Z' }];
    expect(isTemplateDisplayNameTaken('Fiche PRIME', onlyArchived)).toBe(false);
  });

  it('archive un template sans le retirer du stockage', () => {
    const list = [tpl('a', 'A', '2026-01-01T00:00:00Z'), tpl('b', 'B', '2026-01-02T00:00:00Z')];
    const archived = archiveStoredTemplate(list, 'a', '2026-06-01T00:00:00Z');
    expect(archived).toHaveLength(2);
    expect(isActiveStoredTemplate(archived[0]!)).toBe(false);
    expect(archived.filter(isActiveStoredTemplate)).toHaveLength(1);
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
