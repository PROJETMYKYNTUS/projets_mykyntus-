using Conge.Application.DTOs;
using MediatR;

namespace Conge.Application.Queries.GetDemandesByManager;

public record GetDemandesByManagerQuery(
    Guid ManagerId
) : IRequest<IEnumerable<DemandeCongeDto>>;