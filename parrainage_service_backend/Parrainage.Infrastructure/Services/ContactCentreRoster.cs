namespace Parrainage.Infrastructure.Services;

/// <summary>
/// Roster contact centre (miroir de init/contactcentre/roster.json) pour seeds Parrainage.
/// ReferrerId = GUID Auth SubjectId pour alignement JWT / « mes parrainages ».
/// </summary>
internal static class ContactCentreRoster
{
    internal sealed record Employee(
        string PrimeId,
        string FirstName,
        string LastName,
        string ContactEmail,
        string Role,
        Guid Guid,
        string? AuthEmail);

    internal static readonly Employee[] Employees =
    [
        new("e1", "Yasmine", "El Idrissi", "yasmine.elidrissi@contactcentre.ma", "Pilote",
            Guid.Parse("11111111-1111-4111-8111-111111111103"), "employee@kyntus.ma"),
        new("e2", "Mehdi", "Chraibi", "mehdi.chraibi@contactcentre.ma", "Pilote",
            Guid.Parse("22222222-2222-4222-8222-222222222002"), null),
        new("e3", "Ghita", "Benkirane", "ghita.benkirane@contactcentre.ma", "Chef de projet",
            Guid.Parse("11111111-1111-4111-8111-111111111107"), "rp@kyntus.ma"),
        new("e4", "Imane", "Fassi", "imane.fassi@contactcentre.ma", "Pilote",
            Guid.Parse("22222222-2222-4222-8222-222222222004"), null),
        new("e5", "Latifa", "Mansouri", "latifa.mansouri@contactcentre.ma", "RH",
            Guid.Parse("11111111-1111-4111-8111-111111111104"), "rh@kyntus.ma"),
        new("e6", "Hicham", "Benjelloun", "hicham.benjelloun@contactcentre.ma", "Chef de projet",
            Guid.Parse("11111111-1111-4111-8111-111111111110"), "formation@kyntus.ma"),
        new("e7", "Laila", "Zahidi", "laila.zahidi@contactcentre.ma", "Audit",
            Guid.Parse("11111111-1111-4111-8111-111111111109"), "audit@kyntus.ma"),
        new("e8", "Omar", "Tazi", "omar.tazi@contactcentre.ma", "Référent technique",
            Guid.Parse("11111111-1111-4111-8111-111111111106"), "coach@kyntus.ma"),
        new("e9", "Kenza", "Alami", "kenza.alami@contactcentre.ma", "Superviseur",
            Guid.Parse("11111111-1111-4111-8111-111111111111"), "superviseur@kyntus.ma"),
        new("e10", "Nadia", "Benchrif", "nadia.benchrif@contactcentre.ma", "Manager",
            Guid.Parse("11111111-1111-4111-8111-111111111105"), "manager@kyntus.ma"),
        new("e11", "Karim", "Oufkir", "karim.oufkir@contactcentre.ma", "Comptabilité",
            Guid.Parse("22222222-2222-4222-8222-222222222011"), null),
        new("e-admin", "Système", "Admin", "admin@contactcentre.ma", "Admin",
            Guid.Parse("11111111-1111-4111-8111-111111111108"), "admin@kyntus.ma"),
    ];

    internal static string DisplayName(Employee e) => $"{e.FirstName} {e.LastName}";

    internal static string ReferrerId(Employee e) => e.Guid.ToString();

    internal static Employee Require(string primeId) =>
        Employees.First(e => e.PrimeId == primeId);
}
