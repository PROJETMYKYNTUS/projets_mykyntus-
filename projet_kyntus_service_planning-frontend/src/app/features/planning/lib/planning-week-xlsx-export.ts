import type * as ExcelJSTypes from 'exceljs';
import type {
  DayAssignment,
  EmployeePlanning,
  WeeklyPlanningResponse,
} from '../services/planning.service';
import { contractLevelLabel } from '../../../core/hr/user-hr-display.util';

const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;

const DAY_LABELS: Record<string, string> = {
  Monday: 'Lundi',
  Tuesday: 'Mardi',
  Wednesday: 'Mercredi',
  Thursday: 'Jeudi',
  Friday: 'Vendredi',
  Saturday: 'Samedi',
};

type AbsenceLabelFn = (value: string | null) => string;

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

function normalizeDayKey(day: string | null | undefined): string {
  return (day ?? '').trim().toLowerCase();
}

function dayKeyAliases(day: string): Set<string> {
  const groups = [
    ['monday', 'lundi'],
    ['tuesday', 'mardi'],
    ['wednesday', 'mercredi'],
    ['thursday', 'jeudi'],
    ['friday', 'vendredi'],
    ['saturday', 'samedi'],
    ['sunday', 'dimanche'],
  ];
  const key = normalizeDayKey(day);
  for (const g of groups) {
    if (g.includes(key)) return new Set(g);
  }
  return new Set([key]);
}

function getAssignment(employee: EmployeePlanning, day: string): DayAssignment | null {
  const aliases = dayKeyAliases(day);
  return employee.days.find((d) => aliases.has(normalizeDayKey(d.day))) ?? null;
}

function formatDateHeader(weekStartDate: string, day: string): string {
  const offsets: Record<string, number> = {
    Monday: 0,
    Tuesday: 1,
    Wednesday: 2,
    Thursday: 3,
    Friday: 4,
    Saturday: 5,
  };
  const d = new Date(weekStartDate);
  if (Number.isNaN(d.getTime())) {
    return DAY_LABELS[day] ?? day;
  }
  d.setDate(d.getDate() + (offsets[day] ?? 0));
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  return `${DAY_LABELS[day] ?? day} ${dd}/${mm}`;
}

function formatCell(a: DayAssignment | null, getAbsenceLabel: AbsenceLabelFn): string {
  if (!a) return 'OFF';
  if (a.isOnLeave) return getAbsenceLabel(a.absenceType);
  if (a.isHoliday) {
    return a.holidayName?.trim() ? `Férié — ${a.holidayName.trim()}` : 'Férié';
  }
  if (!a.startTime && !a.shiftLabel) return 'OFF';

  const parts: string[] = [];
  if (a.startTime && a.endTime) parts.push(`${a.startTime}–${a.endTime}`);
  else if (a.startTime) parts.push(a.startTime);
  if (a.shiftLabel) parts.push(a.shiftLabel);
  if (a.shiftModeTitle) {
    parts.push(a.isModeOverride ? `${a.shiftModeTitle} (Switch)` : a.shiftModeTitle);
  }
  if (a.breakTime) parts.push(`Pause ${a.breakTime}`);
  if (a.isExceptionalRequest) parts.push('DE');
  if (a.isManagerOverride && !a.isExceptionalRequest) parts.push('Modifié');
  if (a.slotLabel) parts.push(a.slotLabel);
  return parts.join(' | ') || 'OFF';
}

function slugPart(value: string): string {
  return (
    value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-zA-Z0-9]+/g, '-')
      .replace(/^-|-$/g, '')
      .slice(0, 40) || 'planning'
  );
}

/**
 * Exporte la grille hebdomadaire (employés × Lun–Sam) en .xlsx.
 */
export async function downloadPlanningWeekExcel(
  planning: WeeklyPlanningResponse,
  options?: { getAbsenceLabel?: AbsenceLabelFn },
): Promise<void> {
  const getAbsenceLabel = options?.getAbsenceLabel ?? ((v) => (v ? String(v) : 'Congé'));

  const ExcelJS = await loadExcelJS();
  if (typeof ExcelJS?.Workbook !== 'function') {
    throw new Error('ExcelJS.Workbook indisponible après import dynamique.');
  }

  const wb = new ExcelJS.Workbook();
  wb.creator = 'MyKyntus';
  wb.created = new Date();

  const sheet = wb.addWorksheet('Planning');
  const dayHeaders = DAYS.map((d) => formatDateHeader(planning.weekStartDate, d));

  sheet.getColumn(1).width = 28;
  sheet.getColumn(2).width = 12;
  for (let i = 0; i < DAYS.length; i++) sheet.getColumn(3 + i).width = 28;
  sheet.getColumn(3 + DAYS.length).width = 36;

  const meta = sheet.addRow([
    `Planning — ${planning.subServiceName}`,
    planning.weekCode,
    `Statut : ${planning.status}`,
    `${planning.assignments.length} employés`,
  ]);
  meta.font = { bold: true, size: 12 };

  const header = sheet.addRow(['Employé', 'Niveau', ...dayHeaders, 'Note']);
  header.font = { bold: true };
  header.alignment = { vertical: 'middle', wrapText: true };

  const sorted = [...planning.assignments].sort((a, b) =>
    a.fullName.localeCompare(b.fullName, 'fr', { sensitivity: 'base' }),
  );

  for (const emp of sorted) {
    const row = sheet.addRow([
      emp.fullName,
      contractLevelLabel(emp.level),
      ...DAYS.map((day) => formatCell(getAssignment(emp, day), getAbsenceLabel)),
      emp.managerComment?.trim() || '',
    ]);
    row.alignment = { vertical: 'top', wrapText: true };
  }

  const synthesis = planning.coverageReport?.daySynthesis;
  if (synthesis?.length) {
    const syn = wb.addWorksheet('Synthèse');
    syn.columns = [
      { header: 'Jour', key: 'day', width: 18 },
      { header: 'Date', key: 'date', width: 12 },
      { header: 'Présents', key: 'present', width: 10 },
      { header: 'Absences', key: 'leave', width: 10 },
      { header: 'Plateau %', key: 'plateau', width: 12 },
      { header: 'Niveau %', key: 'level', width: 12 },
      { header: 'Rotation %', key: 'rot', width: 12 },
    ];
    syn.getRow(1).font = { bold: true };
    for (const d of synthesis) {
      syn.addRow({
        day: DAY_LABELS[d.day] ?? d.day,
        date: d.date,
        present: d.presentCount ?? '',
        leave: d.leaveCount,
        plateau: d.plateauAvailabilityPercent ?? '',
        level: d.levelBalancePercent ?? '',
        rot: d.rotationCompliancePercent ?? '',
      });
    }
  }

  const fileName = stampFileName(
    `planning-${slugPart(planning.subServiceName)}-${slugPart(planning.weekCode)}`,
  );
  await downloadWorkbook(wb, fileName);
}
