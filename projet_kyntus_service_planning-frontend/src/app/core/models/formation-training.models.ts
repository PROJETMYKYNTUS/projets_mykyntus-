export type AnimatorKind = 'Internal' | 'External';
export type TrainingSessionStatus = 'Draft' | 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled';
export type TrainingAttendance = 'Pending' | 'Present' | 'Absent';
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

export interface TrainingAssignmentDto {
  id: string;
  sessionId: string;
  employeeId: string;
  employeeName: string;
  status: string;
  attendance: TrainingAttendance;
}

/** Session continue où l'employé est bénéficiaire (Mes formations). */
export interface MyAssignedTrainingSessionDto {
  sessionId: string;
  assignmentId: string;
  title: string;
  plannedStart: string;
  plannedEnd: string;
  status: TrainingSessionStatus;
  attendance: TrainingAttendance;
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

export const TRAINING_ATTENDANCE_LABELS: Record<TrainingAttendance, string> = {
  Pending: 'Non pointé',
  Present: 'Présent',
  Absent: 'Absent',
};

/** Parcours initial encore en pipeline (hors production / rejet). */
export const INITIAL_TRAINING_ACTIVE_STATUSES: InitialTrainingStatus[] = [
  'EnCours',
  'QuizASaisir',
  'AttenteValidationFormateur',
  'AttenteValidationRh',
];
