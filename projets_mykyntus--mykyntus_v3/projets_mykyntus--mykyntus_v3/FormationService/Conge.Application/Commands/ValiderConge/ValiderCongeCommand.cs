using MediatR;

namespace Conge.Application.Commands.ValiderConge;

public record ValiderCongeCommand(
    Guid DemandeId,
    Guid ManagerId,
    string? Commentaire = null
) : IRequest<bool>;