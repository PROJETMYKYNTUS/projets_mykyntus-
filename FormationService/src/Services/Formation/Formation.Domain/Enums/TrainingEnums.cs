namespace Formation.Domain.Enums;

public enum TrainingSessionType
{
    Continue = 0,
}

public enum AnimatorKind
{
    Internal = 0,
    External = 1,
}

public enum TrainingSessionStatus
{
    Draft = 0,
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

public enum TrainingAssignmentStatus
{
    Assigned = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
}

public enum InitialTrainingStatus
{
    EnCours = 0,
    QuizASaisir = 1,
    AttenteValidationFormateur = 2,
    AttenteValidationRh = 3,
    EnProduction = 4,
    Rejete = 5,
}

public enum TrainingProgramMode
{
    Single = 0,
    Multiple = 1,
}

public enum TrainingQuizStatus
{
    Draft = 0,
    Published = 1,
    Graded = 2,
    Validated = 3,
    Rejected = 4,
}

public enum TrainingQuizQuestionType
{
    Qcm = 0,
    FreeText = 1,
}
