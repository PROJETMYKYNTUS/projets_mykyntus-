using Conge.Application.Contracts;
using Conge.Domain.Enums;
using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Commands.AnnulerConge;

public class AnnulerCongeHandler : IRequestHandler<AnnulerCongeCommand, bool>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly ISoldeCongeRepository _soldeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICongeEventPublisher _publisher;

    public AnnulerCongeHandler(
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

    public async Task<bool> Handle(AnnulerCongeCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        if (demande.EmployeId != request.EmployeId)
            throw new UnauthorizedAccessException("Vous ne pouvez annuler que vos propres demandes.");

        var ancienStatut = demande.Statut;

        demande.Annuler(request.EmployeId);

        if (ancienStatut == StatutDemande.Validee && demande.TypeConge == TypeConge.Annuel)
        {
            var solde = await _soldeRepo.GetByEmployeAndAnneeAsync(
                demande.EmployeId, demande.DateDebut.Year, ct);

            if (solde != null)
            {
                solde.RestituerSolde(demande.NombreJours);
                _soldeRepo.Update(solde);
            }
        }

        _demandeRepo.Update(demande);
        await _unitOfWork.SaveChangesAsync(ct);

        if (ancienStatut == StatutDemande.Validee)
        {
            await _publisher.PublishCongeRefuseAsync(
                demande.EmployeId,
                demande.Id,
                "Annulation après validation",
                request.EmployeId,
                null,
                ct);
        }

        return true;
    }
}
