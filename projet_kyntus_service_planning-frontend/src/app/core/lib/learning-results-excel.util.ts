import type * as ExcelJSTypes from 'exceljs';
import type { LearningQuizResultExportRowDto } from '../models/formation-training.models';

async function loadExcelJS(): Promise<typeof import('exceljs')> {
  const mod = await import('exceljs');
  return ((mod as unknown) as { default?: typeof import('exceljs') }).default ?? mod;
}

async function downloadWorkbook(wb: ExcelJSTypes.Workbook, fileName: string): Promise<void> {
  const buf = await wb.xlsx.writeBuffer();
  const blob = new Blob([buf], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.rel = 'noopener';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Export des résultats quiz e-learning (même colonnes que le CSV catalogue). */
export async function downloadLearningResultsExcel(
  rows: LearningQuizResultExportRowDto[],
  options?: { fileNamePrefix?: string },
): Promise<void> {
  const ExcelJS = await loadExcelJS();
  if (typeof ExcelJS?.Workbook !== 'function') {
    throw new Error('ExcelJS.Workbook indisponible après import dynamique.');
  }

  const wb = new ExcelJS.Workbook();
  wb.creator = 'MyKyntus';
  wb.created = new Date();

  const sheet = wb.addWorksheet('Résultats');
  sheet.columns = [
    { header: 'Collaborateur', key: 'employeeName', width: 28 },
    { header: 'Email', key: 'email', width: 32 },
    { header: 'Rôle', key: 'role', width: 18 },
    { header: 'Structure', key: 'structureKey', width: 24 },
    { header: 'Session', key: 'sessionTitle', width: 32 },
    { header: 'Score', key: 'score', width: 10 },
    { header: 'Réussi', key: 'passed', width: 10 },
    { header: 'Tentative', key: 'attemptNumber', width: 12 },
    { header: 'Date', key: 'submittedAt', width: 22 },
  ];

  for (const r of rows) {
    sheet.addRow({
      employeeName: r.employeeName,
      email: r.email,
      role: r.role,
      structureKey: r.structureKey,
      sessionTitle: r.sessionTitle,
      score: r.score ?? '',
      passed: r.passed == null ? '' : r.passed ? 'Oui' : 'Non',
      attemptNumber: r.attemptNumber,
      submittedAt: r.submittedAt,
    });
  }

  const prefix = options?.fileNamePrefix ?? 'resultats_formation';
  await downloadWorkbook(wb, `${prefix}_${new Date().toISOString().slice(0, 10)}.xlsx`);
}
