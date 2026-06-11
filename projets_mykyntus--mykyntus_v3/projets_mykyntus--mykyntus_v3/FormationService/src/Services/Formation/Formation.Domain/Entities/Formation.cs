using Formation.Domain.Enums;
using System.Linq;
using Formation.Domain.Events;
using Shared.Kernel;
using System;
using System.Collections.Generic;

namespace Formation.Domain.Entities;

public class FormationEntity : AggregateRoot
{
    public string Titre { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Formateur { get; private set; } = string.Empty;
    public DateTime DateDebut { get; private set; }
    public DateTime DateFin { get; private set; }
    public int CapaciteMax { get; private set; }
    public decimal Prix { get; private set; }
    public StatutFormation Statut { get; private set; }

    private readonly List<Inscription> _inscriptions = new();
    public IReadOnlyCollection<Inscription> Inscriptions => _inscriptions.AsReadOnly();

    private FormationEntity() { } // EF Core

    public static FormationEntity Create(string titre, string description, string formateur,
        DateTime dateDebut, DateTime dateFin, int capaciteMax, decimal prix)
    {
        var formation = new FormationEntity
        {
            Titre = titre,
            Description = description,
            Formateur = formateur,
            DateDebut = dateDebut,
            DateFin = dateFin,
            CapaciteMax = capaciteMax,
            Prix = prix,
            Statut = StatutFormation.Brouillon
        };
        formation.AddDomainEvent(new FormationCreeeEvent(formation.Id, titre));
        return formation;
    }

    public void Update(string titre, string description, string formateur,
        DateTime dateDebut, DateTime dateFin, int capaciteMax, decimal prix)
    {
        Titre = titre;
        Description = description;
        Formateur = formateur;
        DateDebut = dateDebut;
        DateFin = dateFin;
        CapaciteMax = capaciteMax;
        Prix = prix;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result Valider()
    {
        if (Statut != StatutFormation.Brouillon && Statut != StatutFormation.EnAttente)
            return Result.Failure("La formation ne peut pas être validée dans son état actuel.");

        Statut = StatutFormation.Validee;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new FormationValideeEvent(Id, Titre));
        return Result.Success();
    }

    public Result<Inscription> Inscrire(Guid employeId, string nomEmploye)
    {
        if (Statut != StatutFormation.Validee)
            return Result<Inscription>.Failure("La formation n'est pas ouverte aux inscriptions.");

        if (_inscriptions.Count >= CapaciteMax)
            return Result<Inscription>.Failure("La formation est complète.");

        if (_inscriptions.Any(i => i.EmployeId == employeId && i.Statut != StatutInscription.Annulee))
            return Result<Inscription>.Failure("L'employé est déjà inscrit.");

        var inscription = Inscription.Create(Id, employeId, nomEmploye);
        _inscriptions.Add(inscription);
        return Result<Inscription>.Success(inscription);
    }
}