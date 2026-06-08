import type * as ExcelJSTypes from 'exceljs';
import * as XLSX from 'xlsx';
import {
  CHALLENGE_KEYS,
  COL_INDICATOR,
  COL_REPARTITION_V1,
  COL_REPARTITION_V2,
  PRIME_SUBCOLS,
} from './prime-fiche-grid.parser';
import {
  isDerivedCellStableId,
  isSommeLikeRowLabel,
  isTotalGeneralRowLabel,
} from './prime-cell-schema-merge';
import {
  PRIME_FICHE_TEMPLATE_FORMAT_V1,
  type PrimeFicheTemplateSchema,
} from '../models/prime-fiche-template.schema';

/** Largeurs de colonnes sur la plage utilisée (meilleure lisibilité du .xlsx exporté). */
export function applyPrimeFicheExportLayout(ws: XLSX.WorkSheet): void {
  const ref = ws['!ref'];
  if (!ref) return;
  const range = XLSX.utils.decode_range(ref);
  const ncols = range.e.c - range.s.c + 1;
  ws['!cols'] = Array.from({ length: Math.max(ncols, 1) }, () => ({ wch: 13 }));
}

const COLOR_HEADER = 'FFD9E1F2';
const COLOR_SOMME = 'FFD8E4BC';
const COLOR_CELL_INDICATOR = 'FFFFF2CC';
const COLOR_TOTAL_BG = 'FF305496';
const COLOR_TOTAL_FG = 'FFFFFFFF';
const COLOR_PLAFOND = 'FFFCE4D6';

const THIN_BORDER: Partial<ExcelJSTypes.Borders> = {
  top: { style: 'thin', color: { argb: 'FF8FA3B0' } },
  left: { style: 'thin', color: { argb: 'FF8FA3B0' } },
  right: { style: 'thin', color: { argb: 'FF8FA3B0' } },
  bottom: { style: 'thin', color: { argb: 'FF8FA3B0' } },
};

function setFill(cell: ExcelJSTypes.Cell, argb: string): void {
  cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb } };
}

function setBorders(cell: ExcelJSTypes.Cell): void {
  cell.border = { ...THIN_BORDER };
}

function normLabel(raw: unknown): string {
  return String(raw ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}

function parseCellValue(raw: string | undefined): string | number | null {
  if (raw === undefined || raw === null) return null;
  const t = String(raw).replace(/\u00a0/g, ' ').trim();
  if (t === '') return null;
  if (/%$/.test(t)) return t;
  const compact = t.replace(/\s/g, '').replace(',', '.');
  const n = Number(compact);
  if (Number.isFinite(n) && /^-?\d/.test(compact)) return n;
  return t;
}

function isHeaderRow(row: string[] | undefined): boolean {
  if (!row) return false;
  return row.some((c) => {
    const t = normLabel(c);
    return (
      t === 'indicateurs' ||
      t === 'indicateur' ||
      t.startsWith('prime (secteur') ||
      t.startsWith('challenge (secteur') ||
      t.startsWith('repartition') ||
      t.startsWith('plafond ')
    );
  });
}

function isPlafondHeaderCell(value: unknown): boolean {
  return normLabel(value).startsWith('plafond ');
}

interface ContractGroup {
  contract: string;
  rmin: number;
  rmax: number;
}

function buildContractMergeGroups(
  schema: PrimeFicheTemplateSchema,
  rowCount: number,
): ContractGroup[] {
  const lines = [...schema.lines]
    .filter((l) => typeof l.sourceRowIndex === 'number' && l.sourceRowIndex >= 0)
    .sort((a, b) => (a.sourceRowIndex ?? 0) - (b.sourceRowIndex ?? 0));
  const groups: ContractGroup[] = [];
  let cur: ContractGroup | null = null;
  for (const ln of lines) {
    const r = ln.sourceRowIndex!;
    if (r >= rowCount) continue;
    const c = (ln.contract ?? '').trim() || '—';
    if (!cur || cur.contract !== c) {
      if (cur) groups.push(cur);
      cur = { contract: c, rmin: r, rmax: r };
    } else {
      cur.rmax = r;
    }
  }
  if (cur) groups.push(cur);
  return groups.filter((g) => g.contract.trim() !== '');
}

interface HeaderBand {
  startCol: number;
  endCol: number;
}

/**
 * Calcule les bandes d'en-tête à partir du schéma — déterministe et indépendant des
 * cellules vides du fichier source :
 *   - Indicateurs : [0, colRep - 1]
 *   - Répartitions des RDV : [colRep, colRep] (1 colonne, pas de fusion)
 *   - Prime (Secteur) : [primeStartCol, primeStartCol + PRIME_SUBCOLS - 1]  (6 cols)
 *   - Challenge (Secteur) : [primeStartCol + PRIME_SUBCOLS, ... + CHALLENGE_KEYS.length - 1] (5 cols)
 *
 * Les autres cellules (Plafond Prime / Plafond Challenge appondues à droite, sub-headers
 * Résultat / KPI / Pondération / …) restent en mono-colonne.
 */
function bandsFromSchema(schema: PrimeFicheTemplateSchema): HeaderBand[] {
  const isV1 = schema.templateFormatVersion === PRIME_FICHE_TEMPLATE_FORMAT_V1;
  const colRep = isV1 ? COL_REPARTITION_V1 : COL_REPARTITION_V2;
  const firstWithGSC = schema.lines.find((l) => l.secteurs?.[0]?.gridStartCol !== undefined);
  const primeStartCol = firstWithGSC?.secteurs?.[0]?.gridStartCol ?? colRep + 1;
  const primeEndCol = primeStartCol + PRIME_SUBCOLS - 1;
  const challengeStartCol = primeStartCol + PRIME_SUBCOLS;
  const challengeEndCol = challengeStartCol + CHALLENGE_KEYS.length - 1;
  return [
    { startCol: 0, endCol: colRep - 1 },
    { startCol: colRep, endCol: colRep },
    { startCol: primeStartCol, endCol: primeEndCol },
    { startCol: challengeStartCol, endCol: challengeEndCol },
  ];
}

/**
 * Fusions horizontales d'en-tête — algorithme schéma-aware : pour chaque ligne du bloc
 * d'en-tête (5 premières lignes), on fusionne uniquement les bandes définies par le schéma.
 * Une fusion n'est appliquée que si :
 *   - la cellule à `band.startCol` est non vide (libellé de bande),
 *   - les cellules `band.startCol + 1 .. band.endCol` sont toutes vides (sinon ce sont
 *     des sub-headers individuels — Résultat, KPI Min, … — qui ne doivent pas être fusionnés).
 */
function buildHorizontalHeaderMerges(
  rows: string[][],
  schema: PrimeFicheTemplateSchema,
): Array<{ row: number; cmin: number; cmax: number }> {
  const out: Array<{ row: number; cmin: number; cmax: number }> = [];
  const bands = bandsFromSchema(schema);
  const limit = Math.min(5, rows.length);
  for (let r = 0; r < limit; r++) {
    const row = rows[r] ?? [];
    for (const b of bands) {
      if (b.endCol <= b.startCol) continue;
      const head = String(row[b.startCol] ?? '').trim();
      if (!head) continue;
      let allEmpty = true;
      for (let c = b.startCol + 1; c <= b.endCol; c++) {
        if (String(row[c] ?? '').trim() !== '') {
          allEmpty = false;
          break;
        }
      }
      if (allEmpty) out.push({ row: r, cmin: b.startCol, cmax: b.endCol });
    }
  }
  return out;
}

/**
 * Charge ExcelJS dynamiquement (lazy chunk) — évite de gonfler le bundle initial.
 * Le code n'est téléchargé qu'au premier export.
 */
async function loadExcelJS(): Promise<typeof import('exceljs')> {
  const mod = await import('exceljs');
  return ((mod as unknown) as { default?: typeof import('exceljs') }).default ?? mod;
}

/**
 * Construit un workbook ExcelJS stylé pour le livrable employé fusionné :
 * fusions contrat (col 0), fusions horizontales en-tête, fonds verts/Sommes,
 * jaune Cellule, bleu TOTAL, bordures fines.
 */
export async function buildStyledMergedFicheWorkbook(
  rows: string[][],
  schema: PrimeFicheTemplateSchema,
  sheetName: string,
): Promise<ExcelJSTypes.Workbook> {
  const ExcelJS = await loadExcelJS();
  const wb = new ExcelJS.Workbook();
  const ws = wb.addWorksheet(sheetName.slice(0, 31) || 'Fiche_PRIME');

  const rowCount = rows.length;
  const colCount = rows.reduce((m, r) => Math.max(m, r.length), 0);
  if (rowCount === 0 || colCount === 0) return wb;

  for (let r = 0; r < rowCount; r++) {
    for (let c = 0; c < colCount; c++) {
      const v = parseCellValue(rows[r]?.[c]);
      const cell = ws.getCell(r + 1, c + 1);
      cell.value = v;
      cell.alignment = { vertical: 'middle', horizontal: c <= COL_INDICATOR ? 'left' : 'center', wrapText: true };
      setBorders(cell);
    }
  }

  const widths = new Array<number>(colCount).fill(11);
  if (colCount > 0) widths[0] = 9;
  if (colCount > 1) widths[1] = 28;
  if (colCount > 2) widths[2] = 11;
  for (let c = 0; c < colCount; c++) {
    ws.getColumn(c + 1).width = widths[c]!;
  }

  for (const merge of buildHorizontalHeaderMerges(rows, schema)) {
    if (merge.cmax > merge.cmin) {
      try {
        ws.mergeCells(merge.row + 1, merge.cmin + 1, merge.row + 1, merge.cmax + 1);
      } catch {
        /* déjà fusionnée */
      }
    }
  }

  const groups = buildContractMergeGroups(schema, rowCount);
  for (const g of groups) {
    if (g.rmax > g.rmin) {
      try {
        ws.mergeCells(g.rmin + 1, 1, g.rmax + 1, 1);
      } catch {
        /* déjà fusionnée */
      }
    }
    const top = ws.getCell(g.rmin + 1, 1);
    top.value = g.contract;
    top.font = { bold: true };
    top.alignment = { vertical: 'middle', horizontal: 'center', wrapText: true };
    setBorders(top);
  }

  const derivedCellRowIdx = new Set<number>();
  for (const ln of schema.lines) {
    if (
      typeof ln.sourceRowIndex === 'number' &&
      ln.sourceRowIndex >= 0 &&
      ln.sourceRowIndex < rowCount &&
      isDerivedCellStableId(ln.stableId)
    ) {
      derivedCellRowIdx.add(ln.sourceRowIndex);
    }
  }

  for (let r = 0; r < rowCount; r++) {
    const row = rows[r] ?? [];
    const isHeader = isHeaderRow(row);
    const sommeKind = isSommeLikeRowLabel(row[COL_INDICATOR]);
    const isTotal = isTotalGeneralRowLabel(row[COL_INDICATOR]);
    const isDerivedCell = derivedCellRowIdx.has(r);

    if (isHeader) {
      for (let c = 0; c < colCount; c++) {
        const cell = ws.getCell(r + 1, c + 1);
        const v = String(rows[r]?.[c] ?? '').trim();
        if (v === '') continue;
        cell.font = { bold: true };
        setFill(cell, isPlafondHeaderCell(v) ? COLOR_PLAFOND : COLOR_HEADER);
      }
      ws.getRow(r + 1).height = 28;
      continue;
    }

    if (isTotal) {
      for (let c = 0; c < colCount; c++) {
        const cell = ws.getCell(r + 1, c + 1);
        cell.font = { bold: true, color: { argb: COLOR_TOTAL_FG } };
        setFill(cell, COLOR_TOTAL_BG);
      }
      continue;
    }

    if (sommeKind !== null) {
      for (let c = 0; c < colCount; c++) {
        const cell = ws.getCell(r + 1, c + 1);
        cell.font = { bold: true };
        setFill(cell, COLOR_SOMME);
      }
      continue;
    }

    if (isDerivedCell) {
      const indicatorCell = ws.getCell(r + 1, COL_INDICATOR + 1);
      indicatorCell.font = { bold: true };
      setFill(indicatorCell, COLOR_CELL_INDICATOR);
    }
  }

  return wb;
}

/** Exporte une grille brute (snapshot import / archive) sans schéma stylé. */
export function downloadRawGridXlsx(rows: string[][], sheetName: string, fileName: string): void {
  const safeSheet =
    (sheetName || 'Fiche_PRIME').replace(/[:\\/?*[\]]/g, '_').slice(0, 31) || 'Fiche_PRIME';
  const ws = XLSX.utils.aoa_to_sheet(rows);
  applyPrimeFicheExportLayout(ws);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, safeSheet);
  XLSX.writeFile(wb, fileName);
}

/** Déclenche le téléchargement d'un workbook ExcelJS via Blob URL (pas de dépendance externe). */
export async function downloadStyledFicheWorkbook(
  wb: ExcelJSTypes.Workbook,
  fileName: string,
): Promise<void> {
  const buf = await wb.xlsx.writeBuffer();
  const blob = new Blob([buf], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
