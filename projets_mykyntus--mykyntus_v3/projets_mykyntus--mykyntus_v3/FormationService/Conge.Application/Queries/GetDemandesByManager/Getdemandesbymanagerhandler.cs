using Conge.Application.DTOs;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetDemandesByManager;

public class GetDemandesByManagerHandler : IRequestHandler<GetDemandesByManagerQuery, IEnumerable<DemandeCongeDto>>
{
    private readonly IDemandeCongeRepository _repo;

    public GetDemandesByManagerHandler(IDemandeCongeRepository repo)
        => _repo = repo;

    public async Task<IEnumerable<DemandeCongeDto>> Handle(GetDemandesByManagerQuery request, CancellationToken ct)
    {
        var demandes = await _repo.GetByManagerIdAsync(request.ManagerId, ct);

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
            d.DateDecision
        ));
    }
}