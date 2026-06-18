using System;

namespace Formation.Domain.Entities;

/// <summary>Annuaire employé synchronisé depuis Planning / Organisation RH (guid canonique).</summary>
public class EmployeAnnuaire
{
    public Guid Id { get; set; }
    public Guid EmployeId { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public DateTime DerniereModification { get; set; }
}
