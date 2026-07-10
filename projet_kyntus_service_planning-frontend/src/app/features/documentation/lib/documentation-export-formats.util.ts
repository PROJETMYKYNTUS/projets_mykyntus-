import type { DocumentationRole } from '../interfaces/documentation-role';

export type DocumentationExportFormat = 'pdf' | 'docx';

/** Formats de téléchargement autorisés selon le rôle Documentation. */
export function exportFormatsForRole(role: DocumentationRole | string | null | undefined): DocumentationExportFormat[] {
  const r = (role ?? '').trim();
  if (r === 'RH' || r === 'Admin' || r === 'Audit') {
    return ['pdf', 'docx'];
  }
  return ['pdf'];
}

export function roleCanExportDocx(role: DocumentationRole | string | null | undefined): boolean {
  return exportFormatsForRole(role).includes('docx');
}
