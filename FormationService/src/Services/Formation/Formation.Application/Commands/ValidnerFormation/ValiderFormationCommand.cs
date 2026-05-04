using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using System.Threading;
using Formation.Domain.Interfaces;
using Formation.Domain.Exceptions;
namespace Formation.Application.Commands.ValidnerFormation;
public record ValiderFormationCommand(Guid Id) : IRequest;

public class ValiderFormationHandler : IRequestHandler<ValiderFormationCommand>
{
    private readonly IFormationRepository _repo;
    public ValiderFormationHandler(IFormationRepository repo) => _repo = repo;

    public async Task Handle(ValiderFormationCommand cmd, CancellationToken ct)
    {
        var formation = await _repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new FormationNotFoundException(cmd.Id);

        var result = formation.Valider();
        if (result.IsFailure) throw new InvalidOperationException(result.Error);

        _repo.Update(formation);
        await _repo.SaveChangesAsync(ct);
    }
}