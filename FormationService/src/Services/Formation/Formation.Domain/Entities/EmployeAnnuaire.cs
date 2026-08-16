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
    /// <summary>Clé structure/cellule pour ciblage catalogue (optionnel, rétrocompat).</summary>
    public string? StructureKey { get; set; }
    public string? DepartmentId { get; set; }
    public string? PoleId { get; set; }
    public string? CelluleId { get; set; }
    public string? ServiceId { get; set; }
    /// <summary>Libellés optionnels pour affichage.</summary>
    public string? DepartmentName { get; set; }
    public string? PoleName { get; set; }
    public string? CelluleName { get; set; }
    public string? ServiceName { get; set; }
    public Guid ManagerId { get; set; }
    public DateTime DerniereModification { get; set; }
}
