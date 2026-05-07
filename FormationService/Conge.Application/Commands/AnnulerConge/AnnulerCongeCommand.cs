using MediatR;

namespace Conge.Application.Commands.AnnulerConge;

public record AnnulerCongeCommand(
    Guid DemandeId,
    Guid EmployeId  // Pour vérifier que c'est bien le propriétaire qui annule
) : IRequest<bool>;