namespace Formation.Domain;

/// <summary>
/// Règles d’avancement d’un bénéficiaire sur un programme de formation continue.
/// </summary>
public static class ProgramBeneficiaryCompletion
{
    public static bool IsComplete(
        bool attendedInPerson,
        bool hasContentTrack,
        bool contentCompleted,
        bool hasQuizTrack,
        bool quizPassed)
    {
        if (!attendedInPerson)
            return false;
        if (hasContentTrack && !contentCompleted)
            return false;
        if (hasQuizTrack && !quizPassed)
            return false;
        return true;
    }

    /// <summary>
    /// N’affecte pas un agent déjà inscrit à la séance, ni un agent déjà terminé sur le programme.
    /// </summary>
    public static bool ShouldAssignToSession(
        Guid employeeId,
        IReadOnlySet<Guid> alreadyOnSession,
        IReadOnlySet<Guid> completedOnProgram)
    {
        if (alreadyOnSession.Contains(employeeId))
            return false;
        if (completedOnProgram.Contains(employeeId))
            return false;
        return true;
    }
}
