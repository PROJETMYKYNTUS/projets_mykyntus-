using Planning.Application.DTOs.Planning;

namespace Planning.Application.Exceptions;

/// <summary>
/// Erreur métier planning avec anomalies structurées (génération / publish).
/// </summary>
public class PlanningValidationException : InvalidOperationException
{
    public IReadOnlyList<PlanningAnomalyDto> Anomalies { get; }

    public PlanningValidationException(string message, IEnumerable<PlanningAnomalyDto>? anomalies = null)
        : base(message)
    {
        Anomalies = anomalies?.ToList() ?? [];
    }
}
