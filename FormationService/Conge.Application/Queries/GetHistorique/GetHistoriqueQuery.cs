using Conge.Application.DTOs;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetHistorique;

public record GetHistoriqueQuery(
    Guid EmployeId,
    int Annee
) : IRequest<IEnumerable<DemandeCongeDto>>;

public class GetHistoriqueHandler : IRequestHandler<GetHistoriqueQuery, IEnumerable<DemandeCongeDto>>
{
    private readonly IDemandeCongeRepository _repo;

    public GetHistoriqueHandler(IDemandeCongeRepository repo)
        => _repo = repo;

    public async Task<IEnumerable<DemandeCongeDto>> Handle(GetHistoriqueQuery request, CancellationToken ct)
    {
        var demandes = await _repo.GetHistoriqueAsync(request.EmployeId, request.Annee, ct);

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