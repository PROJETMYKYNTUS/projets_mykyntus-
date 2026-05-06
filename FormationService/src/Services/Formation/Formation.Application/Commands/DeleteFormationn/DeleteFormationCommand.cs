using System;
using System.Threading;
using System.Threading.Tasks;
using Formation.Domain.Interfaces;
using Formation.Domain.Exceptions;
using MediatR;

namespace Formation.Application.Commands.DeleteFormationn;

public record DeleteFormationCommand(Guid Id) : IRequest;

public class DeleteFormationHandler : IRequestHandler<DeleteFormationCommand>
{
    private readonly IFormationRepository _repo;
    public DeleteFormationHandler(IFormationRepository repo) => _repo = repo;

    public async Task Handle(DeleteFormationCommand cmd, CancellationToken ct)
    {
        var formation = await _repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new FormationNotFoundException(cmd.Id);
        _repo.Delete(formation);
        await _repo.SaveChangesAsync(ct);
    }
}