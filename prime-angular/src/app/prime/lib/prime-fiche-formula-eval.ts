import { HyperFormula } from 'hyperformula';
import * as XLSX from 'xlsx';
import type { PrimeTemplateCalcSheets, StoredPrimeTemplate } from '../models/prime-template.model';

function cellValueToString(v: unknown): string {
  if (v === null || v === undefined) return '';
  if (typeof v === 'number' && Number.isFinite(v)) return String(v);
  if (typeof v === 'boolean') return v ? 'TRUE' : 'FALSE';
  return String(v);
}

function cloneCalcSheets(cs: PrimeTemplateCalcSheets): Record<string, (string | number | null)[][]> {
  const out: Record<string, (string | number | null)[][]> = {};
  for (const [name, rows] of Object.entries(cs)) {
    out[name] = rows.map((r) => [...r]);
  }
  return out;
}

function applyFormulaList(
  hf: HyperFormula,
  formulas: StoredPrimeTemplate['formulas'],
  errors: string[],
): void {
  for (const fc of formulas) {
    const sid = hf.getSheetId(fc.sheet);
    if (sid === undefined) {
      errors.push(`Formule ignorée (feuille absente du classeur exporté) : ${fc.sheet}`);
      continue;
    }
    const addrPart = fc.address.includes('!') ? fc.address.split('!')[1]! : fc.address;
    let f = fc.formula.trim();
    if (f.startsWith('=')) f = f.slice(1);
    let cellAddr = addrPart;
    if (cellAddr.includes('$')) cellAddr = cellAddr.replaceAll('$', '');
    let pos: { r: number; c: number };
    try {
      pos = XLSX.utils.decode_cell(cellAddr);
    } catch {
      errors.push(`Adresse invalide : ${fc.sheet}!${fc.address}`);
      continue;
    }
    try {
      hf.setCellContents({ sheet: sid, col: pos.c, row: pos.r }, [[`=${f}`]]);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      errors.push(`${fc.sheet}!${cellAddr}: ${msg}`);
    }
  }
}

/**
 * Recalcul complet à partir des feuilles exportées à l’import (`calcSheets`) : toutes les feuilles
 * et plages utiles (exemplaire PRIME, références REF!, etc.).
 */
function computeFromCalcSheets(template: StoredPrimeTemplate, cs: PrimeTemplateCalcSheets): {
  rows: string[][];
  errors: string[];
} {
  const errors: string[] = [];
  const sheets = cloneCalcSheets(cs);
  if (!Object.keys(sheets).length) {
    return { rows: [], errors: ['Aucune feuille de calcul exportée'] };
  }

  try {
    const hf = HyperFormula.buildFromSheets(sheets, { licenseKey: 'gpl-v3' });
    applyFormulaList(hf, template.formulas, errors);

    const mainName = template.previewSheetName || Object.keys(sheets)[0]!;
    const sid = hf.getSheetId(mainName);
    if (sid === undefined) {
      errors.push(`Feuille principale introuvable : ${mainName}`);
      return { rows: [], errors };
    }

    const dims = hf.getSheetDimensions(sid);
    const out: string[][] = [];
    for (let r = 0; r < dims.height; r++) {
      const line: string[] = [];
      for (let c = 0; c < dims.width; c++) {
        const v = hf.getCellValue({ sheet: sid, row: r, col: c });
        line.push(cellValueToString(v));
      }
      out.push(line);
    }
    return { rows: out, errors };
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e);
    errors.push(msg);
    return { rows: [], errors };
  }
}

/** Ancien mode : seulement l’aperçu tronqué (templates sans `calcSheets`). */
function computeFromPreviewRowsOnly(template: StoredPrimeTemplate): {
  rows: string[][];
  errors: string[];
} {
  const errors: string[] = [];
  const pr = template.previewRows;
  if (!pr.length) {
    return { rows: [], errors: ['Aucune ligne d’aperçu'] };
  }

  const maxC = Math.max(...pr.map((r) => r.cells.length), 1);
  const data: (string | number | null)[][] = pr.map((r) => {
    const row: (string | number | null)[] = [];
    for (let c = 0; c < maxC; c++) {
      const raw = r.cells[c] ?? '';
      if (raw === '') {
        row.push(null);
        continue;
      }
      const trimmed = raw.trim();
      if (trimmed.startsWith('=')) {
        row.push(null);
        continue;
      }
      const n = Number(raw);
      if (trimmed !== '' && Number.isFinite(n) && /^-?\d+(\.\d+)?$/.test(trimmed)) {
        row.push(n);
      } else {
        row.push(raw);
      }
    }
    return row;
  });

  const sheetName = template.previewSheetName || 'Sheet1';

  try {
    const hf = HyperFormula.buildFromSheets(
      { [sheetName]: data },
      { licenseKey: 'gpl-v3' },
    );
    const sid = hf.getSheetId(sheetName);
    if (sid === undefined) {
      errors.push(`Feuille introuvable : ${sheetName}`);
      return { rows: pr.map((r) => [...r.cells]), errors };
    }

    for (let r = 0; r < pr.length; r++) {
      const cells = pr[r]?.cells ?? [];
      for (let c = 0; c < cells.length; c++) {
        const trimmed = (cells[c] ?? '').trim();
        if (!trimmed.startsWith('=')) continue;
        try {
          hf.setCellContents({ sheet: sid, row: r, col: c }, [[trimmed]]);
        } catch (e) {
          const msg = e instanceof Error ? e.message : String(e);
          errors.push(`Cellule import ${XLSX.utils.encode_cell({ r, c })} : ${msg}`);
        }
      }
    }

    for (const fc of template.formulas) {
      if (fc.sheet !== template.previewSheetName) continue;
      const addrPart = fc.address.includes('!') ? fc.address.split('!')[1]! : fc.address;
      let f = fc.formula.trim();
      if (f.startsWith('=')) f = f.slice(1);
      let cellAddr = addrPart;
      if (cellAddr.includes('$')) cellAddr = cellAddr.replaceAll('$', '');
      const cell = XLSX.utils.decode_cell(cellAddr);
      hf.setCellContents({ sheet: sid, col: cell.c, row: cell.r }, [[`=${f}`]]);
    }

    const dims = hf.getSheetDimensions(sid);
    const out: string[][] = [];
    for (let r = 0; r < dims.height; r++) {
      const line: string[] = [];
      for (let c = 0; c < dims.width; c++) {
        const v = hf.getCellValue({ sheet: sid, row: r, col: c });
        line.push(cellValueToString(v));
      }
      out.push(line);
    }
    return { rows: out, errors };
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e);
    errors.push(msg);
    return { rows: pr.map((r) => [...r.cells]), errors };
  }
}

/**
 * Recalcule la feuille principale du template avec HyperFormula.
 * Si le template a été importé avec `calcSheets` (classeur multi-feuilles + plage large), tous les calculs
 * et références croisées prises en charge par HF sont rejoués. Sinon, repli sur l’aperçu tronqué.
 */
export function computePreviewGridWithFormulas(template: StoredPrimeTemplate): {
  rows: string[][];
  errors: string[];
} {
  const cs = template.calcSheets;
  if (cs && Object.keys(cs).length > 0) {
    return computeFromCalcSheets(template, cs);
  }
  return computeFromPreviewRowsOnly(template);
}
