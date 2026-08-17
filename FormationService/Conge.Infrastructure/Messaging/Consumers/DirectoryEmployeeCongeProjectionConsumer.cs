using Conge.Application.Abstractions;
using Conge.Application.Commands.InitialiserSolde;
using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Conge.Domain.Interfaces;
using Kyntus.Messaging.Contracts;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Messaging.Consumers;

/// <summary>Projection depuis Employee Directory (source canonique employé).</summary>
public sealed class DirectoryEmployeeCongeProjectionConsumer(
    IEmployeSnapshotRepository employeRepo,
    IDemandeCongeRepository demandeRepo,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    IDirectoryOrgCatalog? orgCatalog = null,
    ILogger<DirectoryEmployeeCongeProjectionConsumer>? logger = null) : IConsumer<DirectoryEmployeeChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryEmployeeChangedMessage> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;
        var snapshot = await employeRepo.GetByEmployeIdAsync(msg.EmployeeId, ct);

        if (msg.IsDeleted || !msg.IsActive)
        {
            await HandleInactiveAsync(snapshot, msg.EmployeeId, ct);
            return;
        }

        var managerId = msg.SuperviseurId ?? msg.ParentId ?? Guid.Empty;
        var serviceId = Guid.TryParse(msg.ServiceId, out var parsedSvc) ? parsedSvc : Guid.Empty;
        var orgServiceId = msg.ServiceId;
        var serviceNom = await ResolveServiceNomAsync(orgServiceId, ct);
        var isNew = snapshot is null;

        if (isNew)
        {
            snapshot = EmployeSnapshot.Creer(
                msg.EmployeeId,
                msg.LastName,
                msg.FirstName,
                msg.Email,
                managerId,
                serviceId,
                serviceNom,
                msg.HireDate ?? DateTime.UtcNow,
                false,
                msg.Role,
                msg.PoleId,
                msg.CelluleId,
                orgServiceId,
                msg.BusinessDepartmentId);
            await employeRepo.AddAsync(snapshot, ct);
        }
        else
        {
            if (snapshot!.IsArchived)
                snapshot.Reactiver();

            var previousCellule = snapshot.CelluleId;
            var previousService = snapshot.OrgServiceId;

            snapshot.MettreAJour(
                msg.LastName,
                msg.FirstName,
                msg.Email,
                managerId,
                serviceId,
                serviceNom,
                msg.Role,
                msg.HireDate,
                msg.PoleId,
                msg.CelluleId,
                orgServiceId,
                msg.BusinessDepartmentId);
            employeRepo.Update(snapshot);

            await SyncOpenDemandesValidationNodeAsync(
                snapshot,
                previousCellule,
                previousService,
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        if (isNew)
        {
            var employe = await employeRepo.GetByEmployeIdAsync(msg.EmployeeId, ct);
            var anciennete = employe!.GetAncienneteAnnees();
            await mediator.Send(
                new InitialiserSoldeCommand(
                    msg.EmployeeId,
                    anciennete,
                    employe!.EstMineur,
                    DateTime.UtcNow.Year),
                ct);
        }
    }

    private async Task HandleInactiveAsync(EmployeSnapshot? snapshot, Guid employeId, CancellationToken ct)
    {
        if (snapshot is null) return;

        var demandes = (await demandeRepo.GetByEmployeIdAsync(employeId, ct)).ToList();
        var hasOpen = demandes.Any(d =>
            d.Statut is StatutDemande.EnAttente or StatutDemande.EnAttenteRh);

        if (hasOpen)
        {
            snapshot.Archiver();
            employeRepo.Update(snapshot);
            await unitOfWork.SaveChangesAsync(ct);
            logger?.LogInformation(
                "CONGE employé {Id} archivé (demandes ouvertes) au lieu d’être supprimé",
                employeId);
            return;
        }

        employeRepo.Remove(snapshot);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task SyncOpenDemandesValidationNodeAsync(
        EmployeSnapshot snapshot,
        string? previousCellule,
        string? previousService,
        CancellationToken ct)
    {
        var celluleChanged = !string.Equals(previousCellule, snapshot.CelluleId, StringComparison.OrdinalIgnoreCase);
        var serviceChanged = !string.Equals(previousService, snapshot.OrgServiceId, StringComparison.OrdinalIgnoreCase);
        if (!celluleChanged && !serviceChanged)
            return;

        var (nodeId, level) = ResolveValidationNode(snapshot);
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        var demandes = await demandeRepo.GetByEmployeIdAsync(snapshot.EmployeId, ct);
        var updated = false;
        foreach (var d in demandes.Where(x =>
                     x.Statut is StatutDemande.EnAttente or StatutDemande.EnAttenteRh))
        {
            if (string.Equals(d.ValidationNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                continue;
            d.AssignerNoeudValidation(nodeId, level);
            demandeRepo.Update(d);
            updated = true;
        }

        if (updated)
            logger?.LogInformation(
                "CONGE ValidationNodeId recalculé pour {Email} → {Node}/{Level}",
                snapshot.Email, nodeId, level);
    }

    private async Task<string> ResolveServiceNomAsync(string? orgServiceId, CancellationToken ct)
    {
        var id = QuotaCongeService.NormalizeNodeId(orgServiceId);
        if (id is null) return string.Empty;
        if (orgCatalog is null) return string.Empty;
        try
        {
            var name = await orgCatalog.ResolveNodeNameAsync(id, ct);
            if (!string.IsNullOrWhiteSpace(name)
                && !string.Equals(name, id, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        catch
        {
            /* soft */
        }

        return string.Empty;
    }

    private static (string? NodeId, string? Level) ResolveValidationNode(EmployeSnapshot employe)
    {
        if (!string.IsNullOrWhiteSpace(employe.CelluleId))
            return (employe.CelluleId, "Cellule");
        if (!string.IsNullOrWhiteSpace(employe.OrgServiceId))
            return (employe.OrgServiceId, "Service");
        if (employe.ServiceId != Guid.Empty)
            return (employe.ServiceId.ToString(), "Service");
        return (null, null);
    }
}
