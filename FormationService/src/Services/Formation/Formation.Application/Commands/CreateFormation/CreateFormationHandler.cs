using System;
using System.Threading;
using System.Threading.Tasks;
using Formation.Domain.Interfaces;
using Formation.Domain.Entities;
using MediatR;

namespace Formation.Application.Commands.CreateFormation;

public class CreateFormationHandler : IRequestHandler<CreateFormationCommand, Guid>
{
    private readonly IFormationRepository _repo;
    public CreateFormationHandler(IFormationRepository repo) => _repo = repo;

    public async Task<Guid> Handle(CreateFormationCommand cmd, CancellationToken ct)
    {
        var formation = FormationEntity.Create(  // ← plus de conflit de nom
            cmd.Titre, cmd.Description, cmd.Formateur,
            cmd.DateDebut, cmd.DateFin, cmd.CapaciteMax, cmd.Prix);
        await _repo.AddAsync(formation, ct);
        await _repo.SaveChangesAsync(ct);
        return formation.Id;
    }
}