using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class ServicePrimeIndicatorsAppService(PrimeDbContext db, PrimeOrgScopeService org)
    : IServicePrimeIndicatorsAppService
{
    private static ServicePrimeIndicatorDto Map(ServicePrimeIndicatorEntity e) =>
        new()
        {
            Id = e.Id,
            ServiceId = e.ServiceId,
            SortOrder = e.SortOrder,
            Label = e.Label,
            PonderationPrimePct = e.PonderationPrimePct,
            PonderationChallengePct = e.PonderationChallengePct,
            IsActive = e.IsActive,
            TemplateStableId = e.TemplateStableId,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
        };

    public async Task<IReadOnlyList<ServicePrimeIndicatorDto>> GetAsync(
        string serviceId,
        string supervisorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("supervisorUserId requis.");

        var celluleId = await org.GetCelluleIdForServiceAsync(serviceId, ct)
            ?? throw new KeyNotFoundException("Cellule introuvable.");
        if (!await org.SupervisorOwnsCelluleAsync(supervisorUserId, celluleId, ct))
            throw new UnauthorizedAccessException("Accès refusé pour ce périmètre.");

        var list = await db.ServicePrimeIndicators.AsNoTracking()
            .Where(x => x.ServiceId == serviceId.Trim())
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
        return list.ConvertAll(Map);
    }

    public async Task<IReadOnlyList<ServicePrimeIndicatorDto>> PutAsync(
        string serviceId,
        string supervisorUserId,
        PutServicePrimeIndicatorsRequest body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("supervisorUserId requis.");

        var celluleId = await org.GetCelluleIdForServiceAsync(serviceId, ct)
            ?? throw new KeyNotFoundException("Cellule introuvable.");
        if (!await org.SupervisorOwnsCelluleAsync(supervisorUserId, celluleId, ct))
            throw new UnauthorizedAccessException("Accès refusé pour ce périmètre.");

        var cid = serviceId.Trim();
        var now = DateTimeOffset.UtcNow;
        var existing = await db.ServicePrimeIndicators.Where(x => x.ServiceId == cid).ToListAsync(ct);
        db.ServicePrimeIndicators.RemoveRange(existing);

        foreach (var item in body.Indicators.OrderBy(i => i.SortOrder))
        {
            db.ServicePrimeIndicators.Add(new ServicePrimeIndicatorEntity
            {
                Id = Guid.NewGuid(),
                ServiceId = cid,
                SortOrder = item.SortOrder,
                Label = item.Label.Trim(),
                PonderationPrimePct = item.PonderationPrimePct,
                PonderationChallengePct = item.PonderationChallengePct,
                IsActive = item.IsActive,
                TemplateStableId = string.IsNullOrWhiteSpace(item.TemplateStableId) ? null : item.TemplateStableId.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        var list = await db.ServicePrimeIndicators.AsNoTracking()
            .Where(x => x.ServiceId == cid)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
        return list.ConvertAll(Map);
    }
}

public sealed class PrimePeriodRecapReportsAppService(PrimeDbContext db) : IPrimePeriodRecapReportsAppService
{
    public async Task<FileExportResultDto> DownloadPeriodRecapAsync(
        string period,
        string actingUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(period))
            throw new ArgumentException("period est requis.");
        if (string.IsNullOrWhiteSpace(actingUserId))
            throw new ArgumentException("actingUserId est requis.");

        var uid = actingUserId.Trim();
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        var role = e?.Role;
        var allow = string.Equals(role, "Admin", StringComparison.Ordinal) ||
                    string.Equals(role, "RH", StringComparison.Ordinal) ||
                    string.Equals(role, "Manager", StringComparison.Ordinal) ||
                    string.Equals(role, "Comptable", StringComparison.Ordinal);
        if (!allow)
            throw new PrimeApiException(403, "Rôle non autorisé (Admin, RH, Manager ou Comptable).");

        var bytes = await PrimeGlobalRecapExcelBuilder.BuildAsync(db, period.Trim(), ct);
        return new FileExportResultDto(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"PRIME_synthese_globale_{period.Trim()}.xlsx");
    }
}

public sealed class PrimeFicheImportAppService(PrimeFicheImportService import) : IPrimeFicheImportAppService
{
    public Task<(bool Ok, string? Error, ImportReadyFicheResponseDto? Result)> ImportAsync(
        ImportReadyFicheRequest body,
        CancellationToken ct = default) =>
        import.ImportReadyFicheAsync(body, ct);

    public async Task<IReadOnlyList<PrimeHistoricalFicheListItemDto>> ListHistoricalAsync(
        string supervisorUserId,
        string? period,
        string? role,
        CancellationToken ct = default) =>
        await import.ListHistoricalAsync(supervisorUserId, period, role, ct);

    public Task<(bool Ok, string? Error, PrimeHistoricalFicheDetailSnapshotDto? Result)> GetHistoricalDetailSnapshotAsync(
        Guid id,
        string supervisorUserId,
        string? role,
        CancellationToken ct = default) =>
        import.GetHistoricalDetailSnapshotAsync(id, supervisorUserId, role, ct);
}

public sealed class PrimeCoreQueryAppService(PrimeDbContext? db, PrimeOrgScopeService org) : IPrimeCoreQueryAppService
{
    public async Task<PrimeHealthStatusDto> GetHealthAsync(CancellationToken ct = default)
    {
        if (db is null)
            return new PrimeHealthStatusDto("ok", "memory-only", null, null);
        try
        {
            var ok = await db.Database.CanConnectAsync(ct);
            return ok
                ? new PrimeHealthStatusDto("ok", null, "prime_db", null)
                : new PrimeHealthStatusDto("db-unreachable", null, null, null);
        }
        catch (Exception ex)
        {
            return new PrimeHealthStatusDto("db-error", null, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<Department>> GetLegacyDepartmentsAsync(CancellationToken ct) =>
        await org.GetLegacyDepartmentTreeAsync(ct);

    public Task<OperationalOrgTreeDto> GetOperationalDepartmentsAsync(CancellationToken ct) =>
        org.GetOperationalOrgTreeAsync(ct);

    public async Task<IReadOnlyList<Employee>> GetLegacyEmployeesAsync(CancellationToken ct) =>
        await org.GetLegacyEmployeesAsync(ct);

    public async Task<IReadOnlyList<PrimeResult>> GetPrimeResultsAsync(CancellationToken ct) =>
        await org.GetPrimeResultsFromFichesAsync(500, ct);

    public async Task<IReadOnlyList<PrimeResult>> GetMyPrimeResultsAsync(string employeeId, CancellationToken ct)
    {
        var list = await org.GetPrimeResultsFromFichesAsync(500, ct);
        return list.Where(r => r.EmployeeId == employeeId.Trim()).ToList();
    }

    public Task<object> GetDashboardStatsAsync(CancellationToken ct) =>
        org.BuildDashboardStatsAsync(ct);
}

public sealed class AllowanceQueryAppService(PrimeDbContext db, AllowanceScopeService scope) : IAllowanceQueryAppService
{
    public async Task<IReadOnlyList<BusinessDepartmentMirrorDto>> ListBusinessDepartmentsAsync(CancellationToken ct = default)
    {
        var rows = await db.BusinessDepartments.AsNoTracking()
            .Include(d => d.PoleAssignments)
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
        return rows.Select(d => new BusinessDepartmentMirrorDto(
            d.Id, d.Code, d.Name, d.Kind, d.ManagerEmployeeId, d.IsActive,
            d.PoleAssignments.Select(p => p.PoleId).ToList())).ToList();
    }

    public async Task<AllowanceUserContextDto> GetMyContextAsync(string userId, string role, CancellationToken ct = default)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == userId, ct);
        var managedDept = await db.BusinessDepartments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ManagerEmployeeId == userId && d.IsActive, ct);
        var managedIds = await scope.GetDirectReportIdsAsync(userId, ct);
        var directReportCount = managedIds.Count;
        return new AllowanceUserContextDto(
            userId,
            role,
            emp?.BusinessDepartmentId,
            emp?.BusinessDepartmentKind,
            managedDept?.Kind == "Support",
            managedDept?.Kind == "Operational",
            managedDept?.Id,
            managedDept?.Kind,
            managedDept?.Name,
            managedDept?.Code,
            directReportCount);
    }

    public async Task<IReadOnlyList<object>> GetTeamAsync(string userId, CancellationToken ct = default)
    {
        if (!await scope.IsSupportDepartmentManagerAsync(userId, ct))
            throw new UnauthorizedAccessException();
        var deptId = await scope.GetManagerDepartmentIdAsync(userId, ct);
        var managedIds = await scope.GetDirectReportIdsAsync(userId, ct);
        if (managedIds.Count == 0) return [];

        var query = db.Employees.AsNoTracking()
            .Where(e => managedIds.Contains(e.Id) && e.BusinessDepartmentKind == "Support");
        if (!string.IsNullOrWhiteSpace(deptId))
            query = query.Where(e => e.BusinessDepartmentId == deptId);
        var team = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new { id = e.Id, firstName = e.FirstName, lastName = e.LastName, email = e.Email })
            .ToListAsync(ct);
        return team.Cast<object>().ToList();
    }
}
