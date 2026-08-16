// Conge.Application/Queries/GetDemandesByEmploye/GetDemandesByEmployeQueryHandler.cs

using Conge.Application.DTOs;
using Conge.Application.Queries.GetDemandesByManager;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetDemandesByEmploye;

public class GetDemandesByEmployeQueryHandler
    : IRequestHandler<GetDemandesByEmployeQuery, IEnumerable<DemandeCongeDto>>
{
    private readonly IDemandeCongeRepository _repo;
    private readonly IEmployeSnapshotRepository _employeRepo;

    public GetDemandesByEmployeQueryHandler(
        IDemandeCongeRepository repo,
        IEmployeSnapshotRepository employeRepo)
    {
        _repo = repo;
        _employeRepo = employeRepo;
    }

    public async Task<IEnumerable<DemandeCongeDto>> Handle(
        GetDemandesByEmployeQuery request,
        CancellationToken cancellationToken)
    {
        var demandes = await _repo.GetByEmployeIdAsync(request.EmployeId, cancellationToken);

        if (request.Statut.HasValue)
            demandes = demandes.Where(d => d.Statut == request.Statut.Value);

        var result = new List<DemandeCongeDto>();
        foreach (var d in demandes)
        {
            string? supNom = null;
            string? rhNom = null;
            if (d.SuperviseurDecideurId is { } sid)
                supNom = (await _employeRepo.GetByEmployeIdAsync(sid, cancellationToken))?.NomComplet;
            if (d.RhDecideurId is { } rid)
                rhNom = (await _employeRepo.GetByEmployeIdAsync(rid, cancellationToken))?.NomComplet;
            result.Add(GetDemandesByManagerHandler.Map(d, null, supNom, rhNom));
        }

        return result;
    }
}
