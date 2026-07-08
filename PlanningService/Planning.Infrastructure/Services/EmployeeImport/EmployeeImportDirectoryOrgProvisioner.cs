using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services.EmployeeImport;

/// <summary>
/// Crée les nœuds org dans l'Employee Directory puis synchronise le miroir Planning.
/// </summary>
public sealed class EmployeeImportDirectoryOrgProvisioner(
    AppDbContext db,
    IDirectoryOrgWriteClient directoryOrg,
    IPlanningOrgMirrorService orgMirror,
    IEmployeeImportOrgResolver orgResolver,
    IHttpContextAccessor httpContextAccessor,
    ILogger<EmployeeImportDirectoryOrgProvisioner> logger) : IEmployeeImportOrgProvisioner
{
    private readonly Dictionary<string, string> _poleDirectoryIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _celluleDirectoryIds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<DirectoryOperationalDepartmentJson>? _operationalDepartments;

    public async Task<IReadOnlyList<OrgNodeCreatedReportDto>> ProvisionAsync(
        IReadOnlyList<PendingOrgCreationDto> approved,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct = default)
    {
        if (approved.Count == 0)
            return [];

        var created = new List<OrgNodeCreatedReportDto>();
        _poleDirectoryIds.Clear();
        _celluleDirectoryIds.Clear();
        _operationalDepartments = await directoryOrg.GetOperationalDepartmentsAsync(ct);

        foreach (var item in approved.OrderBy(a => OrgOrder(a.Type)))
        {
            switch (item.Type)
            {
                case "pole":
                    await EnsureDirectoryPoleAsync(item, created, snapshot, ct);
                    break;
                case "cellule":
                    await EnsureDirectoryPoleAsync(item, created, snapshot, ct);
                    await EnsureDirectoryCelluleAsync(item.Pole!, item.Cellule!, created, snapshot, ct);
                    break;
                case "service":
                    await EnsureDirectoryPoleAsync(item, created, snapshot, ct);
                    await EnsureDirectoryCelluleAsync(item.Pole!, item.Cellule!, created, snapshot, ct);
                    await EnsureDirectoryServiceAsync(item, created, snapshot, ct);
                    break;
            }
        }

        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        await orgMirror.SyncFromDirectoryOverviewAsync(
            string.IsNullOrWhiteSpace(auth) ? null : auth, ct);

        var refreshed = await orgResolver.LoadSnapshotAsync(ct);
        foreach (var item in approved)
            VerifyProvisioned(item, refreshed);

        logger.LogInformation("Provision org Directory import : {Count} opération(s)", created.Count);
        return created;
    }

    private async Task EnsureDirectoryPoleAsync(
        PendingOrgCreationDto item,
        List<OrgNodeCreatedReportDto> created,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct)
    {
        var poleName = item.Pole?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(poleName))
            return;

        if (_poleDirectoryIds.ContainsKey(poleName))
            return;

        var existing = snapshot.Rows.FirstOrDefault(r =>
            EmployeeImportColumnMatcher.Normalize(r.FloorName) == EmployeeImportColumnMatcher.Normalize(poleName));
        if (existing is not null)
        {
            var primeId = await db.Floors.AsNoTracking()
                .Where(f => f.Id == existing.FloorId)
                .Select(f => f.PrimePoleId)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(primeId))
                _poleDirectoryIds[poleName] = primeId!;
            return;
        }

        var businessDepartmentId = await ResolveBusinessDepartmentIdForPoleAsync(item, poleName, ct);
        if (businessDepartmentId == Guid.Empty)
        {
            var departments = _operationalDepartments ?? await directoryOrg.GetOperationalDepartmentsAsync(ct);
            var cause = string.IsNullOrWhiteSpace(item.OperationalDepartment)
                ? "la colonne « Département de production » est vide pour ce pôle"
                : $"la valeur « {item.OperationalDepartment} » ne correspond à aucun département de production existant";
            var hint = departments.Count == 0
                ? "Aucun département de production n'existe : créez-en un dans « Organisation » (ex. OP-001) avant l'import."
                : "Départements disponibles : " + string.Join(", ", departments.Select(d => $"{d.Code} - {d.Name}"))
                  + ". Reprenez l'une de ces valeurs dans le fichier, ou créez le département dans « Organisation » (l'import ne crée pas de département de production).";
            throw new InvalidOperationException(
                $"Impossible de créer le pôle « {poleName} » : {cause}. {hint}");
        }

        string directoryId;
        try
        {
            directoryId = await directoryOrg.CreatePoleAsync(poleName, businessDepartmentId, ct);
        }
        catch (InvalidOperationException)
        {
            if (await TryAdoptExistingOrgNodeAsync("pole", poleName, null, null, snapshot, ct))
                return;
            throw;
        }

        _poleDirectoryIds[poleName] = directoryId;
        created.Add(new OrgNodeCreatedReportDto
        {
            NodeType = "pole",
            Name = poleName,
            DirectoryNodeId = directoryId
        });
    }

    private async Task<Guid> ResolveBusinessDepartmentIdForPoleAsync(
        PendingOrgCreationDto item,
        string poleName,
        CancellationToken ct)
    {
        var departments = _operationalDepartments ?? await directoryOrg.GetOperationalDepartmentsAsync(ct);
        _operationalDepartments ??= departments;

        var fromColumn = EmployeeImportOperationalDeptResolver.ResolveBusinessDepartmentId(
            item.OperationalDepartment,
            departments);
        if (fromColumn.HasValue)
            return fromColumn.Value;

        if (departments.Count == 1 && Guid.TryParse(departments[0].Id, out var onlyDept))
            return onlyDept;

        return Guid.Empty;
    }

    private async Task EnsureDirectoryCelluleAsync(
        string poleName,
        string celluleName,
        List<OrgNodeCreatedReportDto> created,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct)
    {
        var cellKey = $"{poleName}|{celluleName}";
        if (_celluleDirectoryIds.ContainsKey(cellKey))
            return;

        if (FilterRows(snapshot.Rows, poleName, celluleName, null).Any())
        {
            var row = FilterRows(snapshot.Rows, poleName, celluleName, null).First();
            var primeId = await db.Services.AsNoTracking()
                .Where(s => s.Id == row.ServiceId)
                .Select(s => s.PrimeCelluleId)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(primeId))
                _celluleDirectoryIds[cellKey] = primeId!;
            return;
        }

        if (!_poleDirectoryIds.TryGetValue(poleName, out var poleDirectoryId))
        {
            poleDirectoryId = await db.Floors.AsNoTracking()
                .Where(f => EmployeeImportColumnMatcher.Normalize(f.Name) == EmployeeImportColumnMatcher.Normalize(poleName))
                .Select(f => f.PrimePoleId)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(poleDirectoryId))
            throw new InvalidOperationException($"Pôle « {poleName} » introuvable pour créer la cellule « {celluleName} ».");

        string directoryId;
        try
        {
            directoryId = await directoryOrg.CreateCelluleAsync(poleDirectoryId, celluleName, ct);
        }
        catch (InvalidOperationException)
        {
            if (await TryAdoptExistingOrgNodeAsync("cellule", poleName, celluleName, null, snapshot, ct))
                return;
            throw;
        }

        _celluleDirectoryIds[cellKey] = directoryId;
        created.Add(new OrgNodeCreatedReportDto
        {
            NodeType = "cellule",
            Name = celluleName,
            Pole = poleName,
            DirectoryNodeId = directoryId
        });
    }

    private async Task EnsureDirectoryServiceAsync(
        PendingOrgCreationDto item,
        List<OrgNodeCreatedReportDto> created,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct)
    {
        var mapped = new Dictionary<string, string?>
        {
            ["pole"] = item.Pole,
            ["cellule"] = item.Cellule,
            ["service"] = item.Service
        };

        try
        {
            orgResolver.ResolveSubServiceId(snapshot, mapped);
            return;
        }
        catch (InvalidOperationException)
        {
            // create below
        }

        var cellKey = $"{item.Pole}|{item.Cellule}";
        if (!_celluleDirectoryIds.TryGetValue(cellKey, out var celluleDirectoryId))
        {
            celluleDirectoryId = await db.Services.AsNoTracking()
                .Include(s => s.Floor)
                .Where(s => s.Floor != null
                    && EmployeeImportColumnMatcher.Normalize(s.Floor.Name) == EmployeeImportColumnMatcher.Normalize(item.Pole!)
                    && EmployeeImportColumnMatcher.Normalize(s.Name) == EmployeeImportColumnMatcher.Normalize(item.Cellule!))
                .Select(s => s.PrimeCelluleId)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(celluleDirectoryId))
            throw new InvalidOperationException(
                $"Identifiant Directory de la cellule « {item.Cellule} » introuvable.");

        string directoryId;
        try
        {
            directoryId = await directoryOrg.CreateServiceAsync(celluleDirectoryId, item.Service!, ct);
        }
        catch (InvalidOperationException)
        {
            if (await TryAdoptExistingOrgNodeAsync("service", item.Pole!, item.Cellule!, item.Service!, snapshot, ct))
                return;
            throw;
        }

        created.Add(new OrgNodeCreatedReportDto
        {
            NodeType = "service",
            Name = item.Service!,
            Pole = item.Pole,
            Cellule = item.Cellule,
            DirectoryNodeId = directoryId
        });
    }

    private async Task<bool> TryAdoptExistingOrgNodeAsync(
        string nodeType,
        string poleName,
        string? celluleName,
        string? serviceName,
        EmployeeImportOrgSnapshot snapshot,
        CancellationToken ct)
    {
        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        await orgMirror.SyncFromDirectoryOverviewAsync(
            string.IsNullOrWhiteSpace(auth) ? null : auth, ct);

        var refreshed = await orgResolver.LoadSnapshotAsync(ct);
        var exists = nodeType switch
        {
            "pole" => EmployeeImportOrgExistence.PoleExists(refreshed, poleName),
            "cellule" => !string.IsNullOrWhiteSpace(celluleName)
                && EmployeeImportOrgExistence.CelluleExists(refreshed, poleName, celluleName),
            "service" => !string.IsNullOrWhiteSpace(celluleName) && !string.IsNullOrWhiteSpace(serviceName)
                && EmployeeImportOrgExistence.ServiceExists(refreshed, poleName, celluleName, serviceName),
            _ => false
        };

        if (!exists)
            return false;

        switch (nodeType)
        {
            case "pole":
            {
                var row = refreshed.Rows.First(r =>
                    EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
                    EmployeeImportColumnMatcher.Normalize(poleName));
                var primeId = await db.Floors.AsNoTracking()
                    .Where(f => f.Id == row.FloorId)
                    .Select(f => f.PrimePoleId)
                    .FirstOrDefaultAsync(ct);
                if (string.IsNullOrWhiteSpace(primeId))
                    return false;
                _poleDirectoryIds[poleName] = primeId!;
                break;
            }
            case "cellule":
            {
                var row = FilterRows(refreshed.Rows, poleName, celluleName, null).First();
                var primeId = await db.Services.AsNoTracking()
                    .Where(s => s.Id == row.ServiceId)
                    .Select(s => s.PrimeCelluleId)
                    .FirstOrDefaultAsync(ct);
                if (string.IsNullOrWhiteSpace(primeId))
                    return false;
                _celluleDirectoryIds[$"{poleName}|{celluleName}"] = primeId!;
                break;
            }
            case "service":
                break;
        }

        logger.LogInformation(
            "Nœud org « {Type} » déjà présent dans Directory — création ignorée ({Pole}/{Cellule}/{Service}).",
            nodeType, poleName, celluleName, serviceName);
        return true;
    }

    private void VerifyProvisioned(PendingOrgCreationDto item, EmployeeImportOrgSnapshot snapshot)
    {
        switch (item.Type)
        {
            case "pole":
                orgResolver.EnsurePoleExists(snapshot, item.Pole);
                break;
            case "cellule":
                orgResolver.EnsureCelluleExists(snapshot, item.Pole, item.Cellule);
                break;
            case "service":
                orgResolver.ResolveSubServiceId(snapshot, new Dictionary<string, string?>
                {
                    ["pole"] = item.Pole,
                    ["cellule"] = item.Cellule,
                    ["service"] = item.Service
                });
                break;
        }
    }

    private static int OrgOrder(string type) => type switch
    {
        "pole" => 0,
        "cellule" => 1,
        "service" => 2,
        _ => 99
    };

    private static List<OrgHierarchyRow> FilterRows(
        IReadOnlyList<OrgHierarchyRow> rows,
        string? pole,
        string? cellule,
        string? service)
    {
        IEnumerable<OrgHierarchyRow> q = rows;
        if (!string.IsNullOrWhiteSpace(pole))
            q = q.Where(r => EmployeeImportColumnMatcher.Normalize(r.FloorName) == EmployeeImportColumnMatcher.Normalize(pole));
        if (!string.IsNullOrWhiteSpace(cellule))
            q = q.Where(r => EmployeeImportColumnMatcher.Normalize(r.ServiceName) == EmployeeImportColumnMatcher.Normalize(cellule));
        if (!string.IsNullOrWhiteSpace(service))
            q = q.Where(r => EmployeeImportColumnMatcher.Normalize(r.SubServiceName) == EmployeeImportColumnMatcher.Normalize(service));
        return q.ToList();
    }
}
