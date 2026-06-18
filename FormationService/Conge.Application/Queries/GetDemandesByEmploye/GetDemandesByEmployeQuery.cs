using Conge.Application.DTOs;
using Conge.Domain.Enums;
using MediatR;

namespace Conge.Application.Queries.GetDemandesByEmploye;

public record GetDemandesByEmployeQuery(
    Guid EmployeId,
    StatutDemande? Statut = null
) : IRequest<IEnumerable<DemandeCongeDto>>;