namespace Kyntus.Messaging.Contracts;

/// <summary>
/// Publié par Formation lorsqu'un parcours de formation initiale est rejeté
/// (Formateur ou RH) — déclenche la sortie complète de l'employé côté Planning / Directory / Parrainage.
/// </summary>
public record InitialTrainingRejectedMessage
{
    public Guid TrainingPathId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string RejectedBy { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime RejectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Publié par Formation lorsque la RH valide le passage en production —
/// clear EnFormation, expertise initiale, sync Parrainage.
/// </summary>
public record InitialTrainingCompletedMessage
{
    public Guid TrainingPathId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    public DateOnly ProductionStartDate { get; init; }
    /// <summary>1 = Débutant, 2 = Confirmé, 3 = Expert. Défaut métier à la production : Débutant.</summary>
    public int NiveauExpertiseMetier { get; init; } = 1;
    public decimal? QuizScore { get; init; }
}

/// <summary>
/// Publié par Formation lorsqu'un employé est affecté à une session de formation continue —
/// notification in-app destinée au bénéficiaire.
/// </summary>
public record TrainingSessionAssignedMessage
{
    public Guid SessionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime PlannedStart { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateTime AssignedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Publié par Formation lorsqu'un animateur interne est désigné sur une session continue publiée.
/// </summary>
public record TrainingSessionAnimatorAssignedMessage
{
    public Guid SessionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime PlannedStart { get; init; }
    public DateTime PlannedEnd { get; init; }
    public Guid AnimatorUserId { get; init; }
    public DateTime AssignedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Publié par Formation lorsqu'une session continue passe en InProgress (démarrage horaire).
/// </summary>
public record TrainingSessionStartedMessage
{
    public Guid SessionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime PlannedStart { get; init; }
    public Guid RecipientUserId { get; init; }
    public string RecipientRole { get; init; } = "Beneficiary"; // Beneficiary | Animator
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Publié par Formation lorsqu'un parcours initial arrive à ≤ 7 jours de la fin
/// avec des documents physiques encore manquants — notification RH/Admin.
/// </summary>
public record InitialTrainingMissingDocumentsAlertMessage
{
    public Guid TrainingPathId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateTime DateFinPrevue { get; init; }
    public IReadOnlyList<string> MissingDocumentTitles { get; init; } = Array.Empty<string>();
    public DateTime AlertedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Rappel / escalade échéance catalogue e-learning (libre accès ou session).
/// </summary>
public record CatalogEnrollmentDeadlineReminderMessage
{
    public Guid EnrollmentId { get; init; }
    public Guid CatalogItemId { get; init; }
    public string CatalogTitle { get; init; } = string.Empty;
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateTime DueAt { get; init; }
    public bool IsEscalation { get; init; }
    public Guid? ManagerId { get; init; }
    public DateTime AlertedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Formation catalogue publiée (ou audience mise à jour alors qu’elle est déjà publiée) —
/// une notif in-app par employé concerné.
/// </summary>
public record CatalogFormationAvailableMessage
{
    public Guid CatalogItemId { get; init; }
    public string CatalogTitle { get; init; } = string.Empty;
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Quiz de séance continue publié — destinataires = affectations (pas filtré sur présence).</summary>
public record TrainingQuizPublishedMessage
{
    public Guid SessionId { get; init; }
    public Guid QuizId { get; init; }
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<Guid> EmployeeIds { get; init; } = Array.Empty<Guid>();
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Tentative de quiz soumise par un employé.</summary>
public record TrainingQuizAttemptSubmittedMessage
{
    public Guid SessionId { get; init; }
    public Guid QuizId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public bool IsGraded { get; init; }
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Tentative avec réponses libres à noter — destinée à l'animateur.</summary>
public record TrainingQuizNeedsGradingMessage
{
    public Guid SessionId { get; init; }
    public Guid QuizId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid AnimatorUserId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Quiz validé par l'animateur.</summary>
public record TrainingQuizValidatedMessage
{
    public Guid SessionId { get; init; }
    public Guid QuizId { get; init; }
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<Guid> EmployeeIds { get; init; } = Array.Empty<Guid>();
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Quiz rejeté par l'animateur.</summary>
public record TrainingQuizRejectedMessage
{
    public Guid SessionId { get; init; }
    public Guid QuizId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public Guid? AnimatorUserId { get; init; }
    public IReadOnlyList<Guid> EmployeeIds { get; init; } = Array.Empty<Guid>();
    public DateTime RejectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Résultat de tentative prêt (noté) — destinée à l'employé.</summary>
public record TrainingQuizResultReadyMessage
{
    public Guid SessionId { get; init; }
    public Guid QuizId { get; init; }
    public Guid AttemptId { get; init; }
    public Guid EmployeeId { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal? FinalScore { get; init; }
    public bool? Passed { get; init; }
    public DateTime ReadyAt { get; init; } = DateTime.UtcNow;
}
