using Conge.Application.Contracts;
using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Conge.Domain.Events;
using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Commands.DemanderConge;

public class DemanderCongeHandler : IRequestHandler<DemanderCongeCommand, Guid>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly ISoldeCongeRepository _soldeRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICongeEventPublisher _eventPublisher;


    public DemanderCongeHandler(
        IDemandeCongeRepository demandeRepo,
        ISoldeCongeRepository soldeRepo,
        IEmployeSnapshotRepository employeRepo,
        IUnitOfWork unitOfWork,
          ICongeEventPublisher eventPublisher)
        
    {
        _demandeRepo = demandeRepo;
        _soldeRepo = soldeRepo;
        _employeRepo = employeRepo;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
    }

    public async Task<Guid> Handle(DemanderCongeCommand request, CancellationToken ct)
    {
        // 1. Récupérer le snapshot employé
        var employe = await _employeRepo.GetByEmployeIdAsync(request.EmployeId, ct)
            ?? throw new EmployeNotFoundException(request.EmployeId);

        // 2. Déterminer le validateur selon le rôle
        // ✅ Manager → valide par Admin/RH
        // ✅ Employé → valide par son Manager
        Guid validateurId;

        if (employe.Role == "Manager")
        {
            var adminRh = await _employeRepo.GetAdminOuRhAsync(ct)
                ?? throw new EmployeNotFoundException(Guid.Empty);
            validateurId = adminRh.EmployeId;
        }
        else
        {
            validateurId = employe.ManagerId;
        }

        // 3. Vérifier chevauchement
        var chevauchement = await _demandeRepo.ExistsCongeEnChevauchementAsync(
            request.EmployeId, request.DateDebut, request.DateFin, ct);
        if (chevauchement)
            throw new InvalidOperationException(
                "Une demande de congé existe déjà sur cette période.");

        // 4. Créer la demande selon le type
        DemandeConge demande;

        if (request.TypeConge == TypeConge.Annuel)
        {
            var solde = await _soldeRepo.GetByEmployeAndAnneeAsync(
                request.EmployeId, DateTime.Today.Year, ct)
                ?? throw new SoldeNotFoundException(request.EmployeId, DateTime.Today.Year);

            demande = DemandeConge.CreerCongeAnnuel(
                request.EmployeId,
                validateurId,        // ← validateur dynamique
                request.DateDebut,
                request.DateFin,
                solde,
                employe,
                request.Motif);
        }
        else if (request.TypeConge == TypeConge.Exceptionnel)
        {
            if (!request.TypeExceptionnel.HasValue)
                throw new ArgumentException(
                    "Le type exceptionnel est obligatoire pour un congé exceptionnel.");

            demande = DemandeConge.CreerCongeExceptionnel(
                request.EmployeId,
                validateurId,        // ← validateur dynamique
                request.TypeExceptionnel.Value,
                request.DateDebut,
                request.Motif);
        }
        else
        {
            throw new NotSupportedException(
                $"Type de congé '{request.TypeConge}' non supporté via cette commande.");
        }

        await _demandeRepo.AddAsync(demande, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        foreach (var domainEvent in demande.DomainEvents)
        {
            if (domainEvent is CongeDemandeEvent e)
                await _eventPublisher.PublishCongeDemandeAsync(
                    demande.EmployeId,
                    demande.Id,
                    demande.ManagerId,
                    demande.DateDebut,
                    demande.DateFin,
                    ct);
        }
        demande.ClearDomainEvents();
        return demande.Id;
    }
}