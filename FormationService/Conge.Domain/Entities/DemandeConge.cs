using Conge.Domain.Enums;
using Conge.Domain.Events;
using Conge.Domain.Exceptions;

namespace Conge.Domain.Entities;

/// <summary>
/// Aggregate Root — Demande de congé.
/// Contient toute la logique métier relative à une demande.
/// </summary>
public class DemandeConge
{
    private readonly List<object> _domainEvents = new();
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    public Guid Id { get; private set; }
    public Guid EmployeId { get; private set; }
    public Guid ManagerId { get; private set; }
    public TypeConge TypeConge { get; private set; }
    public TypeCongeExceptionnel? TypeExceptionnel { get; private set; }
    public DateTime DateDebut { get; private set; }
    public DateTime DateFin { get; private set; }
    public double NombreJours { get; private set; }
    public StatutDemande Statut { get; private set; }
    public string? Motif { get; private set; }
    public string? CommentaireManager { get; private set; }
    public DateTime DateDemande { get; private set; }
    public DateTime? DateDecision { get; private set; }

    // EF Core constructor
    private DemandeConge() { }

    /// <summary>
    /// Crée une demande de congé annuel.
    /// </summary>
    public static DemandeConge CreerCongeAnnuel(
        Guid employeId,
        Guid managerId,
        DateTime dateDebut,
        DateTime dateFin,
        SoldeConge solde,
        EmployeSnapshot employe,
        string? motif = null)
    {
        // Règle : 6 mois d'ancienneté minimum
        if (!employe.EstEligibleCongeAnnuel())
            throw new EligibiliteException(employeId, "L'employé doit avoir au moins 6 mois d'ancienneté.");

        if (dateDebut >= dateFin)
            throw new ArgumentException("La date de début doit être antérieure à la date de fin.");

        if (dateDebut.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("La date de début ne peut pas être dans le passé.");

        var nombreJours = PolitiqueConge.CompterJoursOuvrables(dateDebut, dateFin);

        // Règle : solde suffisant
        if (!solde.ASufficament(nombreJours))
            throw new SoldeInsuffisantException(employeId, nombreJours, solde.SoldeRestant);

        var demande = new DemandeConge
        {
            Id = Guid.NewGuid(),
            EmployeId = employeId,
            ManagerId = managerId,
            TypeConge = TypeConge.Annuel,
            DateDebut = dateDebut,
            DateFin = dateFin,
            NombreJours = nombreJours,
            Statut = StatutDemande.EnAttente,
            Motif = motif,
            DateDemande = DateTime.UtcNow
        };

        demande._domainEvents.Add(new CongeDemandeEvent(demande.Id, employeId, managerId, TypeConge.Annuel, nombreJours));
        return demande;
    }

    /// <summary>
    /// Crée une demande de congé exceptionnel.
    /// </summary>
    public static DemandeConge CreerCongeExceptionnel(
        Guid employeId,
        Guid managerId,
        TypeCongeExceptionnel typeExceptionnel,
        DateTime dateDebut,
        string? motif = null)
    {
        var duree = PolitiqueConge.GetDureeExceptionnelle(typeExceptionnel);
        var dateFin = dateDebut.AddDays(duree - 1);

        var demande = new DemandeConge
        {
            Id = Guid.NewGuid(),
            EmployeId = employeId,
            ManagerId = managerId,
            TypeConge = TypeConge.Exceptionnel,
            TypeExceptionnel = typeExceptionnel,
            DateDebut = dateDebut,
            DateFin = dateFin,
            NombreJours = duree,
            Statut = StatutDemande.EnAttente,
            Motif = motif,
            DateDemande = DateTime.UtcNow
        };

        demande._domainEvents.Add(new CongeDemandeEvent(demande.Id, employeId, managerId, TypeConge.Exceptionnel, duree));
        return demande;
    }

    /// <summary>
    /// Validation par le manager.
    /// </summary>
    public void Valider(Guid managerId, string? commentaire = null)
    {
        if (Statut != StatutDemande.EnAttente)
            throw new InvalidOperationException($"Impossible de valider une demande avec le statut '{Statut}'.");

        Statut = StatutDemande.Validee;
        CommentaireManager = commentaire;
        DateDecision = DateTime.UtcNow;

        _domainEvents.Add(new CongeValideEvent(Id, EmployeId, managerId, NombreJours, TypeConge));
    }

    /// <summary>
    /// Refus par le manager.
    /// </summary>
    public void Refuser(Guid managerId, string commentaire)
    {
        if (Statut != StatutDemande.EnAttente)
            throw new InvalidOperationException($"Impossible de refuser une demande avec le statut '{Statut}'.");

        if (string.IsNullOrWhiteSpace(commentaire))
            throw new ArgumentException("Un motif de refus est obligatoire.");

        Statut = StatutDemande.Refusee;
        CommentaireManager = commentaire;
        DateDecision = DateTime.UtcNow;

        _domainEvents.Add(new CongeRefuseEvent(Id, EmployeId, managerId, commentaire));
    }

    /// <summary>
    /// Annulation par l'employé (uniquement si encore en attente).
    /// </summary>
    public void Annuler()
    {
        if (Statut != StatutDemande.EnAttente)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être annulées.");

        Statut = StatutDemande.Annulee;
        DateDecision = DateTime.UtcNow;

        _domainEvents.Add(new CongeAnnuleEvent(Id, EmployeId, NombreJours, TypeConge));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}