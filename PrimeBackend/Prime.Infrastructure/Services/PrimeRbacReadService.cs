using Microsoft.EntityFrameworkCore;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class PrimeRbacReadService(PrimeDbContext db, PrimeOrgScopeService org)
{
    public async Task<bool> RoleHasActionAsync(string role, string action, CancellationToken ct = default)
    {
        var r = role.Trim();
        var a = action.Trim();
        return await db.RbacPermissions.AsNoTracking()
            .AnyAsync(p => p.Role == r && p.Action == a && p.IsAllowed, ct);
    }

    public async Task<List<string>> GetDistinctRolesAsync(CancellationToken ct = default) =>
        await db.Employees.AsNoTracking()
            .Select(e => e.Role)
            .Where(r => r != null && r != "")
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync(ct);

    public async Task<List<string>> GetAllowedScopesAsync(string role, string action, CancellationToken ct = default) =>
        await db.RbacPermissions.AsNoTracking()
            .Where(p => p.Role == role.Trim() && p.Action == action.Trim() && p.IsAllowed)
            .Select(p => p.Scope)
            .Distinct()
            .ToListAsync(ct);

    /// <summary>Correspondance rôle employé ↔ rôle valideur d’une étape workflow (RP → Chef de projet, etc.).</summary>
    public static bool RolesMatchWorkflowApprover(string employeeRole, string stepApproverRole)
    {
        if (string.Equals(employeeRole, stepApproverRole, StringComparison.Ordinal)) return true;
        if (string.Equals(employeeRole, "RP", StringComparison.Ordinal) &&
            string.Equals(stepApproverRole, "Chef de projet", StringComparison.Ordinal))
            return true;
        if (string.Equals(employeeRole, "Coach", StringComparison.Ordinal) &&
            string.Equals(stepApproverRole, PrimeFicheValidationRoles.ReferentTechnique, StringComparison.Ordinal))
            return true;
        if (string.Equals(employeeRole, PrimeFicheValidationRoles.ReferentTechnique, StringComparison.Ordinal) &&
            string.Equals(stepApproverRole, "Coach", StringComparison.Ordinal))
            return true;
        return IPrimeRequestUserResolver.RolesMatch(employeeRole, stepApproverRole);
    }

    /// <summary>Vrai si une étape active attend ce rôle sur le statut courant de la fiche.</summary>
    public async Task<bool> IsWorkflowValidationTurnAsync(
        Employee actor,
        EmployeePrimeServiceFiche fiche,
        CancellationToken ct)
    {
        var steps = await db.WorkflowSteps.AsNoTracking()
            .Where(s => s.IsActive && s.FromStatus == fiche.ValidationStatus)
            .ToListAsync(ct);
        return steps.Any(s => RolesMatchWorkflowApprover(actor.Role, s.ApproverRole));
    }

    private static bool IsReferentTechniqueRole(string role) =>
        string.Equals(role, PrimeFicheValidationRoles.ReferentTechnique, StringComparison.Ordinal) ||
        string.Equals(role, "Coach", StringComparison.Ordinal);

    /// <summary>Pilote dans le périmètre du référent technique (hiérarchie RH + cellule).</summary>
    public Task<bool> IsPiloteUnderReferentAsync(
        Employee referent,
        EmployeePrimeServiceFiche fiche,
        CancellationToken ct = default) =>
        org.IsPilotInReferentValidationScopeAsync(referent.Id, fiche.EmployeeId, referent.Role, ct);

    /// <summary>Clone employé avec le rôle d’action UI (validation en mode changement de rôle).</summary>
    public static Employee WithActingRole(Employee source, string actingRole) =>
        new()
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            Role = actingRole.Trim(),
            ParentId = source.ParentId,
            ServiceId = source.ServiceId,
            CelluleId = source.CelluleId,
            PoleId = source.PoleId,
            Email = source.Email,
            Avatar = source.Avatar,
            BusinessDepartmentId = source.BusinessDepartmentId,
            BusinessDepartmentKind = source.BusinessDepartmentKind,
        };

    public async Task<bool> CanAccessFicheAsync(Employee actor, EmployeePrimeServiceFiche fiche, string action, CancellationToken ct)
    {
        if (!await RoleHasActionAsync(actor.Role, action, ct)) return false;

        if (IsReferentTechniqueRole(actor.Role))
        {
            if (!await IsPiloteUnderReferentAsync(actor, fiche, ct))
                return false;
            if (string.Equals(action, "Read", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(action, "Validate", StringComparison.OrdinalIgnoreCase) &&
                await IsWorkflowValidationTurnAsync(actor, fiche, ct))
                return true;
            return false;
        }

        // Tour de validation workflow : le graphe (FromStatus + ApproverRole) prime sur le périmètre RBAC
        // (ex. RH valide en Global puis Chef de projet doit voir les fiches « RH Approved »).
        if (string.Equals(action, "Validate", StringComparison.OrdinalIgnoreCase) &&
            PrimeFicheValidationRoles.IsOperationalApprover(actor.Role) &&
            await IsWorkflowValidationTurnAsync(actor, fiche, ct))
            return true;

        var scopes = await GetAllowedScopesAsync(actor.Role, action, ct);
        foreach (var raw in scopes)
        {
            var scope = raw.Trim();
            if (scope == "Global") return true;
            if (scope == "Self" && string.Equals(fiche.EmployeeId, actor.Id, StringComparison.Ordinal)) return true;
            if (scope == "Service" && string.Equals(fiche.ServiceId, actor.ServiceId, StringComparison.Ordinal)) return true;
            if (scope == "Cellule" && string.Equals(fiche.CelluleId, actor.CelluleId, StringComparison.Ordinal)) return true;
            if (scope == "Pole")
            {
                var poleId = await db.Cellules.AsNoTracking()
                    .Where(c => c.Id == fiche.CelluleId)
                    .Select(c => c.PoleId)
                    .FirstOrDefaultAsync(ct);
                if (!string.IsNullOrEmpty(poleId) && string.Equals(poleId, actor.PoleId, StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }
}
