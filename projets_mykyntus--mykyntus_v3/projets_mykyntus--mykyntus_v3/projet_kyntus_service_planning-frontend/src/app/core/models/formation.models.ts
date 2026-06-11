export enum StatutFormation {
  Brouillon = 0,
  EnAttente = 1,
  Validee = 2,
  EnCours = 3,
  Terminee = 4,
  Annulee = 5
}

export const StatutFormationLabels: Record<number, string> = {
  0: 'Brouillon',
  1: 'En attente',
  2: 'Validée',
  3: 'En cours',
  4: 'Terminée',
  5: 'Annulée'
};

export interface FormationDto {
  id: string;
  titre: string;
  description: string;
  formateur: string;
  dateDebut: string;
  dateFin: string;
  capaciteMax: number;
  nombreInscrits: number;
  prix: number;
  statut: StatutFormation;
  createdAt: string;
}

export interface CreateFormationCommand {
  titre: string;
  description: string;
  formateur: string;
  dateDebut: string;
  dateFin: string;
  capaciteMax: number;
  prix: number;
}

export interface InscrireFormationCommand {
  formationId: string;
  employeId: string;
  nomEmploye: string;
}