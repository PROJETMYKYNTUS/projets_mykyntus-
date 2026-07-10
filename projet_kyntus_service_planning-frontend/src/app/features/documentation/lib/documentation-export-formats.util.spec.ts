import { describe, expect, it } from 'vitest';
import { exportFormatsForRole, roleCanExportDocx } from './documentation-export-formats.util';

describe('exportFormatsForRole', () => {
  it('allows PDF only for Pilote', () => {
    expect(exportFormatsForRole('Pilote')).toEqual(['pdf']);
    expect(roleCanExportDocx('Pilote')).toBe(false);
  });

  it('allows PDF only for Coach / Manager / RP', () => {
    expect(exportFormatsForRole('Coach')).toEqual(['pdf']);
    expect(exportFormatsForRole('Manager')).toEqual(['pdf']);
    expect(exportFormatsForRole('RP')).toEqual(['pdf']);
  });

  it('allows PDF and DOCX for RH / Admin / Audit', () => {
    expect(exportFormatsForRole('RH')).toEqual(['pdf', 'docx']);
    expect(exportFormatsForRole('Admin')).toEqual(['pdf', 'docx']);
    expect(exportFormatsForRole('Audit')).toEqual(['pdf', 'docx']);
    expect(roleCanExportDocx('RH')).toBe(true);
  });

  it('defaults to PDF only when role is unknown', () => {
    expect(exportFormatsForRole(null)).toEqual(['pdf']);
    expect(exportFormatsForRole(undefined)).toEqual(['pdf']);
    expect(exportFormatsForRole('')).toEqual(['pdf']);
  });
});
