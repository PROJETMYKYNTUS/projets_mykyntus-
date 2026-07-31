namespace Formation.Domain.Entities;

/// <summary>
/// Règle d'audience pour un item catalogue.
/// Listes vides = non filtrantes. MatchAny/MatchAll défini sur le catalogue.
/// </summary>
public class TrainingCatalogAudienceRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CatalogItemId { get; set; }
    /// <summary>JSON array of role names, e.g. ["Agent","Superviseur"].</summary>
    public string RolesJson { get; set; } = "[]";
    /// <summary>JSON array of structure/cell keys.</summary>
    public string StructureKeysJson { get; set; } = "[]";
    /// <summary>JSON array of employee GUIDs.</summary>
    public string UserIdsJson { get; set; } = "[]";

    public TrainingCatalogItem? CatalogItem { get; set; }
}
