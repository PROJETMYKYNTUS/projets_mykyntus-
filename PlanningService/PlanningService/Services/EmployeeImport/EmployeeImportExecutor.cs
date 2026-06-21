using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Models;

namespace PlanningService.Services.EmployeeImport;

public interface IEmployeeImportExecutor
{
    Task<EmployeeImportReportDto> ExecuteAsync(
        EmployeeImportExecuteRequest request,
        string? startedByEmail,
        CancellationToken ct = default);
}

public class EmployeeImportExecutor(
    AppDbContext db,
    IEmployeeImportUserPersistence userPersistence,
    IEmployeeImportSessionStore sessionStore,
    IEmployeeImportConfigService configService,
    IEmployeeFieldService fieldService,
    IEmployeeImportOrgResolver orgResolver,
    IEmployeeImportOrgProvisioner orgProvisioner,
    IEmployeeImportStructureAssignmentService structureAssignment) : IEmployeeImportExecutor
{
    public async Task<EmployeeImportReportDto> ExecuteAsync(
        EmployeeImportExecuteRequest request,
        string? startedByEmail,
        CancellationToken ct = default)
    {
        var parsed = await sessionStore.GetAsync(request.ImportSessionId, ct)
            ?? throw new InvalidOperationException("Session d'import expirée ou introuvable. Re-analysez le fichier.");

        var resolvedMappings = await fieldService.ResolveImportMappingsAsync(
            request.Mappings, parsed.Headers, ct);

        var fileName = await sessionStore.GetFileNameAsync(request.ImportSessionId, ct) ?? "import";
        var activeFields = (await configService.GetConfigAsync(ct)).Where(f => f.IsEnabled).ToList();
        var columnToField = EmployeeImportMappingHelper.BuildColumnMap(resolvedMappings, activeFields);
        var orgSnapshot = await orgResolver.LoadSnapshotAsync(ct);

        var orgNodesCreated = new List<OrgNodeCreatedReportDto>();
        if (request.ApprovedOrgCreations.Count > 0)
        {
            if (!request.ConfirmOrgProvision)
            {
                throw new InvalidOperationException(
                    "Confirmation RH requise pour créer des organisations manquantes.");
            }

            ValidateNoDuplicateOrgCreations(request.ApprovedOrgCreations, orgSnapshot);

            orgNodesCreated = (await orgProvisioner.ProvisionAsync(
                request.ApprovedOrgCreations, orgSnapshot, ct)).ToList();
            orgSnapshot = await orgResolver.LoadSnapshotAsync(ct);
        }

        var job = new EmployeeImportJob
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            TotalLignes = parsed.Rows.Count,
            StartedByEmail = startedByEmail,
            StartedAt = DateTime.UtcNow
        };
        db.EmployeeImportJobs.Add(job);

        var report = new EmployeeImportReportDto
        {
            ImportJobId = job.Id,
            TotalLignes = parsed.Rows.Count,
            CompletedAt = DateTime.UtcNow,
            OrgNodesCreated = orgNodesCreated
        };

        for (var i = 0; i < parsed.Rows.Count; i++)
        {
            var lineNumber = i + 2;
            var row = parsed.Rows[i];
            if (IsEmptyRow(row))
            {
                AddLine(job, report, lineNumber, null, "ignore", "Ligne vide.");
                continue;
            }

            var mapped = EmployeeImportRowMapper.MapRow(row, columnToField);
            if (!mapped.TryGetValue("email", out var emailRaw) || string.IsNullOrWhiteSpace(emailRaw))
            {
                AddLine(job, report, lineNumber, null, "ignore", "Identifiant email manquant.");
                continue;
            }

            var email = emailRaw.Trim().ToLowerInvariant();

            try
            {
                var effectiveMapped = ApplyOrgResolution(
                    mapped, orgSnapshot, lineNumber, request.AcceptedFuzzyMatches);
                var roleResult = EmployeeImportRoleResolver.Resolve(effectiveMapped.GetValueOrDefault("role"), orgSnapshot.Roles);
                if (roleResult.ErrorMessage is not null)
                    throw new InvalidOperationException(roleResult.ErrorMessage);

                ValidateOrgForRole(effectiveMapped, roleResult, orgSnapshot);

                var existing = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

                if (existing is null)
                {
                    var created = await CreateAsync(effectiveMapped, email, orgSnapshot, roleResult, activeFields, ct);
                    await UpsertCustomFieldsForUserAsync(
                        created.Id, effectiveMapped, activeFields, isCreate: true, ct);
                    await structureAssignment.ApplyIfNeededAsync(
                        created.Guid,
                        roleResult.CanonicalRoleName,
                        effectiveMapped,
                        orgSnapshot,
                        ct);
                    AddLine(job, report, lineNumber, email, "create", "Employé créé.");
                }
                else
                {
                    var updated = await UpdateAsync(
                        existing.Id, effectiveMapped, email, orgSnapshot, roleResult, activeFields, ct);
                    var customChanged = await UpsertCustomFieldsForUserAsync(
                        existing.Id, effectiveMapped, activeFields, isCreate: false, ct);
                    if (updated is not null || customChanged)
                    {
                        if (updated is not null)
                        {
                            await structureAssignment.ApplyIfNeededAsync(
                                updated.Guid,
                                roleResult.CanonicalRoleName,
                                effectiveMapped,
                                orgSnapshot,
                                ct);
                        }
                        AddLine(job, report, lineNumber, email, "update", "Employé mis à jour.");
                    }
                    else
                    {
                        AddLine(job, report, lineNumber, email, "ignore", "Aucun changement détecté.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLine(job, report, lineNumber, email, "error", ex.Message);
            }
        }

        job.Crees = report.Crees;
        job.MisAJour = report.MisAJour;
        job.Ignores = report.Ignores;
        job.Erreurs = report.Erreurs;
        job.CompletedAt = DateTime.UtcNow;
        report.CompletedAt = job.CompletedAt.Value;

        await db.SaveChangesAsync(ct);
        return report;
    }

    private static void ValidateNoDuplicateOrgCreations(
        IReadOnlyList<PendingOrgCreationDto> approved,
        EmployeeImportOrgSnapshot snapshot)
    {
        foreach (var item in approved)
        {
            var (fieldKey, raw, candidates) = item.Type switch
            {
                "pole" => ("pole", item.Pole, snapshot.Rows.Select(r => r.FloorName).Distinct().ToList()),
                "cellule" => ("cellule", item.Cellule, FilterRows(snapshot.Rows, item.Pole, null, null)
                    .Select(r => r.ServiceName).Distinct().ToList()),
                "service" => ("service", item.Service, FilterRows(snapshot.Rows, item.Pole, item.Cellule, null)
                    .Select(r => r.SubServiceName).Distinct().ToList()),
                _ => (string.Empty, null, new List<string>())
            };

            if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(raw))
                continue;

            var likely = EmployeeImportFuzzyMatcher.FindBestOrgMatch(fieldKey, raw, candidates);
            if (likely is null)
                continue;

            var label = fieldKey switch { "pole" => "pôle", "cellule" => "cellule", _ => "service" };
            throw new InvalidOperationException(
                $"Création refusée : un {label} très proche existe déjà (« {likely.Value} »). " +
                $"Utilisez le nom existant ou validez la correspondance proposée à l'étape Organisation.");
        }
    }

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

    private async Task<EmployeeImportUserResult> CreateAsync(
        Dictionary<string, string?> mapped,
        string email,
        EmployeeImportOrgSnapshot orgSnapshot,
        RoleResolveResult roleResult,
        List<EmployeeImportFieldConfigDto> activeFields,
        CancellationToken ct)
    {
        ValidateRequiredOnCreate(mapped, activeFields);

        var roleId = roleResult.RoleId
            ?? throw new InvalidOperationException("Rôle invalide ou manquant.");
        EnsureImportRoleAllowed(orgSnapshot.Roles, roleId);

        var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(roleResult.CanonicalRoleName);
        int? subServiceId = depth == EmployeeImportOrgDepth.Service
            ? orgResolver.ResolveSubServiceId(orgSnapshot, mapped)
            : null;

        var dto = new CreateUserFromImportDto
        {
            Email = email,
            FirstName = GetRequired(mapped, "firstName"),
            LastName = GetRequired(mapped, "lastName"),
            RoleId = roleId,
            SubServiceId = subServiceId,
            HireDate = ResolveHireDate(mapped),
            Level = ResolveLevel(mapped),
            Password = mapped.GetValueOrDefault("password"),
            IsActiveOnImport = mapped.ContainsKey("isActive") &&
                EmployeeImportRowMapper.TryParseBool(mapped["isActive"], out var isActive)
                ? isActive
                : null,
        };

        return await userPersistence.CreateFromImportAsync(dto, ct);
    }

    private async Task<EmployeeImportUserResult?> UpdateAsync(
        int userId,
        Dictionary<string, string?> mapped,
        string email,
        EmployeeImportOrgSnapshot orgSnapshot,
        RoleResolveResult roleResult,
        List<EmployeeImportFieldConfigDto> activeFields,
        CancellationToken ct)
    {
        var existing = await userPersistence.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Employé introuvable.");

        var hasOrgColumns = mapped.ContainsKey("pole") || mapped.ContainsKey("cellule") ||
                            mapped.ContainsKey("service") || mapped.ContainsKey("subService");
        int? newSubServiceId = existing.SubServiceId;
        if (hasOrgColumns)
        {
            var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(roleResult.CanonicalRoleName);
            newSubServiceId = depth == EmployeeImportOrgDepth.Service
                ? orgResolver.ResolveSubServiceId(orgSnapshot, mapped)
                : existing.SubServiceId;
        }

        var newRoleId = existing.RoleId;
        if (mapped.ContainsKey("role"))
        {
            newRoleId = roleResult.RoleId ?? existing.RoleId;
            EnsureImportRoleAllowed(orgSnapshot.Roles, newRoleId);
        }

        var dto = new UpdateUserDto
        {
            Email = email,
            FirstName = mapped.TryGetValue("firstName", out var fn) && !string.IsNullOrWhiteSpace(fn)
                ? fn.Trim() : existing.FirstName,
            LastName = mapped.TryGetValue("lastName", out var ln) && !string.IsNullOrWhiteSpace(ln)
                ? ln.Trim() : existing.LastName,
            RoleId = newRoleId,
            SubServiceId = hasOrgColumns ? newSubServiceId : existing.SubServiceId,
            HireDate = mapped.ContainsKey("hireDate") && EmployeeImportRowMapper.TryParseDate(mapped["hireDate"], out var hd)
                ? hd : existing.HireDate,
            Level = mapped.TryGetValue("level", out var levelRaw) && !string.IsNullOrWhiteSpace(levelRaw)
                ? EmployeeImportLevelResolver.Resolve(levelRaw)
                : existing.Level,
            IsActive = existing.IsActive,
        };

        if (mapped.ContainsKey("isActive") &&
            EmployeeImportRowMapper.TryParseBool(mapped["isActive"], out var isActive))
        {
            dto.IsActive = isActive;
        }

        var changed = dto.FirstName != existing.FirstName
            || dto.LastName != existing.LastName
            || dto.Email != existing.Email
            || dto.RoleId != existing.RoleId
            || dto.SubServiceId != existing.SubServiceId
            || dto.HireDate != existing.HireDate
            || dto.Level != existing.Level
            || dto.IsActive != existing.IsActive;

        if (!changed)
            return null;

        await userPersistence.UpdateAsync(userId, dto, ct);
        return await userPersistence.GetByIdAsync(userId, ct);
    }

    private void ValidateOrgForRole(
        Dictionary<string, string?> mapped,
        RoleResolveResult roleResult,
        EmployeeImportOrgSnapshot orgSnapshot)
    {
        var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(roleResult.CanonicalRoleName);
        if (depth == EmployeeImportOrgDepth.None)
            return;

        if (!EmployeeImportRoleSynonymRegistry.HasRequiredOrgColumns(mapped, depth))
            throw new InvalidOperationException(EmployeeImportRoleSynonymRegistry.RequiredOrgColumnsMessage(depth));

        if (depth == EmployeeImportOrgDepth.Pole)
            orgResolver.EnsurePoleExists(orgSnapshot, mapped.GetValueOrDefault("pole"));
        else if (depth == EmployeeImportOrgDepth.Cellule)
            orgResolver.EnsureCelluleExists(orgSnapshot, mapped.GetValueOrDefault("pole"), mapped.GetValueOrDefault("cellule"));
        else if (depth == EmployeeImportOrgDepth.Service)
            orgResolver.ResolveSubServiceId(orgSnapshot, mapped);
    }

    private static Dictionary<string, string?> ApplyOrgResolution(
        Dictionary<string, string?> mapped,
        EmployeeImportOrgSnapshot snapshot,
        int lineNumber,
        IReadOnlyList<AcceptedFuzzyOrgMatchDto> accepted)
    {
        mapped.TryGetValue("pole", out var pole);
        mapped.TryGetValue("cellule", out var cellule);
        mapped.TryGetValue("service", out var service);

        var resolution = EmployeeImportOrgFuzzyMatcher.ResolveOrgNames(snapshot, pole, cellule, service);
        var effective = EmployeeImportOrgFuzzyMatcher.ApplyToMapped(mapped, resolution);

        foreach (var hint in resolution.Hints.Where(h => h.Confidence == "medium"))
        {
            var approved = accepted.Any(a =>
                a.LineNumber == lineNumber
                && string.Equals(a.FieldKey, hint.FieldKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.SourceValue, hint.SourceValue, StringComparison.OrdinalIgnoreCase));

            if (!approved)
                effective[hint.FieldKey] = hint.SourceValue;
        }

        return effective;
    }

    private static void EnsureImportRoleAllowed(IReadOnlyList<Role> roles, int roleId)
    {
        var role = roles.FirstOrDefault(r => r.Id == roleId);
        if (role is not null && EmployeeImportFieldRegistry.IsImportForbiddenRoleName(role.Name))
        {
            var message = EmployeeImportRoleSynonymRegistry.GetForbiddenRoleMessage(role.Name)
                ?? "Rôle interdit à l'import.";
            throw new InvalidOperationException(message);
        }
    }

    private async Task<bool> UpsertCustomFieldsForUserAsync(
        int userId,
        Dictionary<string, string?> mapped,
        List<EmployeeImportFieldConfigDto> activeFields,
        bool isCreate,
        CancellationToken ct)
    {
        var customValues = EmployeeFieldValidator.ExtractCustomFieldValues(mapped, activeFields);
        if (customValues.Count == 0)
            return false;

        if (isCreate)
            EmployeeFieldValidator.ValidateRequiredCustomFieldsOnCreate(customValues, activeFields, onlyMappedKeys: true);

        var before = await fieldService.LoadCustomFieldsForUsersAsync([userId], ct);
        await fieldService.UpsertCustomFieldsAsync(userId, customValues, isCreate, ct);
        var after = await fieldService.LoadCustomFieldsForUsersAsync([userId], ct);

        if (!before.TryGetValue(userId, out var beforeValues))
            return after.ContainsKey(userId);

        if (!after.TryGetValue(userId, out var afterValues))
            return beforeValues.Count > 0;

        foreach (var key in customValues.Keys)
        {
            beforeValues.TryGetValue(key, out var oldVal);
            afterValues.TryGetValue(key, out var newVal);
            if (!string.Equals(oldVal ?? string.Empty, newVal ?? string.Empty, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void ValidateRequiredOnCreate(
        Dictionary<string, string?> mapped,
        List<EmployeeImportFieldConfigDto> activeFields)
    {
        foreach (var field in activeFields.Where(f => f.IsRequiredOnCreate && f.IsSystemField))
        {
            if (!mapped.TryGetValue(field.FieldKey, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Champ obligatoire manquant : {field.Label}.");
        }

        EmployeeFieldValidator.ValidateRequiredCustomFieldsOnCreate(
            EmployeeFieldValidator.ExtractCustomFieldValues(mapped, activeFields),
            activeFields,
            onlyMappedKeys: true);
    }

    private static string GetRequired(Dictionary<string, string?> mapped, string key) =>
        mapped.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"Champ obligatoire manquant : {key}.");

    private static DateTime ResolveHireDate(Dictionary<string, string?> mapped)
    {
        if (mapped.TryGetValue("hireDate", out var raw) &&
            EmployeeImportRowMapper.TryParseDate(raw, out var date))
            return date;
        return DateTime.UtcNow;
    }

    private static int ResolveLevel(Dictionary<string, string?> mapped)
    {
        if (!mapped.TryGetValue("level", out var raw))
            return EmployeeImportLevelResolver.DefaultLevel;

        return EmployeeImportLevelResolver.Resolve(raw);
    }

    private static Dictionary<int, string> BuildColumnMap(
        IReadOnlyList<EmployeeImportMappingItemDto> mappings,
        List<EmployeeImportFieldConfigDto> activeFields) =>
        EmployeeImportMappingHelper.BuildColumnMap(mappings, activeFields);

    private static bool IsEmptyRow(IReadOnlyList<string> row) =>
        row.All(c => string.IsNullOrWhiteSpace(c));

    private static void AddLine(
        EmployeeImportJob job,
        EmployeeImportReportDto report,
        int lineNumber,
        string? email,
        string action,
        string? message)
    {
        job.Lines.Add(new EmployeeImportJobLine
        {
            JobId = job.Id,
            LineNumber = lineNumber,
            Email = email,
            Action = action,
            Message = message
        });

        report.Lignes.Add(new EmployeeImportRowResultDto
        {
            LineNumber = lineNumber,
            Email = email,
            Action = action,
            Message = message
        });

        switch (action)
        {
            case "create": report.Crees++; break;
            case "update": report.MisAJour++; break;
            case "error": report.Erreurs++; break;
            default: report.Ignores++; break;
        }
    }
}
