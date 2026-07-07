namespace Prime.Infrastructure.Persistence;

/// <summary>Configuration singleton — diviseur pour la formule de sanction absence.</summary>
public class PrimeAbsenceSanctionConfigEntity
{
    public const string SingletonId = "default";

    public string Id { get; set; } = SingletonId;
    public int DivisorDays { get; set; } = 26;
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
}
