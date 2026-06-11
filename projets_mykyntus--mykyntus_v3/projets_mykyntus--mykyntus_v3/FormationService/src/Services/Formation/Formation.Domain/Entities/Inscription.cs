using Formation.Domain.Enums;
using Shared.Kernel;

using System;

namespace Formation.Domain.Entities;

public class Inscription : AggregateRoot
{
    public Guid FormationId { get; private set; }
    public Guid EmployeId { get; private set; }
    public string NomEmploye { get; private set; } = string.Empty;
    public StatutInscription Statut { get; private set; }
    public int Progression { get; private set; }
    public DateTime? DateValidation { get; private set; }
    public string? Certificat { get; private set; }

    private Inscription() { }

    public static Inscription Create(Guid formationId, Guid employeId, string nomEmploye)
        => new()
        {
            FormationId = formationId,
            EmployeId = employeId,
            NomEmploye = nomEmploye,
            Statut = StatutInscription.EnAttente,
            Progression = 0
        };

    public Result MettreAjourProgression(int progression)
    {
        if (progression < 0 || progression > 100)
            return Result.Failure("La progression doit être entre 0 et 100.");

        Progression = progression;
        UpdatedAt = DateTime.UtcNow;

        if (progression == 100)
        {
            Statut = StatutInscription.Terminee;
            DateValidation = DateTime.UtcNow;
        }
        return Result.Success();
    }
}