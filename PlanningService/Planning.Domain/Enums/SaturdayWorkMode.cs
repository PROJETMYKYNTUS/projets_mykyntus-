namespace Planning.Domain.Enums;

/// <summary>
/// Mode de travail samedi. Stocké en nullable sur User :
/// null = défaut selon Niveau (1 → EveryHalfDay, sinon AlternatingFullDay).
/// </summary>
public enum SaturdayWorkMode
{
    EveryHalfDay = 1,
    AlternatingFullDay = 2
}
