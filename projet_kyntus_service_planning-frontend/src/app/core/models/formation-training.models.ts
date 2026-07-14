export type AnimatorKind = 'Internal' | 'External';
export type TrainingSessionStatus = 'Draft' | 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled';
export type InitialTrainingStatus =
  | 'EnCours'
  | 'QuizASaisir'
  | 'AttenteValidationFormateur'
  | 'AttenteValidationRh'
  | 'EnProduction'
  | 'Rejete';

export interface TrainingSessionDto {
  id: string;
  title: string;
  description: string;
  type: string;
  animatorKind: AnimatorKind;
  animatorUserId?: string | null;
  externalAnimatorName?: string | null;
  externalAnimatorOrganization?: string | null;
  externalAnimatorEmail?: string | null;
  externalAnimatorPhone?: string | null;
  plannedStart: string;
  plannedEnd: string;
  capacity: number;
  status: TrainingSessionStatus;
  assignmentCount: number;
}

export interface InitialTrainingPathDto {
  id: string;
  employeeId: string;
  employeeName: string;
  dateDebut: string;
  dateFinPrevue: string;
  status: InitialTrainingStatus;
  hasQuizResult: boolean;
  formateurValidatedAt?: string | null;
  rhValidatedAt?: string | null;
  rejectedBy?: string | null;
  rejectReason?: string | null;
}

export const INITIAL_TRAINING_STATUS_LABELS: Record<InitialTrainingStatus, string> = {
  EnCours: 'En cours',
  QuizASaisir: 'Quiz à saisir',
  AttenteValidationFormateur: 'Attente validation formateur',
  AttenteValidationRh: 'Attente validation RH',
  EnProduction: 'En production',
  Rejete: 'Rejeté',
};

export const TRAINING_SESSION_STATUS_LABELS: Record<TrainingSessionStatus, string> = {
  Draft: 'Brouillon',
  Scheduled: 'Planifiée',
  InProgress: 'En cours',
  Completed: 'Terminée',
  Cancelled: 'Annulée',
};

/** Parcours initial encore en pipeline (hors production / rejet). */
export const INITIAL_TRAINING_ACTIVE_STATUSES: InitialTrainingStatus[] = [
  'EnCours',
  'QuizASaisir',
  'AttenteValidationFormateur',
  'AttenteValidationRh',
];
