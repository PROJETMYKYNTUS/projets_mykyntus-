using Conge.Application.DTOs;
using Conge.Domain.Enums;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetDemandesByManager;

public class GetDemandesByManagerHandler
    : IRequestHandler<GetDemandesByManagerQuery, IEnumerable<DemandeCongeDto>>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;

    public GetDemandesByManagerHandler(
        IDemandeCongeRepository demandeRepo,
        IEmployeSnapshotRepository employeRepo)
    {
        _demandeRepo = demandeRepo;
        _employeRepo = employeRepo;
    }

    public async Task<IEnumerable<DemandeCongeDto>> Handle(
        GetDemandesByManagerQuery request,
        CancellationToken ct)
    {
        var manager = await _employeRepo.GetByEmployeIdAsync(request.ManagerId, ct);
        if (manager is null)
            return Array.Empty<DemandeCongeDto>();

        var demandes = IsRhOrAdmin(manager.Role)
            ? await _demandeRepo.GetByStatutAsync(StatutDemande.EnAttente, ct)
            : await _demandeRepo.GetByManagerIdAsync(request.ManagerId, ct);

        // 3. Enrichir avec le nom de l'employé depuis le snapshot
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
                employe?.Nom,       // ✅ AJOUTER
                employe?.Prenom     // ✅ AJOUTER
            ));
        }

        return result.OrderByDescending(r => r.DateDemande);
    }

    private static bool IsRhOrAdmin(string? role)
    {
        var r = role?.Trim() ?? string.Empty;
        return r.Equals("RH", StringComparison.OrdinalIgnoreCase)
            || r.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    }
}