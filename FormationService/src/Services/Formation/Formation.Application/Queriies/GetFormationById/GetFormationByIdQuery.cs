using System;
using System.Threading;
using System.Threading.Tasks;
using Formation.Application.DTOs;
using Formation.Domain.Interfaces;
using Formation.Domain.Exceptions;
using MediatR;

namespace Formation.Application.Queriies.GetFormationById;

public record GetFormationByIdQuery(Guid Id) : IRequest<FormationDto>;

public class GetFormationByIdHandler : IRequestHandler<GetFormationByIdQuery, FormationDto>
{
    private readonly IFormationRepository _repo;
    public GetFormationByIdHandler(IFormationRepository repo) => _repo = repo;

    public async Task<FormationDto> Handle(GetFormationByIdQuery query, CancellationToken ct)
    {
        var f = await _repo.GetByIdAsync(query.Id, ct)
            ?? throw new FormationNotFoundException(query.Id);
        return new FormationDto(f.Id, f.Titre, f.Description, f.Formateur,
            f.DateDebut, f.DateFin, f.CapaciteMax,
            f.Inscriptions.Count, f.Prix, f.Statut, f.CreatedAt);
    }
}