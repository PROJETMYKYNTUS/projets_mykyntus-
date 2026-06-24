using Documentation.Application;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Domain.Entities;
using Documentation.Infrastructure.Mapping;
using Documentation.Infrastructure.Persistence;
using Documentation.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Documentation.Infrastructure.Services;

public sealed class DocumentRequestAppService(
    DocumentationDbContext db,
    IDocumentationTenantAccessor tenantAccessor,
    IDocumentationRequestContext userContext,
    ILogger<DocumentRequestAppService> logger) : IDocumentRequestAppService
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const int MaxTemplateVariables = 100;

    public async Task<PagedResponse<DocumentRequestResponse>> ListAsync(DocumentRequestListQuery query, CancellationToken ct = default)
    {
        if (query.Scope is DocumentRequestListScope.MyRequests or DocumentRequestListScope.AssignedToMe
            && !userContext.UserId.HasValue)
            throw new DocumentationApiException(401, "Authentification requise.");

        var tenant = tenantAccessor.ResolvedTenantId;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        if (!TryParseSortOrder(query.SortOrder, out var desc))
            throw new DocumentationApiException(400, "sortOrder doit être « asc » ou « desc ».");

        DocumentRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<DocumentRequestStatus>(query.Status.Trim(), ignoreCase: true, out var st))
                throw new DocumentationApiException(400, "status invalide (pending, approved, rejected, generated, cancelled).");
            statusFilter = st;
        }

        AppRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (!AppRoleHeaderParser.TryParse(query.Role, out var rf))
                throw new DocumentationApiException(400, "role de filtre invalide (pilote, coach, manager, rp, rh, admin, audit).");
            roleFilter = rf;
        }

        var (filterTypeId, catalogOnly, customOnly) = ParseTypeFilter(query.Type);

        if (!TryParseRequestSortField(query.SortBy, out var sortField))
            throw new DocumentationApiException(400, "sortBy doit être createdAt, status ou requestNumber.");

        var baseQuery = db.DocumentRequests.AsNoTracking();
        baseQuery = ApplyStandardDocumentRequestFilters(baseQuery, statusFilter, filterTypeId, catalogOnly, customOnly);

        if (query.Scope == DocumentRequestListScope.MyRequests)
        {
            var uid = userContext.UserId!.Value;
            baseQuery = baseQuery.Where(r => r.RequesterUserId == uid);
        }
        else if (query.Scope == DocumentRequestListScope.AssignedToMe)
        {
            var uid = userContext.UserId!.Value;
            baseQuery = baseQuery.Where(r =>
                r.BeneficiaryUserId == uid
                || (!r.BeneficiaryUserId.HasValue && r.RequesterUserId == uid));
        }
        else
        {
            if (roleFilter.HasValue)
            {
                var roleUserIds = await db.DirectoryUsers.AsNoTracking()
                    .Where(u => u.Role == roleFilter.Value)
                    .Select(u => u.Id)
                    .ToListAsync(ct);
                baseQuery = roleUserIds.Count > 0
                    ? baseQuery.Where(r => roleUserIds.Contains(r.RequesterUserId))
                    : baseQuery.Where(static r => false);
            }

            if (userContext.Role == AppRole.Pilote && userContext.UserId.HasValue)
            {
                var uid = userContext.UserId.Value;
                baseQuery = baseQuery.Where(r => r.RequesterUserId == uid || r.BeneficiaryUserId == uid);
            }
            else if (userContext.Role == AppRole.Coach && userContext.UserId.HasValue)
            {
                var coachId = userContext.UserId.Value;
                var pilotIds = await db.DirectoryUsers.AsNoTracking()
                    .Where(u => u.Role == AppRole.Pilote && u.CoachId == coachId)
                    .Select(u => u.Id)
                    .ToListAsync(ct);
                baseQuery = pilotIds.Count > 0
                    ? baseQuery.Where(r =>
                        pilotIds.Contains(r.RequesterUserId) ||
                        (r.BeneficiaryUserId.HasValue && pilotIds.Contains(r.BeneficiaryUserId.Value)))
                    : baseQuery.Where(static r => false);
            }

            if (userContext.ScopeCoachId.HasValue &&
                userContext.Role is AppRole.Manager or AppRole.Rp)
            {
                var pilotIds = await db.DirectoryUsers.AsNoTracking()
                    .Where(u => u.CoachId == userContext.ScopeCoachId && u.Role == AppRole.Pilote)
                    .Select(u => u.Id)
                    .ToListAsync(ct);
                baseQuery = pilotIds.Count > 0
                    ? baseQuery.Where(r => pilotIds.Contains(r.RequesterUserId))
                    : baseQuery.Where(static r => false);
            }
            else if (userContext.ScopeManagerId.HasValue && !userContext.ScopeCoachId.HasValue &&
                     userContext.Role == AppRole.Rp)
            {
                var coachIds = await db.DirectoryUsers.AsNoTracking()
                    .Where(u => u.Role == AppRole.Coach && u.ManagerId == userContext.ScopeManagerId)
                    .Select(u => u.Id)
                    .ToListAsync(ct);
                var pilotIds = await db.DirectoryUsers.AsNoTracking()
                    .Where(u => u.Role == AppRole.Pilote && u.CoachId.HasValue && coachIds.Contains(u.CoachId!.Value))
                    .Select(u => u.Id)
                    .ToListAsync(ct);
                baseQuery = pilotIds.Count > 0
                    ? baseQuery.Where(r => pilotIds.Contains(r.RequesterUserId))
                    : baseQuery.Where(static r => false);
            }
        }

        var logOperation = query.Scope switch
        {
            DocumentRequestListScope.MyRequests => nameof(DocumentRequestListScope.MyRequests),
            DocumentRequestListScope.AssignedToMe => nameof(DocumentRequestListScope.AssignedToMe),
            _ => nameof(DocumentRequestListScope.AllVisible),
        };

        return await PaginateDocumentRequestsAsync(baseQuery, page, pageSize, sortField, desc, tenant, logOperation, ct);
    }

    public async Task<DocumentRequestResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || !await CanActorViewAsync(r, ct))
            return null;

        DocumentType? typeRow = null;
        if (r.DocumentTypeId.HasValue)
            typeRow = await db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.DocumentTypeId.Value, ct);

        var names = await DocumentRequestMappingHelper.LoadDisplayNamesAsync(
            db,
            new[] { r.RequesterUserId, r.BeneficiaryUserId ?? Guid.Empty },
            ct);
        var latestGen = await DocumentRequestMappingHelper.LoadLatestGeneratedForRequestAsync(db, r.Id, ct);
        string? tplName = null;
        if (r.DocumentTemplateId is { } tidGet)
        {
            tplName = await db.DocumentTemplates.AsNoTracking()
                .Where(t => t.Id == tidGet)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct);
        }

        return DocumentRequestMapper.ToResponse(r, typeRow, userContext, names, latestGen, tplName);
    }

    public async Task<DocumentRequestFieldValuesResponse?> GetFieldValuesAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || !await CanActorViewAsync(r, ct))
            return null;

        var rows = await db.DocumentRequestFieldValues.AsNoTracking()
            .Where(f => f.DocumentRequestId == id)
            .ToListAsync(ct);
        return new DocumentRequestFieldValuesResponse { Values = ToFieldValuesDict(rows) };
    }

    public async Task<DocumentRequestFieldValuesResponse> PutFieldValuesAsync(
        Guid id,
        PutDocumentRequestFieldValuesRequest body,
        CancellationToken ct = default)
    {
        if (body?.Values is null)
            throw new DocumentationApiException(400, "values est obligatoire.");

        var r = await db.DocumentRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
            throw new DocumentationApiException(404, "Demande introuvable.");
        if (!CanActorEditFieldValues(r))
            throw new DocumentationApiException(403, "Accès refusé.");

        var old = await db.DocumentRequestFieldValues.Where(f => f.DocumentRequestId == id).ToListAsync(ct);
        if (old.Count > 0)
            db.DocumentRequestFieldValues.RemoveRange(old);

        var tenant = tenantAccessor.ResolvedTenantId;
        var now = DateTimeOffset.UtcNow;
        AddFieldValueRows(id, body.Values, tenant, now);
        await db.SaveChangesAsync(ct);

        var rows = await db.DocumentRequestFieldValues.AsNoTracking()
            .Where(f => f.DocumentRequestId == id)
            .ToListAsync(ct);
        return new DocumentRequestFieldValuesResponse { Values = ToFieldValuesDict(rows) };
    }

    public async Task<DocumentRequestResponse> CreateAsync(CreateDocumentRequestBody body, CancellationToken ct = default)
    {
        if (!userContext.UserId.HasValue)
            throw new DocumentationApiException(401, "Authentification requise.");

        var requesterId = userContext.UserId.Value;
        var tenant = tenantAccessor.ResolvedTenantId;

        if (body.RequesterUserId.HasValue && body.RequesterUserId.Value != Guid.Empty &&
            body.RequesterUserId.Value != requesterId)
            throw new DocumentationApiException(400, "requesterUserId ne correspond pas au contexte utilisateur.");

        Guid? documentTemplateId = null;
        DocumentTemplate? selectedTemplate = null;
        if (!string.IsNullOrWhiteSpace(body.DocumentTemplateId) && Guid.TryParse(body.DocumentTemplateId, out var tplGuid))
        {
            selectedTemplate = await db.DocumentTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tplGuid && t.TenantId == tenant, ct);
            if (selectedTemplate is null)
                throw new DocumentationApiException(400, "Modèle documentaire introuvable pour ce tenant.");
            if (!selectedTemplate.IsActive)
                throw new DocumentationApiException(400, "Ce modèle est inactif : choisissez un autre modèle.");
            documentTemplateId = selectedTemplate.Id;
        }

        Guid? documentTypeId = null;
        bool isCustomType;
        string? customTypeDescription;
        if (selectedTemplate is not null)
        {
            if (selectedTemplate.DocumentTypeId is { } tdt)
            {
                if (body.IsCustomType)
                    throw new DocumentationApiException(400, "Avec un modèle lié à un type catalogue, ne pas utiliser le mode « Autre ».");
                if (!await db.DocumentTypes.AnyAsync(t => t.Id == tdt && t.IsActive, ct))
                    throw new DocumentationApiException(400, "Le type catalogue lié au modèle est indisponible.");
                documentTypeId = tdt;
                isCustomType = false;
                customTypeDescription = null;
                if (!string.IsNullOrWhiteSpace(body.DocumentTypeId) && Guid.TryParse(body.DocumentTypeId, out var bodyDt) && bodyDt != tdt)
                    throw new DocumentationApiException(400, "documentTypeId ne correspond pas au modèle sélectionné.");
            }
            else
            {
                isCustomType = true;
                documentTypeId = null;
                customTypeDescription = string.IsNullOrWhiteSpace(body.CustomTypeDescription)
                    ? $"{selectedTemplate.Name} (modèle {selectedTemplate.Code})"
                    : body.CustomTypeDescription.Trim();
            }
        }
        else if (body.IsCustomType)
        {
            if (!string.IsNullOrWhiteSpace(body.DocumentTypeId))
                throw new DocumentationApiException(400, "Pour « Autre », ne pas envoyer documentTypeId.");
            if (string.IsNullOrWhiteSpace(body.CustomTypeDescription))
                throw new DocumentationApiException(400, "Description du type obligatoire pour « Autre ».");
            isCustomType = true;
            customTypeDescription = body.CustomTypeDescription.Trim();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(body.DocumentTypeId) || !Guid.TryParse(body.DocumentTypeId, out var dt) || dt == Guid.Empty)
                throw new DocumentationApiException(400, "documentTypeId invalide.");
            var exists = await db.DocumentTypes.AnyAsync(t => t.Id == dt && t.IsActive, ct);
            if (!exists)
                throw new DocumentationApiException(400, "Type de document inconnu ou inactif.");
            documentTypeId = dt;
            isCustomType = false;
            customTypeDescription = null;
        }

        var requesterRow = await db.DirectoryUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == requesterId, ct);
        DocumentRequest? entity = null;
        PostgresException? lastUniqueViolation = null;
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            entity = null;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var requestNumber = await DocumentRequestNumberingService.AllocateNextAsync(db, tenantAccessor.ResolvedTenantId, ct);
                var now = DateTimeOffset.UtcNow;
                var beneficiaryId =
                    body.BeneficiaryUserId is { } b && b != Guid.Empty ? b : requesterId;

                entity = new DocumentRequest
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantAccessor.ResolvedTenantId,
                    RequestNumber = requestNumber,
                    RequesterUserId = requesterId,
                    BeneficiaryUserId = beneficiaryId,
                    DocumentTypeId = isCustomType ? null : documentTypeId,
                    DocumentTemplateId = documentTemplateId,
                    IsCustomType = isCustomType,
                    CustomTypeDescription = isCustomType ? customTypeDescription : null,
                    Reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim(),
                    ComplementaryComments = string.IsNullOrWhiteSpace(body.ComplementaryComments) ? null : body.ComplementaryComments.Trim(),
                    Status = DocumentRequestStatus.Pending,
                    CreatedAt = now,
                    UpdatedAt = now,
                    OrganizationalUnitId = requesterRow?.DepartementId,
                };

                db.DocumentRequests.Add(entity);
                await db.SaveChangesAsync(ct);

                if (body.InitialFieldValues is { Count: > 0 })
                    AddFieldValueRows(entity.Id, body.InitialFieldValues, tenant, now);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresUniqueViolationSqlState)
            {
                lastUniqueViolation = pg;
                await tx.RollbackAsync(ct);
                if (entity is not null)
                    db.Entry(entity).State = EntityState.Detached;
                entity = null;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                throw new DocumentationApiException(503, $"Numérotation indisponible : {ex.Message}");
            }
        }

        if (entity is null)
            throw new DocumentationApiException(409, "Conflit d'unicité persistant. Réessayez dans quelques secondes.");

        logger.LogInformation(
            "CreateDocumentRequest success tenant={TenantId} actorUserId={ActorUserId} requestId={RequestId} requestNumber={RequestNumber} status={Status}",
            tenant,
            requesterId,
            entity.Id,
            entity.RequestNumber ?? "(none)",
            entity.Status.ToString());

        DocumentType? typeRow = null;
        if (!entity.IsCustomType && entity.DocumentTypeId.HasValue)
            typeRow = await db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == entity.DocumentTypeId.Value, ct);

        var displayNames = await DocumentRequestMappingHelper.LoadDisplayNamesAsync(
            db,
            new[] { entity.RequesterUserId, entity.BeneficiaryUserId ?? Guid.Empty },
            ct);
        string? templateName = null;
        if (entity.DocumentTemplateId is { } tidTpl)
        {
            templateName = await db.DocumentTemplates.AsNoTracking()
                .Where(t => t.Id == tidTpl)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct);
        }

        return DocumentRequestMapper.ToResponse(entity, typeRow, userContext, displayNames, null, templateName);
    }

    public async Task<bool> CanActorViewAsync(DocumentRequest r, CancellationToken ct = default)
    {
        if (!userContext.IsComplete || !userContext.Role.HasValue || !userContext.UserId.HasValue)
            return false;

        switch (userContext.Role.Value)
        {
            case AppRole.Rh:
            case AppRole.Admin:
            case AppRole.Audit:
                return true;
            case AppRole.Pilote:
                var uid = userContext.UserId.Value;
                return r.RequesterUserId == uid || r.BeneficiaryUserId == uid;
            case AppRole.Coach:
                var pilotIdsCoach = await db.DirectoryUsers.AsNoTracking()
                    .Where(u => u.Role == AppRole.Pilote && u.CoachId == userContext.UserId!.Value)
                    .Select(u => u.Id)
                    .ToListAsync(ct);
                return pilotIdsCoach.Contains(r.RequesterUserId) ||
                    (r.BeneficiaryUserId.HasValue && pilotIdsCoach.Contains(r.BeneficiaryUserId.Value));
            case AppRole.Manager:
            case AppRole.Rp:
                if (userContext.ScopeCoachId.HasValue)
                {
                    var pilotIdsScope = await db.DirectoryUsers.AsNoTracking()
                        .Where(u => u.Role == AppRole.Pilote && u.CoachId == userContext.ScopeCoachId)
                        .Select(u => u.Id)
                        .ToListAsync(ct);
                    return pilotIdsScope.Contains(r.RequesterUserId) ||
                        (r.BeneficiaryUserId.HasValue && pilotIdsScope.Contains(r.BeneficiaryUserId.Value));
                }

                if (userContext.ScopeManagerId.HasValue && !userContext.ScopeCoachId.HasValue &&
                    userContext.Role == AppRole.Rp)
                {
                    var coachIds = await db.DirectoryUsers.AsNoTracking()
                        .Where(u => u.Role == AppRole.Coach && u.ManagerId == userContext.ScopeManagerId)
                        .Select(u => u.Id)
                        .ToListAsync(ct);
                    var pilotIdsRp = await db.DirectoryUsers.AsNoTracking()
                        .Where(u =>
                            u.Role == AppRole.Pilote &&
                            u.CoachId.HasValue &&
                            coachIds.Contains(u.CoachId!.Value))
                        .Select(u => u.Id)
                        .ToListAsync(ct);
                    return pilotIdsRp.Contains(r.RequesterUserId) ||
                        (r.BeneficiaryUserId.HasValue && pilotIdsRp.Contains(r.BeneficiaryUserId.Value));
                }

                return true;
            default:
                return false;
        }
    }

    private bool CanActorEditFieldValues(DocumentRequest r)
    {
        if (!userContext.IsComplete || !userContext.Role.HasValue || !userContext.UserId.HasValue)
            return false;

        return userContext.Role.Value switch
        {
            AppRole.Rh or AppRole.Admin => true,
            AppRole.Pilote => r.RequesterUserId == userContext.UserId.Value ||
                r.BeneficiaryUserId == userContext.UserId.Value,
            _ => false,
        };
    }

    private void AddFieldValueRows(
        Guid requestId,
        IReadOnlyDictionary<string, string> values,
        string tenant,
        DateTimeOffset now)
    {
        var added = 0;
        foreach (var kv in values)
        {
            var key = (kv.Key ?? "").Trim();
            if (string.IsNullOrEmpty(key) || !IsValidVariableName(key))
                continue;
            if (added >= MaxTemplateVariables)
                break;
            db.DocumentRequestFieldValues.Add(new DocumentRequestFieldValue
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                DocumentRequestId = requestId,
                FieldName = key,
                FieldValue = kv.Value ?? "",
                UpdatedAt = now,
            });
            added++;
        }
    }

    private static Dictionary<string, string> ToFieldValuesDict(IReadOnlyList<DocumentRequestFieldValue> rows)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = row.FieldName.Trim();
            if (string.IsNullOrEmpty(key))
                continue;
            dict[key] = row.FieldValue ?? "";
        }
        return dict;
    }

    private static bool IsValidVariableName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static (Guid? FilterTypeId, bool? CatalogOnly, bool? CustomOnly) ParseTypeFilter(string? type)
    {
        var typeNorm = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        if (typeNorm is null)
            return (null, null, null);

        var tl = typeNorm.ToLowerInvariant();
        if (tl is "catalog" or "catalogue")
            return (null, true, null);
        if (tl is "custom" or "autre" or "other")
            return (null, null, true);
        if (Guid.TryParse(typeNorm, out var tid) && tid != Guid.Empty)
            return (tid, null, null);

        throw new DocumentationApiException(400, "type doit être catalog, custom ou un UUID de type de document.");
    }

    private static IQueryable<DocumentRequest> ApplyStandardDocumentRequestFilters(
        IQueryable<DocumentRequest> query,
        DocumentRequestStatus? statusFilter,
        Guid? filterTypeId,
        bool? catalogOnly,
        bool? customOnly)
    {
        if (statusFilter.HasValue)
            query = query.Where(r => r.Status == statusFilter.Value);
        if (catalogOnly == true)
            query = query.Where(r => !r.IsCustomType);
        if (customOnly == true)
            query = query.Where(r => r.IsCustomType);
        if (filterTypeId.HasValue)
            query = query.Where(r => r.DocumentTypeId == filterTypeId.Value);
        return query;
    }

    private async Task<PagedResponse<DocumentRequestResponse>> PaginateDocumentRequestsAsync(
        IQueryable<DocumentRequest> baseQuery,
        int page,
        int pageSize,
        RequestSortField sortField,
        bool desc,
        string tenantId,
        string logOperation,
        CancellationToken ct)
    {
        baseQuery = ApplyDocumentRequestSort(baseQuery, sortField, desc);

        var total = await baseQuery.CountAsync(ct);
        var rows = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var typeIds = rows.Where(r => r.DocumentTypeId.HasValue).Select(r => r.DocumentTypeId!.Value).Distinct().ToArray();
        var typeMap = typeIds.Length == 0
            ? new Dictionary<Guid, DocumentType>()
            : await db.DocumentTypes.AsNoTracking()
                .Where(t => typeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, ct);

        var nameIds = rows.SelectMany(r => new[] { r.RequesterUserId, r.BeneficiaryUserId ?? Guid.Empty }).ToArray();
        var displayNames = await DocumentRequestMappingHelper.LoadDisplayNamesAsync(db, nameIds, ct);
        var latestGens = await DocumentRequestMappingHelper.LoadLatestGeneratedByRequestIdsAsync(db, rows.Select(x => x.Id), ct);

        var tplIds = rows.Where(r => r.DocumentTemplateId.HasValue).Select(r => r.DocumentTemplateId!.Value).Distinct().ToArray();
        var tplNameMap = tplIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.DocumentTemplates.AsNoTracking()
                .Where(t => tplIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var items = rows.Select(r =>
        {
            DocumentType? typeRow = null;
            if (r.DocumentTypeId.HasValue && typeMap.TryGetValue(r.DocumentTypeId.Value, out var dt))
                typeRow = dt;
            latestGens.TryGetValue(r.Id, out var gen);
            string? tn = null;
            if (r.DocumentTemplateId is { } tId && tplNameMap.TryGetValue(tId, out var nm))
                tn = nm;
            return DocumentRequestMapper.ToResponse(r, typeRow, userContext, displayNames, gen, tn);
        }).ToList();

        logger.LogInformation(
            "{LogOperation} result tenant={TenantId} returned={ReturnedCount} total={TotalCount} page={Page} pageSize={PageSize}",
            logOperation,
            tenantId,
            items.Count,
            total,
            page,
            pageSize);

        return new PagedResponse<DocumentRequestResponse>(items, total, page, pageSize);
    }

    private enum RequestSortField
    {
        CreatedAt,
        Status,
        RequestNumber,
    }

    private static bool TryParseSortOrder(string? sortOrder, out bool descending)
    {
        descending = true;
        if (string.IsNullOrWhiteSpace(sortOrder))
            return true;

        var s = sortOrder.Trim().ToLowerInvariant();
        if (s == "asc")
        {
            descending = false;
            return true;
        }

        return s == "desc";
    }

    private static bool TryParseRequestSortField(string? sortBy, out RequestSortField field)
    {
        field = RequestSortField.CreatedAt;
        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        switch (sortBy.Trim().ToLowerInvariant())
        {
            case "createdat":
                field = RequestSortField.CreatedAt;
                return true;
            case "status":
                field = RequestSortField.Status;
                return true;
            case "requestnumber":
                field = RequestSortField.RequestNumber;
                return true;
            default:
                return false;
        }
    }

    private static IQueryable<DocumentRequest> ApplyDocumentRequestSort(IQueryable<DocumentRequest> q, RequestSortField sortField, bool desc) =>
        sortField switch
        {
            RequestSortField.Status => desc
                ? q.OrderByDescending(r => r.Status).ThenByDescending(r => r.CreatedAt)
                : q.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedAt),
            RequestSortField.RequestNumber => desc
                ? q.OrderByDescending(r => r.RequestNumber).ThenByDescending(r => r.CreatedAt)
                : q.OrderBy(r => r.RequestNumber).ThenByDescending(r => r.CreatedAt),
            _ => desc ? q.OrderByDescending(r => r.CreatedAt) : q.OrderBy(r => r.CreatedAt),
        };
}
