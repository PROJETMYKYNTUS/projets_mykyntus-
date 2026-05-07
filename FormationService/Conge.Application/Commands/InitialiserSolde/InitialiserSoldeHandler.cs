using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Commands.InitialiserSolde;

public class InitialiserSoldeHandler : IRequestHandler<InitialiserSoldeCommand, bool>
{
    private readonly ISoldeCongeRepository _soldeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public InitialiserSoldeHandler(
        ISoldeCongeRepository soldeRepo,
        IUnitOfWork unitOfWork)
    {
        _soldeRepo = soldeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(InitialiserSoldeCommand request, CancellationToken ct)
    {
        // Vérifier si un solde existe déjà pour cette année (idempotence)
        var soldeExistant = await _soldeRepo.GetByEmployeAndAnneeAsync(
            request.EmployeId, request.Annee, ct);

        if (soldeExistant != null)
            return true; // Déjà initialisé, on ignore (idempotent)

        // Calculer le solde selon les règles marocaines (art. 231-240)
        var soldeJours = PolitiqueConge.CalculerSoldeAnnuel(
            request.AncienneteAnnees,
            request.EstMineur);

        var solde = SoldeConge.Initialiser(request.EmployeId, soldeJours, request.Annee);

        await _soldeRepo.AddAsync(solde, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return true;
    }
}
