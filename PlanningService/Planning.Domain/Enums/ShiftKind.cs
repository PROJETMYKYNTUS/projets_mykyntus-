namespace Planning.Domain.Enums;

/// <summary>
/// Typage métier d'un shift. Le samedi n'est pas un kind :
/// la règle d'équilibre s'y applique via le jour (IsSaturday).
/// </summary>
public enum ShiftKind
{
    Standard = 0,
    Opening = 1,
    Closing = 2
}
