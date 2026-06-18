using Conge.Application.DTOs;
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
            result.Add(new DemandeCongeDto(
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
                employe?.Nom,
                employe?.Prenom));
        }

        return result.OrderByDescending(r => r.DateDemande);
    }
}
