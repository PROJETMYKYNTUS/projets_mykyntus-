using DocumentationBackend.Data;
using DocumentationBackend.Data.Entities;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DocumentationBackend.Services;

public sealed class DirectoryUserSyncService(
    DocumentationDbContext db,
    IConfiguration configuration,
    ILogger<DirectoryUserSyncService> logger)
{
    public async Task UpsertFromEmployeMessageAsync(EmployeCreatedMessage msg, CancellationToken ct)
    {
        var tenantId = configuration["Documentation:DefaultTenantId"] ?? "atlas-tech-demo";
        var email = msg.Email.Trim().ToLowerInvariant();
        var roleName = KyntusPortalRoleMapping.ToDocumentationRoleName(msg.Role);
        if (!Enum.TryParse<AppRole>(roleName, ignoreCase: true, out var appRole))
            appRole = AppRole.Pilote;

        var defaultPole = ParseGuid(configuration["Documentation:Sync:DefaultPoleId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01");
        var defaultCellule = ParseGuid(configuration["Documentation:Sync:DefaultCelluleId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02");
        var defaultDept = ParseGuid(configuration["Documentation:Sync:DefaultDepartementId"], "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03");

        Guid poleId = defaultPole;
        Guid celluleId = defaultCellule;
        Guid departementId = defaultDept;

        if (!string.IsNullOrWhiteSpace(msg.PrimeServiceId))
        {
            departementId = KyntusStableGuid.FromPrimeOrgId(msg.PrimeServiceId);
            var deptUnit = await db.OrganisationUnits.AsNoTracking()
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == departementId, ct);
            if (deptUnit?.ParentId is Guid cellId)
            {
                celluleId = cellId;
                var cellUnit = await db.OrganisationUnits.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == cellId, ct);
                if (cellUnit?.ParentId is Guid pId)
                    poleId = pId;
            }
        }

        Guid? managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : null;
        Guid? coachId = null;
        Guid? rpId = null;

        if (appRole == AppRole.Pilote)
            managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : managerId;
        else if (appRole == AppRole.Coach)
            managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : null;
        else if (appRole == AppRole.Manager)
            rpId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : null;

        var now = DateTimeOffset.UtcNow;
        var row = await db.DirectoryUsers.FirstOrDefaultAsync(
            u => u.TenantId == tenantId && u.Email.ToLower() == email, ct);

        if (row is null)
        {
            db.DirectoryUsers.Add(new DirectoryUser
            {
                Id = msg.EmployeId,
                TenantId = tenantId,
                Prenom = msg.Prenom,
                Nom = msg.Nom,
                Email = email,
                Role = appRole,
                ManagerId = managerId,
                CoachId = coachId,
                RpId = rpId,
                PoleId = poleId,
                CelluleId = celluleId,
                DepartementId = departementId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            row.Id = msg.EmployeId;
            row.Prenom = msg.Prenom;
            row.Nom = msg.Nom;
            row.Email = email;
            row.Role = appRole;
            row.ManagerId = managerId;
            row.CoachId = coachId;
            row.RpId = rpId;
            row.PoleId = poleId;
            row.CelluleId = celluleId;
            row.DepartementId = departementId;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("DOCUMENTATION directory_users sync {Email} rôle={Role}", email, appRole);
    }

    public async Task UpsertOrganisationUnitAsync(OrgNodeCreatedMessage msg, CancellationToken ct)
    {
        var tenantId = configuration["Documentation:DefaultTenantId"] ?? "atlas-tech-demo";
        var id = KyntusStableGuid.FromPrimeOrgId(msg.NodeId);
        var unitType = msg.Level switch
        {
            OrgNodeLevel.Pole => "pole",
            OrgNodeLevel.Cellule => "cellule",
            OrgNodeLevel.Service => "departement",
            _ => "departement"
        };
        Guid? parentId = string.IsNullOrWhiteSpace(msg.ParentNodeId)
            ? null
            : KyntusStableGuid.FromPrimeOrgId(msg.ParentNodeId);

        var now = DateTimeOffset.UtcNow;
        var row = await db.OrganisationUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (row is null)
        {
            db.OrganisationUnits.Add(new OrganisationUnit
            {
                Id = id,
                TenantId = tenantId,
                ParentId = parentId,
                UnitType = unitType,
                Code = string.IsNullOrWhiteSpace(msg.Code) ? msg.NodeId : msg.Code,
                Name = msg.Name,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            row.Name = msg.Name;
            row.ParentId = parentId;
            row.UnitType = unitType;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("DOCUMENTATION organisation_units sync {Type} {Name}", unitType, msg.Name);
    }

    private static Guid ParseGuid(string? value, string fallback) =>
        Guid.TryParse(value, out var g) ? g : Guid.Parse(fallback);
}
