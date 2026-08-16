using Conge.Application.Contracts;
using Conge.Application.Services;
using Conge.Domain.Enums;
using Conge.Domain.Events;
using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Commands.ValiderConge;

public class ValiderCongeSuperviseurHandler : IRequestHandler<ValiderCongeSuperviseurCommand, bool>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CongeReglesService _regles;

    public ValiderCongeSuperviseurHandler(
        IDemandeCongeRepository demandeRepo,
        IEmployeSnapshotRepository employeRepo,
        IUnitOfWork unitOfWork,
        CongeReglesService regles)
    {
        _demandeRepo = demandeRepo;
        _employeRepo = employeRepo;
        _unitOfWork = unitOfWork;
        _regles = regles;
    }

    public async Task<bool> Handle(ValiderCongeSuperviseurCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        if (demande.Statut != StatutDemande.EnAttente)
            throw new InvalidOperationException("Seules les demandes en attente superviseur peuvent être validées ici.");

        var employe = await _employeRepo.GetByEmployeIdAsync(demande.EmployeId, ct)
            ?? throw new EmployeNotFoundException(demande.EmployeId);

        await _regles.AssertHorsPeriodeInterditeAsync(demande.DateDebut, demande.DateFin, ct);
        await _regles.AssertQuotaServiceDisponibleAsync(
            employe.ServiceId, demande.DateDebut, demande.DateFin, demande.Id, ct);

        var acteur = await _employeRepo.GetByEmployeIdAsync(request.SuperviseurId, ct);
        demande.ValiderParSuperviseur(
            request.SuperviseurId,
            request.Commentaire,
            acteur?.NomComplet,
            acteur?.Role ?? "Superviseur");
        _demandeRepo.Update(demande);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

public class ValiderCongeRhHandler : IRequestHandler<ValiderCongeRhCommand, bool>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly ISoldeCongeRepository _soldeRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICongeEventPublisher _publisher;
    private readonly CongeReglesService _regles;

    public ValiderCongeRhHandler(
        IDemandeCongeRepository demandeRepo,
        ISoldeCongeRepository soldeRepo,
        IEmployeSnapshotRepository employeRepo,
        IUnitOfWork unitOfWork,
        ICongeEventPublisher publisher,
        CongeReglesService regles)
    {
        _demandeRepo = demandeRepo;
        _soldeRepo = soldeRepo;
        _employeRepo = employeRepo;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _regles = regles;
    }

    public async Task<bool> Handle(ValiderCongeRhCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        if (demande.Statut != StatutDemande.EnAttenteRh)
            throw new InvalidOperationException("Seules les demandes en attente RH peuvent être validées ici.");

        var employe = await _employeRepo.GetByEmployeIdAsync(demande.EmployeId, ct)
            ?? throw new EmployeNotFoundException(demande.EmployeId);

        await _regles.AssertHorsPeriodeInterditeAsync(demande.DateDebut, demande.DateFin, ct);
        // excludeDemandeId : la demande occupe déjà le quota en EnAttenteRh
        await _regles.AssertQuotaServiceDisponibleAsync(
            employe.ServiceId, demande.DateDebut, demande.DateFin, demande.Id, ct);

        var acteur = await _employeRepo.GetByEmployeIdAsync(request.RhId, ct);
        var acteurNom = acteur?.NomComplet;
        demande.ValiderParRh(
            request.RhId,
            request.Commentaire,
            acteurNom,
            acteur?.Role ?? "RH");

        if (demande.TypeConge == TypeConge.Annuel)
        {
            var solde = await _soldeRepo.GetByEmployeAndAnneeAsync(
                demande.EmployeId, demande.DateDebut.Year, ct)
                ?? throw new SoldeNotFoundException(demande.EmployeId, demande.DateDebut.Year);

            solde.DeduireSolde(demande.NombreJours);
            _soldeRepo.Update(solde);
        }

        _demandeRepo.Update(demande);
        await _unitOfWork.SaveChangesAsync(ct);

        await _publisher.PublishCongeValideAsync(
            demande.EmployeId,
            demande.Id,
            demande.DateDebut,
            demande.DateFin,
            demande.NombreJours,
            demande.TypeConge.ToString(),
            demande.TypeExceptionnel?.ToString(),
            request.RhId,
            acteurNom,
            ct);

        foreach (var ev in demande.DomainEvents.OfType<CongeValideEvent>().ToList())
        {
            /* déjà publié via publisher */
        }
        demande.ClearDomainEvents();
        return true;
    }
}

/// <summary>
/// Compat route PUT /valider : superviseur si EnAttente, RH si EnAttenteRh.
/// </summary>
public class ValiderCongeHandler : IRequestHandler<ValiderCongeCommand, bool>
{
    private readonly IMediator _mediator;
    private readonly IDemandeCongeRepository _demandeRepo;

    public ValiderCongeHandler(IMediator mediator, IDemandeCongeRepository demandeRepo)
    {
        _mediator = mediator;
        _demandeRepo = demandeRepo;
    }

    public async Task<bool> Handle(ValiderCongeCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        return demande.Statut switch
        {
            StatutDemande.EnAttente => await _mediator.Send(
                new ValiderCongeSuperviseurCommand(request.DemandeId, request.ManagerId, request.Commentaire), ct),
            StatutDemande.EnAttenteRh => await _mediator.Send(
                new ValiderCongeRhCommand(request.DemandeId, request.ManagerId, request.Commentaire), ct),
            _ => throw new InvalidOperationException($"Impossible de valider une demande avec le statut '{demande.Statut}'.")
        };
    }
}
