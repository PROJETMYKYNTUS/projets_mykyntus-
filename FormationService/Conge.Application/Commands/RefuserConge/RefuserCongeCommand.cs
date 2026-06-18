using MediatR;

namespace Conge.Application.Commands.RefuserConge;

public record RefuserCongeCommand(
    Guid DemandeId,
    Guid ManagerId,
    string Commentaire
) : IRequest<bool>;
