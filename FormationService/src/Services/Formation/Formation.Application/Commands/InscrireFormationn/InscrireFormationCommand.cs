using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using System.Threading;
using Formation.Domain.Interfaces;
using Formation.Domain.Exceptions;
namespace Formation.Application.Commands.InscrireFormationn;

public record InscrireFormationCommand(Guid FormationId, Guid EmployeId, string NomEmploye) : IRequest<Guid>;

public class InscrireFormationHandler : IRequestHandler<InscrireFormationCommand, Guid>
{
    private readonly IFormationRepository _repo;
    public InscrireFormationHandler(IFormationRepository repo) => _repo = repo;

    public async Task<Guid> Handle(InscrireFormationCommand cmd, CancellationToken ct)
    {
        var formation = await _repo.GetByIdAsync(cmd.FormationId, ct)
            ?? throw new FormationNotFoundException(cmd.FormationId);

        var result = formation.Inscrire(cmd.EmployeId, cmd.NomEmploye);
        if (result.IsFailure) throw new InvalidOperationException(result.Error);

        await _repo.AddInscriptionAsync(result.Value, ct); // ← INSERT explicite
        await _repo.SaveChangesAsync(ct);
        return result.Value.Id;
    }
}
