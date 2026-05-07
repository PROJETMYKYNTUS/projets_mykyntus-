using Conge.Application.DTOs;
using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetSoldeByEmploye;

public record GetSoldeByEmployeQuery(
    Guid EmployeId,
    int? Annee = null
) : IRequest<SoldeCongeDto>;

public class GetSoldeByEmployeHandler : IRequestHandler<GetSoldeByEmployeQuery, SoldeCongeDto>
{
    private readonly ISoldeCongeRepository _repo;

    public GetSoldeByEmployeHandler(ISoldeCongeRepository repo)
        => _repo = repo;

    public async Task<SoldeCongeDto> Handle(GetSoldeByEmployeQuery request, CancellationToken ct)
    {
        var annee = request.Annee ?? DateTime.Today.Year;

        var solde = await _repo.GetByEmployeAndAnneeAsync(request.EmployeId, annee, ct)
            ?? throw new SoldeNotFoundException(request.EmployeId, annee);

        return new SoldeCongeDto(
            solde.EmployeId,
            solde.Annee,
            solde.SoldeInitial,
            solde.SoldeUtilise,
            solde.SoldeRestant
        );
    }
}