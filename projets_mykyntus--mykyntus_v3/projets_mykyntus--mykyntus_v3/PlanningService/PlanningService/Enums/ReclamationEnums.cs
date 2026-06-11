namespace PlanningService.Enums
{
    public enum ReclamationStatus
    {
        Soumise = 0,
        EnCours = 1,
        Traitee = 2,
        Rejetee = 3,
        Cloturee = 4
    }

    public enum ReclamationType
    {
        ServiceQualite = 0,
        RessourcesHumaines = 1,
        Technique = 2,
        Administrative = 3,
        Autre = 4
    }

    public enum PropositionStatus
    {
        Soumise = 0,
        EnEvaluation = 1,
        Approuvee = 2,
        Rejetee = 3,
        EnCours = 4,
        Implementee = 5
    }

    public enum Priority
    {
        Basse = 0,
        Normale = 1,
        Haute = 2,
        Critique = 3
    }
}