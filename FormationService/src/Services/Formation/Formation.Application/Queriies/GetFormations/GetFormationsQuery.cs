using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formation.Application.DTOs;
using Formation.Domain.Enums;
using Formation.Domain.Interfaces;
using MediatR;

namespace Formation.Application.Queriies.GetFormations; // ← double 'i' pour matcher le dossier

public record GetFormationsQuery(StatutFormation? Statut = null) : IRequest<List<FormationDto>>;

public class GetFormationsHandler : IRequestHandler<GetFormationsQuery, List<FormationDto>>
{
    private readonly IFormationRepository _repo;
    public GetFormationsHandler(IFormationRepository repo) => _repo = repo;

    public async Task<List<FormationDto>> Handle(GetFormationsQuery query, CancellationToken ct)
    {
        var formations = await _repo.GetAllAsync(query.Statut, ct);
        return formations.Select(f => new FormationDto(
            f.Id,
            f.Titre,
            f.Description,
            f.Formateur,
            f.DateDebut,
            f.DateFin,
            f.CapaciteMax,
            f.Inscriptions.Count,
            f.Prix,
            f.Statut,
            f.CreatedAt
        )).ToList();
    }
}