export type AnimatorKind = 'Internal' | 'External';
export type TrainingSessionStatus = 'Draft' | 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled';
export type TrainingAttendance = 'Pending' | 'Present' | 'Absent';
export type TrainingProgramMode = 'Single' | 'Multiple';
export type TrainingQuizStatus = 'Draft' | 'Published' | 'Graded' | 'Validated' | 'Rejected';
export type TrainingQuizQuestionType = 'Qcm' | 'FreeText';
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
  programId?: string | null;
  sequenceNumber?: number;
  hasReport?: boolean;
  quizId?: string | null;
  quizStatus?: TrainingQuizStatus | string | null;
}

export interface TrainingProgramDto {
  id: string;
  title: string;
  description: string;
  mode: TrainingProgramMode | number;
  sessionCount: number;
  animatorKind: AnimatorKind | number;
  animatorUserId?: string | null;
  externalAnimatorName?: string | null;
  capacity: number;
  sessions: TrainingSessionDto[];
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
  quizId?: string | null;
  quizStatus?: string | null;
  canTakeQuiz?: boolean;
  attemptId?: string | null;
  attemptGraded?: boolean;
  finalScore?: number | null;
  passed?: boolean | null;
}

export interface TrainingSessionReportDto {
  id: string;
  sessionId: string;
  fileName: string;
  contentType: string;
  uploadedAt: string;
}

export interface TrainingQuizQuestionDto {
  id: string;
  sortOrder: number;
  type: TrainingQuizQuestionType | number;
  prompt: string;
  options?: string[] | null;
  correctOptionIndex?: number | null;
  correctOptionIndexes?: number[] | null;
  allowMultiple?: boolean;
  points: number;
}

export interface TrainingQuizDto {
  id: string;
  sessionId: string;
  title: string;
  status: TrainingQuizStatus | number;
  questions: TrainingQuizQuestionDto[];
  rejectedReason?: string | null;
  /** Seuil de réussite en % (score ≥ plafond → Valide). */
  passThreshold?: number;
}

export interface TrainingQuizForEmployeeDto {
  id: string;
  sessionId: string;
  title: string;
  status: TrainingQuizStatus | number;
  questions: {
    id: string;
    sortOrder: number;
    type: TrainingQuizQuestionType | number;
    prompt: string;
    options?: string[] | null;
    points: number;
    allowMultiple?: boolean;
  }[];
}

export interface TrainingQuizAttemptAnswerDetailDto {
  questionId: string;
  sortOrder: number;
  type: TrainingQuizQuestionType | number;
  prompt: string;
  options?: string[] | null;
  selectedOptionIndex?: number | null;
  selectedOptionIndexes?: number[] | null;
  freeText?: string | null;
  correctOptionIndex?: number | null;
  correctOptionIndexes?: number[] | null;
  allowMultiple: boolean;
  isCorrect?: boolean | null;
  points: number;
}

export interface TrainingQuizAttemptDto {
  id: string;
  quizId: string;
  assignmentId: string;
  employeeId: string;
  employeeName: string;
  autoScore?: number | null;
  manualScore?: number | null;
  finalScore?: number | null;
  passed?: boolean | null;
  isGraded: boolean;
  submittedAt: string;
  animatorComment?: string | null;
  answers?: TrainingQuizAttemptAnswerDetailDto[] | null;
}

export interface FormationDashboardStatsDto {
  programCount: number;
  sessionCount: number;
  assignmentCount: number;
  presentCount: number;
  attendanceRate: number;
  quizCount: number;
  quizzesValidated: number;
  gradedAttempts: number;
  passedAttempts: number;
  quizSuccessRate: number;
  upcomingSessions: number;
  missingReports: number;
  quizzesPendingValidation: number;
}

export interface FormationInitialRiskItemDto {
  pathId: string;
  employeeId: string;
  employeeName: string;
  daysUntilEnd?: number | null;
  documentsReceivedCount: number;
  documentsTotalCount: number;
  missingDocumentTitles: string[];
}

export interface FormationInitialDashboardStatsDto {
  totalPaths: number;
  enCours: number;
  attenteValidationFormateur: number;
  attenteValidationRh: number;
  enProduction: number;
  rejete: number;
  pendingRh: number;
  avgQuizSuccessRate: number;
  pathsWithMissingDocs: number;
  endingWithin7Days: number;
  atRisk: FormationInitialRiskItemDto[];
}

export interface InitialTrainingQuizResultDto {
  id: string;
  title: string;
  score: number;
  passed: boolean;
  recordedBy?: string | null;
  recordedAt: string;
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
  quizResults?: InitialTrainingQuizResultDto[];
  quizSuccessRate?: number;
  documentsReceivedCount?: number;
  documentsTotalCount?: number;
  missingDocumentTitles?: string[];
  daysUntilEnd?: number | null;
}

export interface FormationDocumentDefinitionDto {
  id: string;
  title: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
}

export interface FormationDocumentChecklistItemDto {
  id: string;
  definitionId: string;
  title: string;
  sortOrder: number;
  isReceived: boolean;
  receivedAt?: string | null;
  receivedBy?: string | null;
  note?: string | null;
  pathId?: string | null;
}

/** Stub jusqu’à branchement de l’API absentéisme / retard externe. */
export interface FormationAttendanceMetricsStub {
  absenteeismRate: number | null;
  latenessRate: number | null;
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

export const TRAINING_QUIZ_STATUS_LABELS: Record<TrainingQuizStatus, string> = {
  Draft: 'Brouillon',
  Published: 'Publié',
  Graded: 'Noté',
  Validated: 'Validé',
  Rejected: 'Rejeté',
};

/** Parcours initial encore en pipeline (hors production / rejet). */
export const INITIAL_TRAINING_ACTIVE_STATUSES: InitialTrainingStatus[] = [
  'EnCours',
  'QuizASaisir',
  'AttenteValidationFormateur',
  'AttenteValidationRh',
];
