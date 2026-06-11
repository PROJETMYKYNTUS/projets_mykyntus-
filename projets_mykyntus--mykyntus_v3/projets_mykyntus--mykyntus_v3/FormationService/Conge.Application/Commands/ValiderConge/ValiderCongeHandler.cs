using Conge.Application.Contracts;
using Conge.Domain.Enums;
using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;
using System;

namespace Conge.Application.Commands.ValiderConge;

public class ValiderCongeHandler : IRequestHandler<ValiderCongeCommand, bool>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly ISoldeCongeRepository _soldeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICongeEventPublisher _publisher;

    public ValiderCongeHandler(
        IDemandeCongeRepository demandeRepo,
        ISoldeCongeRepository soldeRepo,
        IUnitOfWork unitOfWork,
        ICongeEventPublisher publisher)
    {
        _demandeRepo = demandeRepo;
        _soldeRepo = soldeRepo;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<bool> Handle(ValiderCongeCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        // Valider via la logique domaine
        demande.Valider(request.ManagerId, request.Commentaire);

        // Déduire du solde uniquement pour les congés annuels
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

        // Publier l'événement vers RabbitMQ (notifier RH/Planning)
        await _publisher.PublishCongeValideAsync(
            demande.EmployeId,
            demande.Id,
            demande.DateDebut,
            demande.DateFin,
            demande.NombreJours,
            ct);

        return true;
    }
}