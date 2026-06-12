using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;

namespace PrimeBackend.Messaging;

public sealed class EmployeSyncConsumer(
    PrimeDbContext db,
    PrimeInMemoryStore store,
    ILogger<EmployeSyncConsumer> logger) :
    IConsumer<EmployeCreatedMessage>,
    IConsumer<EmployeUpdatedMessage>
{
    public async Task Consume(ConsumeContext<EmployeCreatedMessage> context)
    {
        await UpsertEmployeeAsync(context.Message.EmployeId, context.Message, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<EmployeUpdatedMessage> context)
    {
        var msg = context.Message;
        await UpsertEmployeeAsync(msg.EmployeId, new EmployeCreatedMessage
        {
            EmployeId = msg.EmployeId,
            Nom = msg.Nom,
            Prenom = msg.Prenom,
            Email = msg.Email,
            ManagerId = msg.ManagerId,
            ServiceId = msg.ServiceId,
            ServiceNom = msg.ServiceNom,
            DateEmbauche = DateTime.UtcNow,
            EstMineur = false,
            Role = msg.Role,
            SubServiceId = msg.SubServiceId,
            PrimeServiceId = msg.PrimeServiceId,
            SupervisorId = msg.SupervisorId
        }, context.CancellationToken);
    }

    private async Task UpsertEmployeeAsync(Guid employeGuid, EmployeCreatedMessage msg, CancellationToken ct)
    {
        var id = employeGuid.ToString();
        var role = KyntusRoleNames.NormalizePlanningRole(msg.Role);
        if (string.Equals(role, KyntusRoleNames.Manager, StringComparison.OrdinalIgnoreCase))
            role = KyntusRoleNames.Superviseur;

        var existing = await db.Employees.FirstOrDefaultAsync(e => e.Id == id || e.Email == msg.Email, ct);
        string? serviceId = msg.PrimeServiceId;
        string? celluleId = null;
        string? poleId = null;

        if (!string.IsNullOrWhiteSpace(serviceId))
        {
            var svc = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId, ct);
            if (svc is not null)
            {
                celluleId = svc.CelluleId;
                var cell = await db.Cellules.AsNoTracking().FirstOrDefaultAsync(c => c.Id == celluleId, ct);
                poleId = cell?.PoleId;
            }
        }

        if (existing is null)
        {
            db.Employees.Add(new EmployeeEntity
            {
                Id = id,
                Email = msg.Email.Trim(),
                FirstName = msg.Prenom,
                LastName = msg.Nom,
                Role = role,
                ServiceId = serviceId,
                CelluleId = celluleId,
                PoleId = poleId ?? "",
                ParentId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId.ToString() : null
            });
        }
        else
        {
            existing.Email = msg.Email.Trim();
            existing.FirstName = msg.Prenom;
            existing.LastName = msg.Nom;
            existing.Role = role;
            existing.ServiceId = serviceId ?? existing.ServiceId;
            existing.CelluleId = celluleId ?? existing.CelluleId;
            existing.PoleId = poleId ?? existing.PoleId;
            if (msg.SupervisorId != Guid.Empty)
                existing.ParentId = msg.SupervisorId.ToString();
        }

        await db.SaveChangesAsync(ct);
        store.HydrateOrganizationFromDatabase(db);
        logger.LogInformation("prime_employee synchronisé {Email} rôle={Role}", msg.Email, role);
    }
}
