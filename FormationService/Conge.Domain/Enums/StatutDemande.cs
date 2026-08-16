namespace Conge.Domain.Enums;

public enum StatutDemande
{
    EnAttente = 1,
    Validee = 2,
    Refusee = 3,
    Annulee = 4,
    /// <summary>Validée par le superviseur, en attente de validation RH.</summary>
    EnAttenteRh = 5
}
