import type { DirectoryUserDto } from '../../shared/models/api.models';
import type { DocumentationRole } from '../interfaces/documentation-role';

export interface PersonalOrgLabels {
  employeeName: string;
  departement: string;
  pole: string;
  cellule: string;
  equipe: string;
}

const DEPT_NAMES: Record<string, string> = {
  'dept-doc-1': 'Opérations & Qualité',
};

const POLE_NAMES: Record<string, string> = {
  'pole-doc-1': 'Pôle Documentation',
};

const CELL_NAMES: Record<string, string> = {
  'cell-doc-1': 'Cellule Conformité',
};

const TEAM_NAMES: Record<string, string> = {
  'team-doc-1': 'Équipe Documents',
};

/**
 * Structure organisationnelle issue de l’annuaire réel chargé depuis l’API.
 */
export function getPersonalOrgLabelsForViewer(
  users: DirectoryUserDto[],
  userId: string | undefined,
  _role: DocumentationRole,
): PersonalOrgLabels {
  const person = userId ? users.find((user) => user.id === userId) : undefined;
  if (!person) {
    return {
      employeeName: '',
      departement: '—',
      pole: '—',
      cellule: '—',
      equipe: '—',
    };
  }
  return {
    employeeName: '',
    departement: person.departement?.name ?? DEPT_NAMES[person.departementId] ?? '—',
    pole: person.pole?.name ?? POLE_NAMES[person.poleId] ?? '—',
    cellule: person.cellule?.name ?? CELL_NAMES[person.celluleId] ?? '—',
    equipe: person.departement?.name ?? TEAM_NAMES[person.departementId] ?? '—',
  };
}

export function formatOrgCompactLine(organizational: {
  departement: string;
  pole: string;
  cellule: string;
}): string {
  return [organizational.departement, organizational.pole, organizational.cellule]
    .filter((v) => v && v.trim() !== '' && v !== '—')
    .join(' • ');
}
