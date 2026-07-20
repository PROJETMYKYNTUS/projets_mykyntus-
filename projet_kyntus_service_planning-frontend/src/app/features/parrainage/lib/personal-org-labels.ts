import type { ParrainageUser } from '../models/referral.model';

export interface PersonalOrgLabels {
  employeeName: string;
  departement: string;
  pole: string;
  cellule: string;
  equipe: string;
}

export function formatOrgCompactLine(organizational: { departement: string; pole: string; cellule: string }): string {
  return [organizational.departement, organizational.pole, organizational.cellule]
    .filter((v) => v && v.trim() !== '' && v !== '—')
    .join(' • ');
}

export function getParrainagePersonalOrgLabels(user: ParrainageUser | null): PersonalOrgLabels {
  if (!user) {
    return { employeeName: '—', departement: '—', pole: '—', cellule: '—', equipe: '—' };
  }
  return {
    employeeName: user.name,
    departement: '—',
    pole: '—',
    cellule: '—',
    equipe: '—',
  };
}
