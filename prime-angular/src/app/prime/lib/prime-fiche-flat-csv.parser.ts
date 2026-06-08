import { isTotalGeneralRowLabel } from './prime-cell-schema-merge';

export interface FlatCsvParseResult {
  rows: string[][];
  errors: string[];
  primeAmount: number | null;
  challengeAmount: number | null;
  totalAmount: number | null;
  serviceSaisieJson: string;
}

function parseNumLoose(raw: string | undefined): number | null {
  const t = (raw ?? '').replace(/\u00a0/g, ' ').trim();
  if (!t) return null;
  const n = Number(t.replace(/\s/g, '').replace(',', '.'));
  return Number.isFinite(n) ? n : null;
}

function parseCsvLine(line: string): string[] {
  const out: string[] = [];
  let cur = '';
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i]!;
    if (ch === '"') {
      if (inQuotes && line[i + 1] === '"') {
        cur += '"';
        i++;
      } else {
        inQuotes = !inQuotes;
      }
      continue;
    }
    if (ch === ',' && !inQuotes) {
      out.push(cur);
      cur = '';
      continue;
    }
    cur += ch;
  }
  out.push(cur);
  return out.map((c) => c.trim());
}

function detectDelimiter(headerLine: string): string {
  const commas = (headerLine.match(/,/g) ?? []).length;
  const semis = (headerLine.match(/;/g) ?? []).length;
  return semis > commas ? ';' : ',';
}

function splitCsv(text: string, delimiter: string): string[][] {
  const lines = text.replace(/^\uFEFF/, '').split(/\r?\n/).filter((l) => l.trim().length > 0);
  return lines.map((l) => {
    if (delimiter === ';') return l.split(';').map((c) => c.trim());
    return parseCsvLine(l);
  });
}

function extractTotalsFromRows(rows: string[][]): {
  primeAmount: number | null;
  challengeAmount: number | null;
  totalAmount: number | null;
} {
  const totalRow = rows.find((r) => isTotalGeneralRowLabel(r[0] ?? ''));
  if (!totalRow) {
    return { primeAmount: null, challengeAmount: null, totalAmount: null };
  }
  const nums = totalRow.map(parseNumLoose).filter((n): n is number => n !== null);
  if (nums.length >= 3) {
    const primeAmount = nums[nums.length - 3] ?? null;
    const challengeAmount = nums[nums.length - 2] ?? null;
    const totalAmount = nums[nums.length - 1] ?? null;
    return { primeAmount, challengeAmount, totalAmount };
  }
  if (nums.length === 1) {
    return { primeAmount: nums[0], challengeAmount: 0, totalAmount: nums[0] };
  }
  return { primeAmount: null, challengeAmount: null, totalAmount: null };
}

export function parseFlatPrimeFicheCsv(text: string, fileName: string): FlatCsvParseResult {
  const trimmed = text.trim();
  if (!trimmed) {
    return {
      rows: [],
      errors: ['Fichier CSV vide.'],
      primeAmount: null,
      challengeAmount: null,
      totalAmount: null,
      serviceSaisieJson: '{}',
    };
  }

  const firstLine = trimmed.split(/\r?\n/)[0] ?? '';
  const delimiter = detectDelimiter(firstLine);
  const rows = splitCsv(trimmed, delimiter);
  if (rows.length < 2) {
    return {
      rows: [],
      errors: ['Le CSV doit contenir au moins une ligne d’en-tête et une ligne de données.'],
      primeAmount: null,
      challengeAmount: null,
      totalAmount: null,
      serviceSaisieJson: '{}',
    };
  }

  const totals = extractTotalsFromRows(rows);
  const serviceSaisieJson = JSON.stringify({
    mode: 'import-flat-csv',
    fileName,
    importedAt: new Date().toISOString(),
    headers: rows[0],
    dataRows: rows.slice(1),
  });

  return {
    rows,
    errors: [],
    ...totals,
    serviceSaisieJson,
  };
}
