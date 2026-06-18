using Conge.Domain.Enums;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Queries.GetPendingCongesRhCount;

public record GetPendingCongesRhCountQuery : IRequest<int>;

public class GetPendingCongesRhCountHandler : IRequestHandler<GetPendingCongesRhCountQuery, int>
{
    private readonly IDemandeCongeRepository _demandeRepo;

    public GetPendingCongesRhCountHandler(IDemandeCongeRepository demandeRepo)
    {
        _demandeRepo = demandeRepo;
    }

    public async Task<int> Handle(GetPendingCongesRhCountQuery request, CancellationToken ct)
    {
        var pending = await _demandeRepo.GetByStatutAsync(StatutDemande.EnAttente, ct);
        return pending.Count();
    }
}
