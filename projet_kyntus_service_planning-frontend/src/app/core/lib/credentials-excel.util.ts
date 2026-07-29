import type * as ExcelJSTypes from 'exceljs';

export type CredentialExcelRow = {
  email: string;
  password: string;
  firstName?: string | null;
  lastName?: string | null;
  lineNumber?: number | null;
};

/** Vite / browser : exceljs expose souvent Workbook via `default`. */
async function loadExcelJS(): Promise<typeof import('exceljs')> {
  const mod = await import('exceljs');
  return ((mod as unknown) as { default?: typeof import('exceljs') }).default ?? mod;
}

function stampFileName(prefix: string): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${prefix}-${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}-${pad(d.getHours())}${pad(d.getMinutes())}.xlsx`;
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

/**
 * Génère et télécharge un Excel one-shot des identifiants (Email / MotDePasse / Prenom / Nom).
 * À utiliser uniquement tant que les secrets sont encore en mémoire UI.
 */
export async function downloadCredentialsExcel(
  rows: CredentialExcelRow[],
  options?: { fileNamePrefix?: string },
): Promise<void> {
  if (!rows.length) return;

  const ExcelJS = await loadExcelJS();
  if (typeof ExcelJS?.Workbook !== 'function') {
    throw new Error('ExcelJS.Workbook indisponible après import dynamique.');
  }

  const wb = new ExcelJS.Workbook();
  wb.creator = 'MyKyntus';
  wb.created = new Date();

  const notice = wb.addWorksheet('Notice');
  notice.getColumn(1).width = 100;
  const noticeLines = [
    'Identifiants MyKyntus — fichier confidentiel',
    '',
    '• Ce fichier contient des mots de passe en clair destinés uniquement à la remise RH → employé.',
    '• Les mots de passe ne sont plus récupérables dans l’application après avoir quitté cet écran.',
    '• Ne stockez pas ce fichier sur un partage public ni dans un canal non sécurisé.',
    '• Après remise, détruisez ou archivez le fichier selon la procédure RH interne.',
    '• En cas de perte d’identifiants, utilisez « Réinitialiser le mot de passe » dans Planning.',
  ];
  noticeLines.forEach((line, i) => {
    notice.getCell(i + 1, 1).value = line;
  });

  const sheet = wb.addWorksheet('Identifiants');
  sheet.columns = [
    { header: 'Email', key: 'email', width: 36 },
    { header: 'MotDePasse', key: 'password', width: 24 },
    { header: 'Prenom', key: 'firstName', width: 18 },
    { header: 'Nom', key: 'lastName', width: 18 },
    { header: 'LigneImport', key: 'lineNumber', width: 12 },
  ];
  const header = sheet.getRow(1);
  header.font = { bold: true };

  for (const row of rows) {
    sheet.addRow({
      email: row.email,
      password: row.password,
      firstName: row.firstName?.trim() || '',
      lastName: row.lastName?.trim() || '',
      lineNumber: row.lineNumber ?? '',
    });
  }

  const prefix = options?.fileNamePrefix ?? 'identifiants-mykyntus';
  await downloadWorkbook(wb, stampFileName(prefix));
}
