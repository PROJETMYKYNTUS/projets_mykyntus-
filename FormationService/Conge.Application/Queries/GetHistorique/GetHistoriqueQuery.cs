using Conge.Application.DTOs;
using Conge.Application.Queries.GetDemandesByManager;
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
    private readonly IEmployeSnapshotRepository _employeRepo;

    public GetHistoriqueHandler(
        IDemandeCongeRepository repo,
        IEmployeSnapshotRepository employeRepo)
    {
        _repo = repo;
        _employeRepo = employeRepo;
    }

    public async Task<IEnumerable<DemandeCongeDto>> Handle(GetHistoriqueQuery request, CancellationToken ct)
    {
        var demandes = await _repo.GetHistoriqueAsync(request.EmployeId, request.Annee, ct);
        var result = new List<DemandeCongeDto>();
        foreach (var d in demandes)
        {
            string? supNom = null;
            string? rhNom = null;
            if (d.SuperviseurDecideurId is { } sid)
                supNom = (await _employeRepo.GetByEmployeIdAsync(sid, ct))?.NomComplet;
            if (d.RhDecideurId is { } rid)
                rhNom = (await _employeRepo.GetByEmployeIdAsync(rid, ct))?.NomComplet;
            result.Add(GetDemandesByManagerHandler.Map(d, null, supNom, rhNom));
        }

        return result;
    }
}
