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

    public AnnulerCongeHandler(
        IDemandeCongeRepository demandeRepo,
        ISoldeCongeRepository soldeRepo,
        IUnitOfWork unitOfWork)
    {
        _demandeRepo = demandeRepo;
        _soldeRepo = soldeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AnnulerCongeCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        // Vérifier que c'est bien le propriétaire
        if (demande.EmployeId != request.EmployeId)
            throw new UnauthorizedAccessException("Vous ne pouvez annuler que vos propres demandes.");

        var ancienStatut = demande.Statut;

        // Logique métier dans l'entité
        demande.Annuler();

        // Si la demande était déjà validée, recréditer le solde annuel
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

        return true;
    }
}
