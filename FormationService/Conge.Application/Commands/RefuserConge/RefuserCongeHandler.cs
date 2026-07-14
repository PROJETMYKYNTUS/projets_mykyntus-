using Conge.Application.Contracts;
using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Commands.RefuserConge;

public class RefuserCongeHandler : IRequestHandler<RefuserCongeCommand, bool>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICongeEventPublisher _publisher;

    public RefuserCongeHandler(
        IDemandeCongeRepository demandeRepo,
        IUnitOfWork unitOfWork,
        ICongeEventPublisher publisher)
    {
        _demandeRepo = demandeRepo;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<bool> Handle(RefuserCongeCommand request, CancellationToken ct)
    {
        var demande = await _demandeRepo.GetByIdAsync(request.DemandeId, ct)
            ?? throw new CongeNotFoundException(request.DemandeId);

        demande.Refuser(request.ManagerId, request.Commentaire);

        _demandeRepo.Update(demande);
        await _unitOfWork.SaveChangesAsync(ct);

        await _publisher.PublishCongeRefuseAsync(
            demande.EmployeId,
            demande.Id,
            request.Commentaire,
            ct);

        return true;
    }
}
