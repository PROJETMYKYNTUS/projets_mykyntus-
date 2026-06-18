namespace Kyntus.Messaging.Contracts;

public static class KyntusGuidEncoding
{
    /// <summary>Encode un identifiant entier Planning (SubService, etc.) en Guid stable pour les messages inter-services.</summary>
    public static Guid FromIntId(int id) => new(id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
