using Conge.Domain.Exceptions;

namespace Conge.Domain.Entities;

/// <summary>
/// Solde de congés d'un employé pour une année donnée.
/// Initialisé par le service RH via RabbitMQ.
/// </summary>
public class SoldeConge
{
    public Guid Id { get; private set; }
    public Guid EmployeId { get; private set; }
    public int Annee { get; private set; }
    public double SoldeInitial { get; private set; }
    public double SoldeUtilise { get; private set; }
    public double SoldeRestant => SoldeInitial - SoldeUtilise;
    public DateTime DateCreation { get; private set; }
    public DateTime DerniereModification { get; private set; }

    // EF Core constructor
    private SoldeConge() { }

    public static SoldeConge Initialiser(Guid employeId, double soldeInitial, int annee)
    {
        if (soldeInitial < 0)
            throw new ArgumentException("Le solde initial ne peut pas être négatif.");

        return new SoldeConge
        {
            Id = Guid.NewGuid(),
            EmployeId = employeId,
            Annee = annee,
            SoldeInitial = soldeInitial,
            SoldeUtilise = 0,
            DateCreation = DateTime.UtcNow,
            DerniereModification = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Débite le solde lors de la validation d'une demande.
    /// </summary>
    public void DeduireSolde(double nombreJours)
    {
        if (nombreJours <= 0)
            throw new ArgumentException("Le nombre de jours doit être positif.");

        if (nombreJours > SoldeRestant)
            throw new SoldeInsuffisantException(EmployeId, nombreJours, SoldeRestant);

        SoldeUtilise += nombreJours;
        DerniereModification = DateTime.UtcNow;
    }

    /// <summary>
    /// Recrédite le solde en cas de refus ou annulation.
    /// </summary>
    public void RestituerSolde(double nombreJours)
    {
        if (nombreJours <= 0)
            throw new ArgumentException("Le nombre de jours doit être positif.");

        SoldeUtilise = Math.Max(0, SoldeUtilise - nombreJours);
        DerniereModification = DateTime.UtcNow;
    }

    public bool ASufficament(double nombreJours) => SoldeRestant >= nombreJours;
}