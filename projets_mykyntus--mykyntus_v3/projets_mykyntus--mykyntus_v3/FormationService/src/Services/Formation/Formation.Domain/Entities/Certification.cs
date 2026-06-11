using Shared.Kernel;
using System;

namespace Formation.Domain.Entities;

public class Certification : BaseEntity
{
    public Guid InscriptionId { get; private set; }
    public Guid EmployeId { get; private set; }
    public string NomFormation { get; private set; } = string.Empty;
    public string NomEmploye { get; private set; } = string.Empty;
    public DateTime DateObtention { get; private set; }
    public DateTime DateExpiration { get; private set; }
    public string NuméroCertificat { get; private set; } = string.Empty;

    private Certification() { }

    public static Certification Create(Guid inscriptionId, Guid employeId,
        string nomFormation, string nomEmploye)
        => new()
        {
            InscriptionId = inscriptionId,
            EmployeId = employeId,
            NomFormation = nomFormation,
            NomEmploye = nomEmploye,
            DateObtention = DateTime.UtcNow,
            DateExpiration = DateTime.UtcNow.AddYears(2),
            NuméroCertificat = $"CERT-{Guid.NewGuid().ToString()[..8].ToUpper()}"
        };
}