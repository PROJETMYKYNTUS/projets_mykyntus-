using Formation.Domain.Exceptions;
using Formation.Domain.Interfaces;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Formation.Application.Commands.UpdateFormationn;

public record UpdateFormationCommand(
    Guid Id,
    string Titre,
    string Description,
    string Formateur,
    DateTime DateDebut,
    DateTime DateFin,
    int CapaciteMax,
    decimal Prix
) : IRequest;

public class UpdateFormationHandler : IRequestHandler<UpdateFormationCommand>
{
    private readonly IFormationRepository _repo;

    public UpdateFormationHandler(IFormationRepository repo) => _repo = repo;

    public async Task Handle(UpdateFormationCommand cmd, CancellationToken ct)
    {
        var formation = await _repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new FormationNotFoundException(cmd.Id);

        formation.Update(cmd.Titre, cmd.Description, cmd.Formateur,
            cmd.DateDebut, cmd.DateFin, cmd.CapaciteMax, cmd.Prix);

        _repo.Update(formation);
        await _repo.SaveChangesAsync(ct);
    }
}