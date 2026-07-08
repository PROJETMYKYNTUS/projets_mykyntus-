using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.Abstractions.EmployeeImport;
using Planning.Infrastructure.Persistence;
using Planning.Application.DTOs;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services.EmployeeImport;

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
    IUserService userService,
    IContractService contractService,
    IEmployeeImportSessionStore sessionStore,
    IEmployeeImportConfigService configService,
    IEmployeeFieldService fieldService,
    IEmployeeImportOrgResolver orgResolver,
    IEmployeeImportOrgProvisioner orgProvisioner,
    IEmployeeImportStructureAssignmentService structureAssignment,
    IImportExecutionJournal journal,
    IPlanningOrgMirrorService orgMirror,
    IHttpContextAccessor httpContextAccessor) : IEmployeeImportExecutor
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

        ValidateStructuralPreconditions(parsed, columnToField, request, orgSnapshot);

        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        var directoryOverview = await orgMirror.GetDirectoryOverviewAsync(
            string.IsNullOrWhiteSpace(auth) ? null : auth, ct);

        if (request.ApprovedOrgCreations.Count > 0)
            EmployeeImportOrgExistence.ValidateNoDuplicateCreations(request.ApprovedOrgCreations, orgSnapshot);

        var committed = false;
        try
        {
            orgSnapshot = await orgResolver.LoadSnapshotAsync(ct);

            var (orgToProvision, orgNodesSkipped) = request.ApprovedOrgCreations.Count > 0
                ? EmployeeImportOrgExistence.FilterStillNeeded(request.ApprovedOrgCreations, orgSnapshot)
                : ([], []);

            var orgNodesCreated = new List<OrgNodeCreatedReportDto>();
            if (orgToProvision.Count > 0)
            {
                orgNodesCreated = (await orgProvisioner.ProvisionAsync(
                    orgToProvision, orgSnapshot, ct)).ToList();
                orgSnapshot = await orgResolver.LoadSnapshotAsync(ct);

                foreach (var node in orgNodesCreated)
                    journal.RecordOrgCreated(node);
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
                OrgNodesCreated = orgNodesCreated,
                OrgNodesSkipped = orgNodesSkipped
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
                        var created = await CreateAsync(
                            effectiveMapped, email, orgSnapshot, roleResult, activeFields, directoryOverview, ct);
                        journal.RecordUserCreated(created.Id, created.Guid, created.AuthUserId);

                        try
                        {
                            await UpsertCustomFieldsForUserAsync(
                                created.Id, effectiveMapped, activeFields, isCreate: true, ct);
                            await structureAssignment.ApplyIfNeededAsync(
                                created.Guid,
                                roleResult.CanonicalRoleName,
                                effectiveMapped,
                                orgSnapshot,
                                ct);
                            await UpsertContractForUserAsync(created.Id, effectiveMapped, created.HireDate, ct);
                            AddLine(job, report, lineNumber, email, "create", "Employé créé.");
                        }
                        catch (Exception lineEx)
                        {
                            await journal.RollbackLastUserChangeAsync(ct);
                            AddLine(job, report, lineNumber, email, "error", lineEx.Message);
                        }
                    }
                    else
                    {
                        var fullUser = await userService.GetUserByIdAsync(existing.Id);
                        var customBefore = await fieldService.LoadCustomFieldsForUsersAsync([existing.Id], ct);
                        var previousCustom = customBefore.GetValueOrDefault(existing.Id)
                            ?? new Dictionary<string, string?>();

                        var previousState = fullUser is not null
                            ? EmployeeImportHrProfileMapper.MapToUpdateDto(fullUser)
                            : BuildUpdateSnapshot(existing);

                        var (updated, contractChanged) = await UpdateAsync(
                            existing.Id, effectiveMapped, email, orgSnapshot, roleResult, activeFields, fullUser,
                            directoryOverview, ct);

                        var customChanged = await UpsertCustomFieldsForUserAsync(
                            existing.Id, effectiveMapped, activeFields, isCreate: false, ct);

                        if (updated is not null || customChanged || contractChanged)
                        {
                            if (updated is not null)
                            {
                                journal.RecordUserUpdated(existing.Id, previousState, previousCustomFields: previousCustom);

                                try
                                {
                                    await structureAssignment.ApplyIfNeededAsync(
                                        updated.Guid,
                                        roleResult.CanonicalRoleName,
                                        effectiveMapped,
                                        orgSnapshot,
                                        ct);
                                    AddLine(job, report, lineNumber, email, "update", "Employé mis à jour.");
                                }
                                catch (Exception lineEx)
                                {
                                    await journal.RollbackLastUserChangeAsync(ct);
                                    AddLine(job, report, lineNumber, email, "error", lineEx.Message);
                                }
                            }
                            else if (contractChanged)
                            {
                                AddLine(job, report, lineNumber, email, "update", "Employé mis à jour.");
                            }
                            else
                            {
                                AddLine(job, report, lineNumber, email, "update", "Employé mis à jour.");
                            }
                        }
                        else
                        {
                            AddLine(job, report, lineNumber, email, "ignore", "Employé déjà présent — données identiques, aucune modification.");
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
            journal.Commit();
            committed = true;
            return report;
        }
        catch (Exception ex) when (!committed)
        {
            await journal.CompensateAsync(ct);
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException(
                $"L'import a échoué avant la génération du rapport. Aucune modification n'a été appliquée. Détail : {detail}",
                ex);
        }
    }

    private static void ValidateStructuralPreconditions(
        ParsedImportFile parsed,
        Dictionary<int, string> columnToField,
        EmployeeImportExecuteRequest request,
        EmployeeImportOrgSnapshot orgSnapshot)
    {
        if (!columnToField.Values.Contains("email"))
        {
            throw new InvalidOperationException(
                "Mapping invalide : la colonne email est obligatoire pour l'import.");
        }

        if (request.ApprovedOrgCreations.Count > 0 && !request.ConfirmOrgProvision)
        {
            throw new InvalidOperationException(
                "Confirmation RH requise pour créer des organisations manquantes.");
        }

        if (orgSnapshot.Roles.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucun rôle disponible pour l'import. Vérifiez la configuration Planning.");
        }

        for (var i = 0; i < parsed.Rows.Count; i++)
        {
            if (IsEmptyRow(parsed.Rows[i]))
                continue;

            var lineNumber = i + 2;
            var mapped = EmployeeImportRowMapper.MapRow(parsed.Rows[i], columnToField);
            if (!mapped.TryGetValue("email", out var emailRaw) || string.IsNullOrWhiteSpace(emailRaw))
                continue;

            var effectiveMapped = ApplyOrgResolution(
                mapped, orgSnapshot, lineNumber, request.AcceptedFuzzyMatches);
            var roleResult = EmployeeImportRoleResolver.Resolve(
                effectiveMapped.GetValueOrDefault("role"), orgSnapshot.Roles);

            if (roleResult.ErrorMessage is not null)
            {
                throw new InvalidOperationException(
                    $"Validation structurelle ligne {lineNumber} : {roleResult.ErrorMessage}");
            }

            var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(roleResult.CanonicalRoleName);
            if (depth != EmployeeImportOrgDepth.None &&
                !EmployeeImportRoleSynonymRegistry.HasRequiredOrgColumns(effectiveMapped, depth))
            {
                throw new InvalidOperationException(
                    $"Validation structurelle ligne {lineNumber} : {EmployeeImportRoleSynonymRegistry.RequiredOrgColumnsMessage(depth)}");
            }

            foreach (var hint in EmployeeImportOrgFuzzyMatcher.ResolveOrgNames(
                         orgSnapshot,
                         effectiveMapped.GetValueOrDefault("pole"),
                         effectiveMapped.GetValueOrDefault("cellule"),
                         effectiveMapped.GetValueOrDefault("service")).Hints
                         .Where(h => h.Confidence == "medium"))
            {
                var approved = request.AcceptedFuzzyMatches.Any(a =>
                    a.LineNumber == lineNumber
                    && string.Equals(a.FieldKey, hint.FieldKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a.SourceValue, hint.SourceValue, StringComparison.OrdinalIgnoreCase));

                if (!approved)
                {
                    throw new InvalidOperationException(
                        $"Validation structurelle ligne {lineNumber} : correspondance organisationnelle « {hint.SourceValue} » " +
                        $"({hint.FieldKey}) non validée. Acceptez-la à l'étape Organisation ou corrigez le fichier.");
                }
            }
        }
    }

    private static UpdateUserDto BuildUpdateSnapshot(User existing) =>
        new()
        {
            Email = existing.Email,
            FirstName = existing.FirstName,
            LastName = existing.LastName,
            RoleId = existing.RoleId,
            SubServiceId = existing.SubServiceId,
            HireDate = existing.HireDate,
            Level = existing.Level,
            IsActive = existing.IsActive,
        };

    private async Task<EmployeeImportUserResult> CreateAsync(
        Dictionary<string, string?> mapped,
        string email,
        EmployeeImportOrgSnapshot orgSnapshot,
        RoleResolveResult roleResult,
        List<EmployeeImportFieldConfigDto> activeFields,
        EmployeeImportOrgOverview? directoryOverview,
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

        var hireDate = ResolveHireDate(mapped);
        var hrBuild = EmployeeImportHrProfileMapper.BuildForCreate(mapped, hireDate);
        var mentors = await ResolveMentorsAsync(mapped, roleResult.CanonicalRoleName, directoryOverview, ct);

        var dto = new CreateUserFromImportDto
        {
            Email = email,
            FirstName = GetRequired(mapped, "firstName"),
            LastName = GetRequired(mapped, "lastName"),
            RoleId = roleId,
            SubServiceId = subServiceId,
            HireDate = hireDate,
            Level = ResolveLevel(mapped),
            Password = mapped.GetValueOrDefault("password"),
            IsActiveOnImport = mapped.ContainsKey("isActive") &&
                EmployeeImportRowMapper.TryParseBool(mapped["isActive"], out var isActive)
                ? isActive
                : null,
            HrProfile = hrBuild.Profile,
            NiveauExpertiseMetier = hrBuild.NiveauExpertiseMetier,
            ChefDeProjetId = mentors.Chef,
            SuperviseurId = mentors.Superviseur,
            ReferentTechniqueId = mentors.Referent,
        };

        return await userPersistence.CreateFromImportAsync(dto, ct);
    }

    private async Task<(EmployeeImportUserResult? User, bool ContractChanged)> UpdateAsync(
        int userId,
        Dictionary<string, string?> mapped,
        string email,
        EmployeeImportOrgSnapshot orgSnapshot,
        RoleResolveResult roleResult,
        List<EmployeeImportFieldConfigDto> activeFields,
        UserDto? fullUser,
        EmployeeImportOrgOverview? directoryOverview,
        CancellationToken ct)
    {
        var existing = await userPersistence.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Employé introuvable.");

        fullUser ??= await userService.GetUserByIdAsync(userId);

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

        var hireDate = mapped.ContainsKey("hireDate") && EmployeeImportRowMapper.TryParseDate(mapped["hireDate"], out var hd)
            ? hd
            : existing.HireDate;

        var hrMerge = EmployeeImportHrProfileMapper.MergeForUpdate(
            mapped,
            fullUser?.HrProfile,
            fullUser?.NiveauExpertiseMetier,
            fullUser?.ChefDeProjetId,
            fullUser?.SuperviseurId,
            fullUser?.ReferentTechniqueId,
            hireDate);

        var mentors = await ResolveMentorsAsync(mapped, roleResult.CanonicalRoleName, directoryOverview, ct);
        var chefId = mapped.ContainsKey("chefDeProjetName") ? mentors.Chef : fullUser?.ChefDeProjetId;
        var superviseurId = mapped.ContainsKey("superviseurName") ? mentors.Superviseur : fullUser?.SuperviseurId;
        var referentId = mapped.ContainsKey("referentTechniqueName") ? mentors.Referent : fullUser?.ReferentTechniqueId;

        var dto = new UpdateUserDto
        {
            Email = email,
            FirstName = mapped.TryGetValue("firstName", out var fn) && !string.IsNullOrWhiteSpace(fn)
                ? fn.Trim() : existing.FirstName,
            LastName = mapped.TryGetValue("lastName", out var ln) && !string.IsNullOrWhiteSpace(ln)
                ? ln.Trim() : existing.LastName,
            RoleId = newRoleId,
            SubServiceId = hasOrgColumns ? newSubServiceId : existing.SubServiceId,
            HireDate = hireDate,
            Level = mapped.TryGetValue("level", out var levelRaw) && !string.IsNullOrWhiteSpace(levelRaw)
                ? EmployeeImportLevelResolver.Resolve(levelRaw)
                : existing.Level,
            IsActive = existing.IsActive,
            HrProfile = hrMerge.Profile,
            NiveauExpertiseMetier = hrMerge.NiveauExpertiseMetier,
            ChefDeProjetId = chefId,
            SuperviseurId = superviseurId,
            ReferentTechniqueId = referentId,
        };

        if (mapped.ContainsKey("isActive") &&
            EmployeeImportRowMapper.TryParseBool(mapped["isActive"], out var isActive))
        {
            dto.IsActive = isActive;
        }

        var mentorsChanged = (mapped.ContainsKey("chefDeProjetName") && chefId != fullUser?.ChefDeProjetId)
            || (mapped.ContainsKey("superviseurName") && superviseurId != fullUser?.SuperviseurId)
            || (mapped.ContainsKey("referentTechniqueName") && referentId != fullUser?.ReferentTechniqueId);

        var changed = dto.FirstName != existing.FirstName
            || dto.LastName != existing.LastName
            || dto.Email != existing.Email
            || dto.RoleId != existing.RoleId
            || dto.SubServiceId != existing.SubServiceId
            || dto.HireDate != existing.HireDate
            || dto.Level != existing.Level
            || dto.IsActive != existing.IsActive
            || hrMerge.HasHrData
            || mentorsChanged;

        var contractChanged = false;
        if (EmployeeImportHrProfileMapper.ShouldUpsertContract(mapped))
            contractChanged = await UpsertContractForUserAsync(userId, mapped, hireDate, ct);

        if (!changed)
            return (null, contractChanged);

        await userPersistence.UpdateAsync(userId, dto, ct);
        var updated = await userPersistence.GetByIdAsync(userId, ct);
        return (updated, contractChanged);
    }

    private async Task<bool> UpsertContractForUserAsync(
        int userId,
        Dictionary<string, string?> mapped,
        DateTime hireDate,
        CancellationToken ct)
    {
        if (!EmployeeImportHrProfileMapper.ShouldUpsertContract(mapped))
            return false;

        var existingContracts = (await contractService.GetContractsByUserIdAsync(userId)).ToList();
        var latest = existingContracts.FirstOrDefault();

        if (latest is null)
        {
            var createDto = EmployeeImportHrProfileMapper.BuildCreateContractDto(mapped, userId, hireDate);
            if (createDto is null)
                return false;

            await contractService.CreateContractAsync(createDto);
            return true;
        }

        var updateDto = EmployeeImportHrProfileMapper.BuildUpdateContractDto(mapped, latest);
        if (updateDto is null)
        {
            if (mapped.ContainsKey("contractType"))
            {
                var createDto = EmployeeImportHrProfileMapper.BuildCreateContractDto(mapped, userId, hireDate);
                if (createDto is not null)
                {
                    await contractService.CreateContractAsync(createDto);
                    return true;
                }
            }

            return false;
        }

        await contractService.UpdateContractAsync(latest.Id, updateDto);
        return true;
    }

    private void ValidateOrgForRole(
        Dictionary<string, string?> mapped,
        RoleResolveResult roleResult,
        EmployeeImportOrgSnapshot orgSnapshot) =>
        ValidateOrgForRoleStatic(mapped, roleResult, orgSnapshot, orgResolver);

    private static void ValidateOrgForRoleStatic(
        Dictionary<string, string?> mapped,
        RoleResolveResult roleResult,
        EmployeeImportOrgSnapshot orgSnapshot,
        IEmployeeImportOrgResolver orgResolver)
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

    private async Task<(Guid? Chef, Guid? Superviseur, Guid? Referent)> ResolveMentorsAsync(
        IReadOnlyDictionary<string, string?> mapped,
        string canonicalRole,
        EmployeeImportOrgOverview? directoryOverview,
        CancellationToken ct)
    {
        if (mapped.ContainsKey("chefDeProjetEmail")
            || mapped.ContainsKey("superviseurEmail")
            || mapped.ContainsKey("referentTechniqueEmail"))
        {
            throw new InvalidOperationException(
                "Les colonnes email des responsables sont obsolètes. Utilisez Chef de projet, Superviseur et Référent technique (nom complet).");
        }

        if (!EmployeeImportMentorResolver.HasAnyMentorField(mapped))
            return (null, null, null);

        if (directoryOverview is null)
        {
            throw new InvalidOperationException(
                "Impossible de valider les responsables : synchronisation Organisation RH requise (réessayez après connexion).");
        }

        return await EmployeeImportMentorResolver.ResolveAndValidateAsync(
            db, directoryOverview, mapped, canonicalRole, ct);
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
