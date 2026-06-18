// Conge.Application/Queries/GetDemandesByEmploye/GetDemandesByEmployeQueryHandler.cs

using Conge.Application.DTOs;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetDemandesByEmploye;

public class GetDemandesByEmployeQueryHandler
    : IRequestHandler<GetDemandesByEmployeQuery, IEnumerable<DemandeCongeDto>>
{
    private readonly IDemandeCongeRepository _repo;

    public GetDemandesByEmployeQueryHandler(IDemandeCongeRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<DemandeCongeDto>> Handle(
        GetDemandesByEmployeQuery request,
        CancellationToken cancellationToken)
    {
        // Récupère toutes les demandes de l'employé
        var demandes = await _repo.GetByEmployeIdAsync(request.EmployeId, cancellationToken);

        // Filtre par statut si fourni
        if (request.Statut.HasValue)
            demandes = demandes.Where(d => d.Statut == request.Statut.Value);

        return demandes.Select(d => new DemandeCongeDto(
           d.Id,
           d.EmployeId,
           d.ManagerId,
           d.TypeConge,
           d.TypeExceptionnel,
           d.DateDebut,
           d.DateFin,
           d.NombreJours,
           d.Statut,
           d.Motif,
           d.CommentaireManager,
           d.DateDemande,
           d.DateDecision,
           null,   // ✅ NomEmploye
           null    // ✅ PrenomEmploye
       ));
    }
}