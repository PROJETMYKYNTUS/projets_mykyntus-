using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Commands.RefuserConge;

public class RefuserCongeHandler : IRequestHandler<RefuserCongeCommand, bool>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RefuserCongeHandler(
        IDemandeCongeRepository demandeRepo,
        IUnitOfWork unitOfWork)
    {
        _demandeRepo = demandeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(RefuserCongeCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        // Logique métier dans l'entité (motif obligatoire, statut EnAttente)
        demande.Refuser(request.ManagerId, request.Commentaire);

        _demandeRepo.Update(demande);
        await _unitOfWork.SaveChangesAsync(ct);

        return true;
    }
}
