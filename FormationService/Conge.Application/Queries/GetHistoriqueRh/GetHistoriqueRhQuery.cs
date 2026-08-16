using Conge.Application.DTOs;
using Conge.Application.Queries.GetDemandesByManager;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetHistoriqueRh;

public record GetHistoriqueRhQuery(int Annee) : IRequest<IEnumerable<DemandeCongeDto>>;

public class GetHistoriqueRhHandler : IRequestHandler<GetHistoriqueRhQuery, IEnumerable<DemandeCongeDto>>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;

    public GetHistoriqueRhHandler(
        IDemandeCongeRepository demandeRepo,
        IEmployeSnapshotRepository employeRepo)
    {
        _demandeRepo = demandeRepo;
        _employeRepo = employeRepo;
    }

    public async Task<IEnumerable<DemandeCongeDto>> Handle(GetHistoriqueRhQuery request, CancellationToken ct)
    {
        var demandes = await _demandeRepo.GetByAnneeAsync(request.Annee, ct);
        var result = new List<DemandeCongeDto>();

        foreach (var d in demandes)
        {
            var employe = await _employeRepo.GetByEmployeIdAsync(d.EmployeId, ct);
            string? supNom = null;
            string? rhNom = null;
            if (d.SuperviseurDecideurId is { } sid)
                supNom = (await _employeRepo.GetByEmployeIdAsync(sid, ct))?.NomComplet;
            if (d.RhDecideurId is { } rid)
                rhNom = (await _employeRepo.GetByEmployeIdAsync(rid, ct))?.NomComplet;
            result.Add(GetDemandesByManagerHandler.Map(d, employe, supNom, rhNom));
        }

        return result.OrderByDescending(r => r.DateDemande);
    }
}
