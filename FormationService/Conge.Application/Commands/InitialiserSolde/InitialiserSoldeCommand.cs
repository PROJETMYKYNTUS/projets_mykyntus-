using MediatR;

namespace Conge.Application.Commands.InitialiserSolde;

public record InitialiserSoldeCommand(
    Guid EmployeId,
    int AncienneteAnnees,
    bool EstMineur,
    int Annee
) : IRequest<bool>;
