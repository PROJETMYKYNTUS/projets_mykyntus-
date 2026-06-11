using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Conge.Domain.Exceptions;
using Conge.Domain.Interfaces;
using MediatR;
using System;

namespace Conge.Application.Commands.DemanderConge;

public class DemanderCongeHandler : IRequestHandler<DemanderCongeCommand, Guid>
{
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly ISoldeCongeRepository _soldeRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DemanderCongeHandler(
        IDemandeCongeRepository demandeRepo,
        ISoldeCongeRepository soldeRepo,
        IEmployeSnapshotRepository employeRepo,
        IUnitOfWork unitOfWork)
    {
        _demandeRepo = demandeRepo;
        _soldeRepo = soldeRepo;
        _employeRepo = employeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(DemanderCongeCommand request, CancellationToken ct)
    {
        // 1. Récupérer le snapshot employé
        var employe = await _employeRepo.GetByEmployeIdAsync(request.EmployeId, ct)
            ?? throw new EmployeNotFoundException(request.EmployeId);

        // 2. Vérifier chevauchement
        var chevauchement = await _demandeRepo.ExistsCongeEnChevauchementAsync(
            request.EmployeId, request.DateDebut, request.DateFin, ct);

        if (chevauchement)
            throw new InvalidOperationException("Une demande de congé existe déjà sur cette période.");

        DemandeConge demande;

        if (request.TypeConge == TypeConge.Annuel)
        {
            // 3. Récupérer le solde de l'année en cours
            var solde = await _soldeRepo.GetByEmployeAndAnneeAsync(
                request.EmployeId, DateTime.Today.Year, ct)
                ?? throw new SoldeNotFoundException(request.EmployeId, DateTime.Today.Year);

            // 4. Créer la demande (la logique métier est dans l'entité)
            demande = DemandeConge.CreerCongeAnnuel(
                request.EmployeId,
                employe.ManagerId,
                request.DateDebut,
                request.DateFin,
                solde,
                employe,
                request.Motif);
        }
        else if (request.TypeConge == TypeConge.Exceptionnel)
        {
            if (!request.TypeExceptionnel.HasValue)
                throw new ArgumentException("Le type exceptionnel est obligatoire pour un congé exceptionnel.");

            demande = DemandeConge.CreerCongeExceptionnel(
                request.EmployeId,
                employe.ManagerId,
                request.TypeExceptionnel.Value,
                request.DateDebut,
                request.Motif);
        }
        else
        {
            throw new NotSupportedException($"Type de congé '{request.TypeConge}' non supporté via cette commande.");
        }

        await _demandeRepo.AddAsync(demande, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return demande.Id;
    }
}
