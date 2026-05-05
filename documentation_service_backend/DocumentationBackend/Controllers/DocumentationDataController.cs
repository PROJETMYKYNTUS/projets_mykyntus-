using System.Data;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentationBackend.Api;
using DocumentationBackend.Application.Abstractions;
using DocumentationBackend.Application.DocumentTemplates;
using DocumentationBackend.Configuration;
using DocumentationBackend.Context;
using DocumentationBackend.Data;
using DocumentationBackend.Data.Entities;
using DocumentationBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DocumentationBackend.Controllers;

/// <summary>
/// Données métier PostgreSQL (schéma documentation). Pas de mock : lecture/écriture réelle.
/// Le contexte utilisateur est injecté par en-têtes (voir <see cref="DocumentationUserContextMiddleware"/>).
/// </summary>
[ApiController]
[Route("api/documentation/data")]
public class DocumentationDataController(
    DocumentationDbContext db,
    DocumentationUserContext userContext,
    IDocumentationTenantAccessor tenantAccessor,
    DocumentationWorkflowService workflow,
    ITemplateEngineService templateEngine,
    ITemplatePlaceholderNormalizationService placeholderNormalization,
    IOriginalDocxTemplateRenderService originalDocxTemplateRender,
    IPdfExportService pdfExport,
    IDocumentTemplateManagementService templateManagement,
    ITemplateBlobStorage templateBlobStorage,
    IAiTemplateContentGenerator aiTemplateGenerator,
    AiDirectDocumentFillOrchestrator aiDirectOrchestrator,
    IOptions<DocumentWorkflowOptions> documentWorkflowOptions,
    IOptions<AiTemplateOptions> aiTemplateOptions,
    IRibValidationService ribValidation,
    ILogger<DocumentationDataController> logger) : ControllerBase
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const int MaxTemplateContentLength = 100_000;
    private const int MaxTemplateVariables = 100;
    private static readonly Regex LegacyMissingMarkerRegex = new(
        @"\(\s*X+\s*\)|\(\s*\)|\(\s+\)|(?<![A-Za-z0-9])X(?![A-Za-z0-9])|_{1,}|(?<![A-Za-z0-9])(?:-|—)(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Libellé normalisé pour filtrer <c>unit_type</c> (comparaison en minuscules côté requête).</summary>
    private const string OrgUnitTypePole = "pole";

    private const string OrgUnitTypeCellule = "cellule";

    private const string OrgUnitTypeDepartement = "departement";

    private static string TemplateKindToApi(DocumentTemplateKind k) =>
        k == DocumentTemplateKind.Static ? "static" : "dynamic";

    private static DocumentTemplateKind ParseTemplateKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DocumentTemplateKind.Dynamic;
        return raw.Trim().ToLowerInvariant() switch
        {
            "static" => DocumentTemplateKind.Static,
            "dynamic" => DocumentTemplateKind.Dynamic,
            _ => DocumentTemplateKind.Dynamic,
        };
    }

    [HttpGet("document-types")]
    public async Task<ActionResult<IReadOnlyList<DocumentTypeResponse>>> GetDocumentTypes(CancellationToken ct)
    {
        var rows = await db.DocumentTypes
            .AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new DocumentTypeResponse(
                t.Id.ToString(),
                t.Name,
                t.Code,
                t.Description ?? "",
                t.DepartmentCode ?? "",
                t.RetentionDays,
                t.WorkflowId.HasValue ? t.WorkflowId.Value.ToString() : "",
                t.IsMandatory))
            .ToListAsync(ct);

        return rows;
    }

    /// <summary>
    /// Demandes paginées du tenant courant. Total 0 → 200 avec page vide. Erreurs SQL/mapping → middleware global.
    /// </summary>
    [HttpGet("document-requests")]
    public async Task<ActionResult<PagedResponse<DocumentRequestResponse>>> GetDocumentRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? role = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default)
    {
        var tenant = tenantAccessor.ResolvedTenantId;
        logger.LogInformation(
            "GetDocumentRequests start tenant={TenantId} actorRole={Role} actorUserId={UserId} statusFilter={Status} roleFilter={RoleFilter}",
            tenant,
            userContext.Role?.ToString() ?? "unknown",
            userContext.UserId?.ToString() ?? "unknown",
            status ?? "(none)",
            role ?? "(none)");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!TryParseSortOrder(sortOrder, out var desc))
            return BadRequest(new { message = "sortOrder doit être « asc » ou « desc »." });

        DocumentRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DocumentRequestStatus>(status.Trim(), ignoreCase: true, out var st))
                return BadRequest(new { message = "status invalide (pending, approved, rejected, generated, cancelled)." });
            statusFilter = st;
        }

        AppRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!AppRoleHeaderParser.TryParse(role, out var rf))
                return BadRequest(new { message = "role de filtre invalide (pilote, coach, manager, rp, rh, admin, audit)." });
            roleFilter = rf;
        }

        string? typeNorm = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        Guid? filterTypeId = null;
        bool? catalogOnly = null;
        bool? customOnly = null;
        if (typeNorm is not null)
        {
            var tl = typeNorm.ToLowerInvariant();
            if (tl is "catalog" or "catalogue")
                catalogOnly = true;
            else if (tl is "custom" or "autre" or "other")
                customOnly = true;
            else if (Guid.TryParse(typeNorm, out var tid) && tid != Guid.Empty)
                filterTypeId = tid;
            else
                return BadRequest(new { message = "type doit être catalog, custom ou un UUID de type de document." });
        }

        if (!TryParseRequestSortField(sortBy, out var sortField))
            return BadRequest(new { message = "sortBy doit être createdAt, status ou requestNumber." });

        // Pas de Include ici : évite les jointures EF/Npgsql problématiques (traduction SQL, enums) sur certaines bases.
        var baseQuery = db.DocumentRequests.AsNoTracking();
        baseQuery = ApplyStandardDocumentRequestFilters(baseQuery, statusFilter, filterTypeId, catalogOnly, customOnly);

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

        // Visibilité par rôle (toujours dans le tenant courant — filtre EF global).
        // RH / Admin / Audit : toutes les demandes du locataire (aucun filtre supplémentaire ici).
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

        // Périmètre hiérarchique (manager / RP) : demandes des pilotes encadrés par le coach choisi.
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

        return await PaginateDocumentRequestsAsync(
            baseQuery,
            page,
            pageSize,
            sortField,
            desc,
            tenant,
            nameof(GetDocumentRequests),
            ct);
    }

    /// <summary>
    /// Demandes dont l’utilisateur courant est le demandeur (<c>RequesterUserId</c>) — « Suivi des demandes » côté pilote.
    /// </summary>
    [HttpGet("document-requests/my-requests")]
    public async Task<ActionResult<PagedResponse<DocumentRequestResponse>>> GetMyDocumentRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();

        var tenant = tenantAccessor.ResolvedTenantId;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!TryParseSortOrder(sortOrder, out var desc))
            return BadRequest(new { message = "sortOrder doit être « asc » ou « desc »." });

        DocumentRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DocumentRequestStatus>(status.Trim(), ignoreCase: true, out var st))
                return BadRequest(new { message = "status invalide (pending, approved, rejected, generated, cancelled)." });
            statusFilter = st;
        }

        string? typeNorm = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        Guid? filterTypeId = null;
        bool? catalogOnly = null;
        bool? customOnly = null;
        if (typeNorm is not null)
        {
            var tl = typeNorm.ToLowerInvariant();
            if (tl is "catalog" or "catalogue")
                catalogOnly = true;
            else if (tl is "custom" or "autre" or "other")
                customOnly = true;
            else if (Guid.TryParse(typeNorm, out var tid) && tid != Guid.Empty)
                filterTypeId = tid;
            else
                return BadRequest(new { message = "type doit être catalog, custom ou un UUID de type de document." });
        }

        if (!TryParseRequestSortField(sortBy, out var sortField))
            return BadRequest(new { message = "sortBy doit être createdAt, status ou requestNumber." });

        var uid = userContext.UserId.Value;
        var baseQuery = db.DocumentRequests.AsNoTracking().Where(r => r.RequesterUserId == uid);
        baseQuery = ApplyStandardDocumentRequestFilters(baseQuery, statusFilter, filterTypeId, catalogOnly, customOnly);

        return await PaginateDocumentRequestsAsync(
            baseQuery,
            page,
            pageSize,
            sortField,
            desc,
            tenant,
            nameof(GetMyDocumentRequests),
            ct);
    }

    /// <summary>
    /// Demandes dont l’utilisateur courant est le bénéficiaire (<c>BeneficiaryUserId</c>) — « Mes documents » (tous statuts).
    /// </summary>
    [HttpGet("document-requests/assigned-to-me")]
    public async Task<ActionResult<PagedResponse<DocumentRequestResponse>>> GetDocumentRequestsAssignedToMe(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();

        var tenant = tenantAccessor.ResolvedTenantId;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!TryParseSortOrder(sortOrder, out var desc))
            return BadRequest(new { message = "sortOrder doit être « asc » ou « desc »." });

        DocumentRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DocumentRequestStatus>(status.Trim(), ignoreCase: true, out var st))
                return BadRequest(new { message = "status invalide (pending, approved, rejected, generated, cancelled)." });
            statusFilter = st;
        }

        string? typeNorm = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        Guid? filterTypeId = null;
        bool? catalogOnly = null;
        bool? customOnly = null;
        if (typeNorm is not null)
        {
            var tl = typeNorm.ToLowerInvariant();
            if (tl is "catalog" or "catalogue")
                catalogOnly = true;
            else if (tl is "custom" or "autre" or "other")
                customOnly = true;
            else if (Guid.TryParse(typeNorm, out var tid) && tid != Guid.Empty)
                filterTypeId = tid;
            else
                return BadRequest(new { message = "type doit être catalog, custom ou un UUID de type de document." });
        }

        if (!TryParseRequestSortField(sortBy, out var sortField))
            return BadRequest(new { message = "sortBy doit être createdAt, status ou requestNumber." });

        var uid = userContext.UserId.Value;
        // Bénéficiaire explicite, ou anciennes lignes sans bénéficiaire : le demandeur est la personne concernée.
        var baseQuery = db.DocumentRequests.AsNoTracking().Where(r =>
            r.BeneficiaryUserId == uid
            || (!r.BeneficiaryUserId.HasValue && r.RequesterUserId == uid));
        baseQuery = ApplyStandardDocumentRequestFilters(baseQuery, statusFilter, filterTypeId, catalogOnly, customOnly);

        return await PaginateDocumentRequestsAsync(
            baseQuery,
            page,
            pageSize,
            sortField,
            desc,
            tenant,
            nameof(GetDocumentRequestsAssignedToMe),
            ct);
    }

    [HttpGet("document-requests/{id:guid}")]
    public async Task<ActionResult<DocumentRequestResponse>> GetDocumentRequest(Guid id, CancellationToken ct)
    {
        var r = await db.DocumentRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
            return NotFound();

        if (!await CanActorViewDocumentRequestAsync(r, ct))
            return NotFound();

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

    [HttpGet("document-requests/{id:guid}/field-values")]
    public async Task<ActionResult<DocumentRequestFieldValuesResponse>> GetDocumentRequestFieldValues(Guid id, CancellationToken ct)
    {
        var r = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
            return NotFound();
        if (!await CanActorViewDocumentRequestAsync(r, ct))
            return NotFound();

        var rows = await db.DocumentRequestFieldValues.AsNoTracking()
            .Where(f => f.DocumentRequestId == id)
            .ToListAsync(ct);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = row.FieldName.Trim();
            if (string.IsNullOrEmpty(key))
                continue;
            dict[key] = row.FieldValue ?? "";
        }

        return Ok(new DocumentRequestFieldValuesResponse { Values = dict });
    }

    [HttpPut("document-requests/{id:guid}/field-values")]
    public async Task<ActionResult<DocumentRequestFieldValuesResponse>> PutDocumentRequestFieldValues(
        Guid id,
        [FromBody] PutDocumentRequestFieldValuesRequest body,
        CancellationToken ct)
    {
        if (body?.Values is null)
            return BadRequest(new { message = "values est obligatoire." });

        var r = await db.DocumentRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
            return NotFound();
        if (!CanActorEditDocumentRequestFieldValues(r))
            return Forbid();

        var old = await db.DocumentRequestFieldValues.Where(f => f.DocumentRequestId == id).ToListAsync(ct);
        if (old.Count > 0)
            db.DocumentRequestFieldValues.RemoveRange(old);

        var tenant = tenantAccessor.ResolvedTenantId;
        var now = DateTimeOffset.UtcNow;
        var added = 0;
        foreach (var kv in body.Values)
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
                DocumentRequestId = id,
                FieldName = key,
                FieldValue = kv.Value ?? "",
                UpdatedAt = now,
            });
            added++;
        }

        await db.SaveChangesAsync(ct);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in await db.DocumentRequestFieldValues.AsNoTracking()
                     .Where(f => f.DocumentRequestId == id)
                     .ToListAsync(ct))
        {
            var key = row.FieldName.Trim();
            if (string.IsNullOrEmpty(key))
                continue;
            dict[key] = row.FieldValue ?? "";
        }

        return Ok(new DocumentRequestFieldValuesResponse { Values = dict });
    }

    [HttpPost("document-requests")]
    public async Task<ActionResult<DocumentRequestResponse>> CreateDocumentRequest(
        [FromBody] CreateDocumentRequestBody body,
        CancellationToken ct)
    {
        var requesterId = userContext.UserId!.Value;
        var tenant = tenantAccessor.ResolvedTenantId;

        if (body.RequesterUserId.HasValue && body.RequesterUserId.Value != Guid.Empty &&
            body.RequesterUserId.Value != requesterId)
            return BadRequest(new { message = "requesterUserId ne correspond pas au contexte utilisateur." });

        Guid? documentTemplateId = null;
        DocumentTemplate? selectedTemplate = null;
        if (!string.IsNullOrWhiteSpace(body.DocumentTemplateId) && Guid.TryParse(body.DocumentTemplateId, out var tplGuid))
        {
            selectedTemplate = await db.DocumentTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tplGuid && t.TenantId == tenant, ct);
            if (selectedTemplate is null)
                return BadRequest(new { message = "Modèle documentaire introuvable pour ce tenant." });
            if (!selectedTemplate.IsActive)
                return BadRequest(new { message = "Ce modèle est inactif : choisissez un autre modèle." });
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
                    return BadRequest(new { message = "Avec un modèle lié à un type catalogue, ne pas utiliser le mode « Autre »." });
                if (!await db.DocumentTypes.AnyAsync(t => t.Id == tdt && t.IsActive, ct))
                    return BadRequest(new { message = "Le type catalogue lié au modèle est indisponible." });
                documentTypeId = tdt;
                isCustomType = false;
                customTypeDescription = null;
                if (!string.IsNullOrWhiteSpace(body.DocumentTypeId) && Guid.TryParse(body.DocumentTypeId, out var bodyDt) && bodyDt != tdt)
                    return BadRequest(new { message = "documentTypeId ne correspond pas au modèle sélectionné." });
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
                return BadRequest(new { message = "Pour « Autre », ne pas envoyer documentTypeId." });
            if (string.IsNullOrWhiteSpace(body.CustomTypeDescription))
                return BadRequest(new { message = "Description du type obligatoire pour « Autre »." });
            isCustomType = true;
            customTypeDescription = body.CustomTypeDescription.Trim();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(body.DocumentTypeId) || !Guid.TryParse(body.DocumentTypeId, out var dt) || dt == Guid.Empty)
                return BadRequest(new { message = "documentTypeId invalide." });
            var exists = await db.DocumentTypes.AnyAsync(t => t.Id == dt && t.IsActive, ct);
            if (!exists)
                return BadRequest(new { message = "Type de document inconnu ou inactif." });
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
                // Sans bénéficiaire explicite, la demande concerne le demandeur (cohérence suivi / « Mes documents »).
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
                    AddDocumentRequestFieldValueRows(entity.Id, body.InitialFieldValues, tenant, now);

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
                return Problem(detail: ex.Message, title: "Numérotation indisponible");
            }
        }

        if (entity is null)
        {
            return Conflict(new
            {
                message = "Conflit d'unicité persistant. Réessayez dans quelques secondes.",
                constraint = lastUniqueViolation?.ConstraintName,
            });
        }

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

        return Ok(DocumentRequestMapper.ToResponse(entity, typeRow, userContext, displayNames, null, templateName));
    }

    /// <summary>Alias REST (PUT) — même logique que <c>POST /workflow/validate</c>.</summary>
    [HttpPut("document-requests/{id:guid}/validate")]
    public async Task<ActionResult<DocumentRequestResponse>> PutValidateDocumentRequest(
        Guid id,
        [FromBody] WorkflowValidatePutBody? body,
        CancellationToken ct)
    {
        logger.LogInformation("PutValidateDocumentRequest requestId={RequestId} tenant={TenantId}", id, tenantAccessor.ResolvedTenantId);
        var (res, code, err) = await workflow.ValidateAsync(id, body?.Comment, ct);
        return MapWorkflowResult(code, res, err);
    }

    /// <summary>Alias REST (PUT) — même logique que <c>POST /workflow/approve</c>.</summary>
    [HttpPut("document-requests/{id:guid}/approve")]
    public async Task<ActionResult<DocumentRequestResponse>> PutApproveDocumentRequest(Guid id, CancellationToken ct)
    {
        logger.LogInformation("PutApproveDocumentRequest requestId={RequestId} tenant={TenantId}", id, tenantAccessor.ResolvedTenantId);
        var (res, code, err) = await workflow.ApproveAsync(id, ct);
        return MapWorkflowResult(code, res, err);
    }

    /// <summary>Alias REST (PUT) — même logique que <c>POST /workflow/reject</c>.</summary>
    [HttpPut("document-requests/{id:guid}/reject")]
    public async Task<ActionResult<DocumentRequestResponse>> PutRejectDocumentRequest(
        Guid id,
        [FromBody] WorkflowRejectPutBody body,
        CancellationToken ct)
    {
        logger.LogInformation("PutRejectDocumentRequest requestId={RequestId} tenant={TenantId}", id, tenantAccessor.ResolvedTenantId);
        var (res, code, err) = await workflow.RejectAsync(id, body.RejectionReason ?? "", ct);
        return MapWorkflowResult(code, res, err);
    }

    private ActionResult<DocumentRequestResponse> MapWorkflowResult(int code, DocumentRequestResponse? res, string? err) =>
        code switch
        {
            StatusCodes.Status200OK => Ok(res!),
            StatusCodes.Status404NotFound => NotFound(new { message = err ?? "Demande introuvable." }),
            StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = err }),
            StatusCodes.Status400BadRequest => BadRequest(new { message = err }),
            StatusCodes.Status409Conflict => Conflict(new { message = err }),
            _ => StatusCode(code, new { message = err }),
        };

    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null,
        [FromQuery] string? role = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        if (!TryParseSortOrder(sortOrder, out var desc))
            return BadRequest(new { message = "sortOrder doit être « asc » ou « desc »." });

        AppRole? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!AppRoleHeaderParser.TryParse(role, out var rf))
                return BadRequest(new { message = "role de filtre invalide (pilote, coach, manager, rp, rh, admin, audit)." });
            roleFilter = rf;
        }

        if (!TryParseAuditSortField(sortBy, out var sortField))
            return BadRequest(new { message = "sortBy doit être occurredAt ou action." });

        var query = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var a = action.Trim();
            query = query.Where(x => x.Action.Contains(a));
        }

        if (roleFilter.HasValue)
        {
            var actorIdsForRole = await db.DirectoryUsers.AsNoTracking()
                .Where(u => u.Role == roleFilter.Value)
                .Select(u => u.Id)
                .ToListAsync(ct);
            query = actorIdsForRole.Count > 0
                ? query.Where(x => x.ActorUserId.HasValue && actorIdsForRole.Contains(x.ActorUserId.Value))
                : query.Where(static x => false);
        }

        query = ApplyAuditSort(query, sortField, desc);

        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var auditActorIds = rows.Where(a => a.ActorUserId.HasValue).Select(a => a.ActorUserId!.Value).ToArray();
        var auditNames = await DocumentRequestMappingHelper.LoadDisplayNamesAsync(db, auditActorIds, ct);

        var items = rows.Select(a => new AuditLogResponse(
            a.Id.ToString(),
            a.OccurredAt.ToString("O"),
            a.ActorUserId.HasValue ? DocumentRequestMappingHelper.ResolveName(auditNames, a.ActorUserId.Value) : null,
            a.ActorUserId.HasValue ? a.ActorUserId.Value.ToString() : null,
            a.Action,
            a.EntityType,
            a.EntityId.HasValue ? a.EntityId.Value.ToString() : null,
            a.Success,
            a.ErrorMessage,
            a.CorrelationId.HasValue ? a.CorrelationId.Value.ToString() : null)).ToList();

        return new PagedResponse<AuditLogResponse>(items, total, page, pageSize);
    }

    [HttpGet("document-templates")]
    public async Task<ActionResult<IReadOnlyList<DocumentTemplateListItemResponse>>> GetDocumentTemplates(CancellationToken ct)
    {
        // Projection : ne pas charger structured_content (jsonb) — évite charge inutile et certains soucis Npgsql/EF sur le jsonb.
        var rows = await db.DocumentTemplates.AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.Name,
                t.Source,
                t.IsActive,
                t.DocumentTypeId,
                DocumentTypeName = t.DocumentType != null ? t.DocumentType.Name : null,
                t.CurrentVersionId,
                CurrentVersionNumber = t.CurrentVersion != null ? (int?)t.CurrentVersion.VersionNumber : null,
                t.UpdatedAt,
                t.Description,
                t.Kind,
                t.RequiresPilotUpload,
                FileUrl = t.CurrentVersion != null ? t.CurrentVersion.OriginalAssetUri : null,
                CreatedAt = t.CurrentVersion != null ? (DateTimeOffset?)t.CurrentVersion.CreatedAt : null,
            })
            .ToListAsync(ct);
        var templateIds = rows.Select(t => t.Id).ToArray();
        Dictionary<Guid, List<string>> variableNamesByTemplate = new();
        if (templateIds.Length > 0)
        {
            var varRows = await db.DocumentTemplateVariables.AsNoTracking()
                .Where(v => templateIds.Contains(v.TemplateId))
                .OrderBy(v => v.TemplateId)
                .ThenBy(v => v.SortOrder)
                .Select(v => new { v.TemplateId, v.VariableName })
                .ToListAsync(ct);
            foreach (var g in varRows.GroupBy(x => x.TemplateId))
                variableNamesByTemplate[g.Key] = g.Select(x => x.VariableName).ToList();
        }

        var list = rows.Select(t => new DocumentTemplateListItemResponse(
            t.Id.ToString(),
            t.Code,
            t.Name,
            t.Source,
            TemplateKindToApi(t.Kind),
            t.RequiresPilotUpload,
            t.IsActive,
            t.DocumentTypeId?.ToString(),
            t.DocumentTypeName,
            variableNamesByTemplate.GetValueOrDefault(t.Id, new List<string>()),
            t.CurrentVersionId?.ToString(),
            t.CurrentVersionNumber,
            t.UpdatedAt.ToString("O"),
            t.Description,
            t.FileUrl,
            t.CreatedAt?.ToString("O"))).ToList();
        return Ok(list);
    }

    [HttpGet("document-templates/{id:guid}")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> GetDocumentTemplate(Guid id, CancellationToken ct)
    {
        var template = await db.DocumentTemplates
            .AsNoTracking()
            .Include(t => t.DocumentType)
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();

        var cv = template.CurrentVersion ?? await db.DocumentTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        DocumentTemplateVersionResponse? versionDto = null;
        if (cv is not null)
        {
            var vars = await db.DocumentTemplateVariables.AsNoTracking()
                .Where(v => v.TemplateVersionId == cv.Id)
                .OrderBy(v => v.SortOrder)
                .Select(v => new DocumentTemplateVariableResponse(
                    v.Id.ToString(),
                    v.VariableName,
                    v.VariableType,
                    v.IsRequired,
                    v.DefaultValue,
                    v.ValidationRule,
                    v.DisplayLabel,
                    NormalizeFormScope(v.FormScope),
                    v.SourcePriority,
                    v.NormalizedName,
                    v.RawPlaceholder,
                    v.SortOrder))
                .ToListAsync(ct);
            versionDto = new DocumentTemplateVersionResponse(
                cv.Id.ToString(),
                cv.VersionNumber,
                cv.Status,
                SanitizeForJson(cv.StructuredContent),
                cv.OriginalAssetUri,
                cv.CreatedAt.ToString("O"),
                cv.PublishedAt?.ToString("O"),
                vars);
        }

        return Ok(new DocumentTemplateDetailResponse(
            template.Id.ToString(),
            template.Code,
            template.Name,
            template.Source,
            TemplateKindToApi(template.Kind),
            template.RequiresPilotUpload,
            template.IsActive,
            template.DocumentTypeId?.ToString(),
            template.DocumentType?.Name,
            template.UpdatedAt.ToString("O"),
            template.Description,
            versionDto));
    }

    /// <summary>
    /// URL GET signée (court délai) pour ouvrir le fichier source MinIO/S3 depuis le navigateur (bucket privé → pas d’accès anonyme direct).
    /// </summary>
    [HttpGet("document-templates/{id:guid}/template-file-url")]
    public async Task<ActionResult<TemplateSourceFileUrlResponse>> GetTemplateSourceFileUrl(Guid id, CancellationToken ct)
    {
        var template = await db.DocumentTemplates
            .AsNoTracking()
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();

        var cv = template.CurrentVersion ?? await db.DocumentTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (cv is null || string.IsNullOrWhiteSpace(cv.OriginalAssetUri))
            return NotFound(new { message = "Aucun fichier source stocké pour ce modèle." });

        if (!templateBlobStorage.IsConfigured)
            return BadRequest(new { message = "MinIO / S3 n’est pas configuré (DocumentTemplates:Minio)." });

        var lifetime = TimeSpan.FromMinutes(15);
        var signed = templateBlobStorage.TryGetPresignedGetUrl(cv.OriginalAssetUri, lifetime);
        if (string.IsNullOrEmpty(signed))
            return BadRequest(new
            {
                message =
                    "Impossible de signer l’URL du fichier (URI source non reconnue pour ce bucket ou autre hôte). " +
                    "Vérifiez DocumentTemplates:Minio:Bucket et que l’URL en base correspond au format path-style du dépôt.",
            });

        return Ok(new TemplateSourceFileUrlResponse(signed, DateTimeOffset.UtcNow.Add(lifetime).ToString("O")));
    }

    /// <summary>
    /// Fichier binaire (PDF, DOCX, …) servi par l’API pour prévisualisation dans l’UI (évite CORS / AccessDenied sur MinIO).
    /// </summary>
    [HttpGet("document-templates/{id:guid}/template-file")]
    public async Task<IActionResult> GetTemplateSourceFile(Guid id, CancellationToken ct)
    {
        var template = await db.DocumentTemplates
            .AsNoTracking()
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();

        var cv = template.CurrentVersion ?? await db.DocumentTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (cv is null || string.IsNullOrWhiteSpace(cv.OriginalAssetUri))
            return NotFound(new { message = "Aucun fichier source stocké pour ce modèle." });

        if (!templateBlobStorage.IsConfigured)
            return BadRequest(new { message = "MinIO / S3 n’est pas configuré (DocumentTemplates:Minio)." });

        var payload = await templateBlobStorage.TryReadObjectAsync(cv.OriginalAssetUri, ct);
        if (payload is null)
            return NotFound(new { message = "Fichier introuvable dans MinIO ou trop volumineux (limite 52 Mo)." });

        var star = Uri.EscapeDataString(payload.FileName);
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"file\"; filename*=UTF-8''{star}");
        return File(payload.Content, payload.ContentType);
    }

    [HttpPost("document-templates")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> CreateDocumentTemplate(
        [FromBody] CreateDocumentTemplateRequest body,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Code) || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { message = "Code et nom du template sont obligatoires." });
        if (!IsValidTemplateCode(body.Code))
            return BadRequest(new { message = "Le code template doit contenir uniquement lettres/chiffres/souligné/tiret." });
        var normalizedSource = NormalizeSource(body.Source);
        var kind = ParseTemplateKind(body.Kind);
        if (normalizedSource is "AI_GENERATED" or "RULE_BASED")
            kind = DocumentTemplateKind.Dynamic;

        var structuredToSave = body.StructuredContent ?? "";
        IReadOnlyList<TemplateVariableInput> variablesToSave = body.Variables;

        if (kind == DocumentTemplateKind.Static)
        {
            if (string.IsNullOrWhiteSpace(body.OriginalAssetUri))
                return BadRequest(new { message = "originalAssetUri est obligatoire pour un modèle statique (fichier MinIO / S3)." });
            if (string.IsNullOrWhiteSpace(structuredToSave))
                structuredToSave = "{}";
            variablesToSave = Array.Empty<TemplateVariableInput>();
        }
        else
        {
            if (normalizedSource == "AI_GENERATED" && string.IsNullOrWhiteSpace(structuredToSave))
                return BadRequest(new { message = "Le contenu structuré est obligatoire pour un template généré par IA." });
            if (normalizedSource == "UPLOAD" && string.IsNullOrWhiteSpace(body.OriginalAssetUri))
                return BadRequest(new { message = "L’URL ou le chemin du fichier (originalAssetUri) est obligatoire pour un template uploadé." });
        }

        if (structuredToSave.Length > MaxTemplateContentLength)
            return BadRequest(new { message = "Contenu template trop volumineux." });

        var now = DateTimeOffset.UtcNow;
        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantAccessor.ResolvedTenantId,
            Code = body.Code.Trim().ToUpperInvariant(),
            Name = body.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
            Source = normalizedSource,
            Kind = kind,
            RequiresPilotUpload = body.RequiresPilotUpload,
            IsActive = true,
            DocumentTypeId = body.DocumentTypeId,
            UpdatedAt = now,
        };
        db.DocumentTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        var version = await CreateTemplateVersionInternalAsync(
            template,
            structuredToSave,
            "published",
            body.OriginalAssetUri,
            variablesToSave,
            userContext.UserId,
            ct);

        template.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(ct);
        return await GetDocumentTemplate(template.Id, ct);
    }

    [HttpPut("document-templates/{id:guid}")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> UpdateDocumentTemplate(
        Guid id,
        [FromBody] UpdateDocumentTemplateRequest body,
        CancellationToken ct)
    {
        var template = await db.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(body.Name))
            template.Name = body.Name.Trim();
        if (body.Description is not null)
            template.Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        template.DocumentTypeId = body.DocumentTypeId;
        if (body.RequiresPilotUpload.HasValue)
            template.RequiresPilotUpload = body.RequiresPilotUpload.Value;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetDocumentTemplate(id, ct);
    }

    [HttpPatch("document-templates/{id:guid}/status")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> UpdateDocumentTemplateStatus(
        Guid id,
        [FromBody] UpdateTemplateStatusRequest body,
        CancellationToken ct)
    {
        var template = await db.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();

        template.IsActive = body.IsActive;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetDocumentTemplate(id, ct);
    }

    [HttpDelete("document-templates/{id:guid}")]
    public async Task<IActionResult> DeleteDocumentTemplate(Guid id, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var template = await db.DocumentTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound(new { message = "Template introuvable." });

        var linkedRequests = await db.DocumentRequests
            .Where(r => r.DocumentTemplateId == id)
            .ToListAsync(ct);
        if (linkedRequests.Count > 0)
        {
            var blocked = linkedRequests
                .Where(r => r.Status is not (DocumentRequestStatus.Rejected or DocumentRequestStatus.Cancelled))
                .ToList();
            if (blocked.Count > 0)
            {
                return BadRequest(new
                {
                    message = $"Suppression refusée : template lié à {blocked.Count} demande(s) active(s) (pending/approved/generated)."
                });
            }
        }

        var versionIds = template.Versions.Select(v => v.Id).ToArray();
        if (versionIds.Length > 0)
        {
            var usedInGenerated = await db.GeneratedDocuments.AsNoTracking()
                .AnyAsync(g => g.TemplateVersionId.HasValue && versionIds.Contains(g.TemplateVersionId.Value), ct);
            if (usedInGenerated)
                return BadRequest(new { message = "Suppression refusée : ce template possède déjà des documents générés." });
        }

        if (linkedRequests.Count > 0)
        {
            var requestIds = linkedRequests.Select(r => r.Id).ToArray();
            var fieldRows = await db.DocumentRequestFieldValues
                .Where(f => requestIds.Contains(f.DocumentRequestId))
                .ToListAsync(ct);
            if (fieldRows.Count > 0)
                db.DocumentRequestFieldValues.RemoveRange(fieldRows);

            var now = DateTimeOffset.UtcNow;
            foreach (var req in linkedRequests)
            {
                req.DocumentTemplateId = null;
                req.UpdatedAt = now;
            }
        }

        db.DocumentTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("document-templates/upload")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> UploadTemplate(CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Fichier « file » requis (multipart/form-data)." });
            var code = form["code"].ToString();
            var name = form["name"].ToString();
            var description = string.IsNullOrWhiteSpace(form["description"].ToString()) ? null : form["description"].ToString();
            Guid? documentTypeId = null;
            if (Guid.TryParse(form["documentTypeId"].ToString(), out var dt))
                documentTypeId = dt;
            var asStatic = string.Equals(form["kind"].ToString(), "static", StringComparison.OrdinalIgnoreCase);
            var requiresPilotUpload = string.Equals(form["requiresPilotUpload"].ToString(), "true", StringComparison.OrdinalIgnoreCase)
                || form["requiresPilotUpload"].ToString() == "1"
                || string.Equals(form["requiresPilotUpload"].ToString(), "on", StringComparison.OrdinalIgnoreCase);
            try
            {
                var created = await templateManagement.CreateFromUploadedFileAsync(
                    userContext.UserId.Value,
                    code,
                    name,
                    description,
                    documentTypeId,
                    file,
                    staticDocument: asStatic,
                    requiresPilotUpload: requiresPilotUpload,
                    cancellationToken: ct);
                return Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return MapTemplateUploadException(ex);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        UploadTemplateRequest body;
        try
        {
            using var sr = new StreamReader(Request.Body);
            var json = await sr.ReadToEndAsync(ct);
            body = JsonSerializer.Deserialize<UploadTemplateRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new JsonException("empty");
        }
        catch (Exception)
        {
            return BadRequest(new { message = "Corps JSON invalide (attendu UploadTemplateRequest) ou utilisez multipart/form-data avec le champ file." });
        }

        if (string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(new { message = "Le contenu du fichier est requis pour analyse (mode JSON)." });
        if (body.Content.Length > MaxTemplateContentLength)
            return BadRequest(new { message = "Fichier trop volumineux pour l'analyse V1." });

        var detected = placeholderNormalization.ExtractPlaceholders(body.Content);
        var vars = detected.Select(v => new TemplateVariableInput
        {
            Name = v.CanonicalKey,
            Type = v.Type,
            IsRequired = v.IsRequired,
            ValidationRule = v.ValidationRule,
            DisplayLabel = v.SuggestedLabel,
            FormScope = placeholderNormalization.IsDatabaseBackedKey(v.CanonicalKey) ? "db" : "pilot",
            SourcePriority = placeholderNormalization.IsDatabaseBackedKey(v.CanonicalKey) ? 10 : 20,
            NormalizedName = v.NormalizedKey,
            RawPlaceholder = v.RawToken,
        }).ToList();

        var req = new CreateDocumentTemplateRequest
        {
            Code = body.Code,
            Name = body.Name,
            Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
            DocumentTypeId = body.DocumentTypeId,
            Source = "UPLOAD",
            StructuredContent = body.Content,
            OriginalAssetUri = string.IsNullOrWhiteSpace(body.FileName) ? "inline-text" : body.FileName.Trim(),
            Variables = vars,
        };

        return await CreateDocumentTemplate(req, ct);
    }

    [HttpPost("document-templates/internal-engine/analyze")]
    public ActionResult<InternalEngineAnalysisResponse> AnalyzeInternalEngineTemplate(
        [FromBody] InternalEngineTemplateRequest body)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();
        if (string.IsNullOrWhiteSpace(body.StructuredContent))
            return BadRequest(new { message = "structuredContent est obligatoire." });

        var placeholders = placeholderNormalization.ExtractPlaceholders(body.StructuredContent);
        var variables = placeholders.Select(p => new TemplateVariableInput
        {
            Name = p.CanonicalKey,
            Type = p.Type,
            IsRequired = p.IsRequired,
            ValidationRule = p.ValidationRule,
            DisplayLabel = p.SuggestedLabel,
            FormScope = placeholderNormalization.IsDatabaseBackedKey(p.CanonicalKey) ? "db" : "pilot",
            SourcePriority = placeholderNormalization.IsDatabaseBackedKey(p.CanonicalKey) ? 10 : 20,
            NormalizedName = p.NormalizedKey,
            RawPlaceholder = p.RawToken,
        }).ToList();

        return Ok(new InternalEngineAnalysisResponse(
            body.StructuredContent,
            placeholders.Select(p => new InternalEnginePlaceholderResponse(
                p.RawToken,
                p.NormalizedKey,
                p.CanonicalKey,
                p.Status,
                p.SuggestedLabel,
                p.Type,
                p.IsRequired,
                p.ValidationRule)).ToList(),
            variables));
    }

    [HttpPost("document-templates/internal-engine")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> CreateInternalEngineTemplate(
        [FromBody] InternalEngineTemplateRequest body,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();
        if (string.IsNullOrWhiteSpace(body.StructuredContent))
            return BadRequest(new { message = "structuredContent est obligatoire." });
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { message = "Le nom est obligatoire." });

        try
        {
            var created = await templateManagement.CreateFromInternalEngineAsync(
                userContext.UserId.Value,
                body.Code,
                body.Name,
                body.Description,
                body.DocumentTypeId,
                body.StructuredContent,
                body.Variables,
                ct);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return MapTemplateUploadException(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Génération de contenu structuré via API IA (clé dans DocumentTemplates:Ai).</summary>
    [HttpPost("document-templates/generate")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> GenerateTemplateFromAi(
        [FromBody] AiGenerateTemplateRequest body,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Description))
            return BadRequest(new { message = "La description est obligatoire pour la génération IA." });
        if (body.Description.Length > 4000)
            return BadRequest(new { message = "Description trop longue (max 4000 caractères)." });
        try
        {
            var created = await templateManagement.CreateFromAiDescriptionAsync(
                userContext.UserId.Value,
                body.Description,
                body.Name,
                body.Code,
                body.DocumentTypeId,
                ct);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return MapTemplateUploadException(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("document-templates/rule-generate")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> GenerateRuleBasedTemplate(
        [FromBody] RuleGenerateTemplateRequest body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Description))
            return BadRequest(new { message = "La description RH est obligatoire." });
        if (body.Description.Length > 2000)
            return BadRequest(new { message = "Description trop longue." });
        var names = body.SuggestedVariables.Count == 0
            ? new[] { "nom", "prenom", "cin", "poste", "salaire", "date_embauche", "departement", "date" }
            : body.SuggestedVariables;
        var content = templateEngine.BuildRuleBasedContent(body.Description.Trim(), names);
        var vars = templateEngine.DetectVariables(content).Select(v => new TemplateVariableInput
        {
            Name = v.Name,
            Type = v.Type,
            IsRequired = v.IsRequired,
            ValidationRule = v.ValidationRule,
        }).ToList();

        var req = new CreateDocumentTemplateRequest
        {
            Code = body.Code,
            Name = body.Name,
            Description = body.Description.Trim(),
            DocumentTypeId = body.DocumentTypeId,
            Source = "RULE_BASED",
            StructuredContent = content,
            Variables = vars,
        };

        return await CreateDocumentTemplate(req, ct);
    }

    [HttpPost("document-templates/{id:guid}/versions")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> CreateTemplateVersion(
        Guid id,
        [FromBody] CreateTemplateVersionRequest body,
        CancellationToken ct)
    {
        logger.LogDebug(
            "CreateTemplateVersion:entry templateId={TemplateId} status={Status} contentLen={Len} vars={VarCount}",
            id,
            body.Status,
            body.StructuredContent?.Length ?? 0,
            body.Variables?.Count ?? 0);
        var template = await db.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();
        logger.LogDebug(
            "CreateTemplateVersion:template loaded templateId={TemplateId} kind={Kind} code={Code}",
            template.Id,
            template.Kind,
            template.Code);
        if (string.IsNullOrWhiteSpace(body.StructuredContent))
            return BadRequest(new { message = "structuredContent est obligatoire." });
        if (body.StructuredContent.Length > MaxTemplateContentLength)
            return BadRequest(new { message = "structuredContent trop volumineux." });

        var status = NormalizeVersionStatus(body.Status);
        IReadOnlyList<TemplateVariableInput> vars;
        if (template.Kind == DocumentTemplateKind.Static)
            vars = Array.Empty<TemplateVariableInput>();
        else if (body.Variables is null || body.Variables.Count == 0)
        {
            vars = templateEngine.DetectVariables(body.StructuredContent).Select(v => new TemplateVariableInput
            {
                Name = v.Name, Type = v.Type, IsRequired = v.IsRequired, ValidationRule = v.ValidationRule,
            }).ToList();
        }
        else
            vars = body.Variables;

        try
        {
            var version = await CreateTemplateVersionInternalAsync(
                template,
                body.StructuredContent,
                status,
                body.OriginalAssetUri,
                vars,
                userContext.UserId,
                ct);

            if (status == "published")
                template.CurrentVersionId = version.Id;
            template.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "CreateTemplateVersion:success templateId={TemplateId} versionId={VersionId} versionNumber={VersionNumber} status={Status}",
                template.Id,
                version.Id,
                version.VersionNumber,
                status);

            return Ok(MapVersionResponse(version, vars.Select((v, i) => ToVariableResponse(v, i)).ToList()));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "CreateTemplateVersion:exception templateId={TemplateId} sqlState={SqlState}",
                template.Id,
                FindPostgresException(ex)?.SqlState);
            throw;
        }
    }

    /// <summary>
    /// Libellés et obligatoire pour les champs « données nécessaires » (version courante, sans nouveau fichier).
    /// </summary>
    [HttpPut("document-templates/{id:guid}/current-version/variables")]
    public async Task<ActionResult<DocumentTemplateDetailResponse>> PutCurrentVersionVariables(
        Guid id,
        [FromBody] IReadOnlyList<TemplateVariableInput>? body,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();
        if (body is null || body.Count == 0)
            return BadRequest(new { message = "Au moins une variable est requise." });

        var template = await db.DocumentTemplates
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();
        if (template.CurrentVersionId is not { } cvId)
            return BadRequest(new { message = "Aucune version courante sur ce modèle." });

        var existing = await db.DocumentTemplateVariables
            .Where(v => v.TemplateVersionId == cvId)
            .ToListAsync(ct);
        if (existing.Count > 0)
            db.DocumentTemplateVariables.RemoveRange(existing);

        var rows = body.Select((v, index) => new DocumentTemplateVariable
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            TemplateVersionId = cvId,
            VariableName = v.Name.Trim(),
            VariableType = string.IsNullOrWhiteSpace(v.Type) ? "text" : v.Type.Trim().ToLowerInvariant(),
            IsRequired = v.IsRequired,
            DefaultValue = string.IsNullOrWhiteSpace(v.DefaultValue) ? null : v.DefaultValue.Trim(),
            ValidationRule = string.IsNullOrWhiteSpace(v.ValidationRule) ? null : v.ValidationRule.Trim(),
            DisplayLabel = string.IsNullOrWhiteSpace(v.DisplayLabel) ? null : v.DisplayLabel.Trim(),
            FormScope = NormalizeFormScope(v.FormScope),
            SourcePriority = v.SourcePriority ?? GuessSourcePriority(v.FormScope),
            NormalizedName = string.IsNullOrWhiteSpace(v.NormalizedName) ? null : v.NormalizedName.Trim(),
            RawPlaceholder = string.IsNullOrWhiteSpace(v.RawPlaceholder) ? null : v.RawPlaceholder.Trim(),
            SortOrder = index,
        })
            .Where(v => IsValidVariableName(v.VariableName))
            .Take(MaxTemplateVariables)
            .ToList();
        if (rows.Count == 0)
            return BadRequest(new { message = "Aucun nom de variable valide." });
        db.DocumentTemplateVariables.AddRange(rows);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetDocumentTemplate(id, ct);
    }

    [HttpGet("document-templates/{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<DocumentTemplateVersionResponse>>> GetTemplateVersions(Guid id, CancellationToken ct)
    {
        var exists = await db.DocumentTemplates.AnyAsync(t => t.Id == id, ct);
        if (!exists)
            return NotFound();

        var versions = await db.DocumentTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == id)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

        var versionIds = versions.Select(v => v.Id).ToArray();
        var vars = await db.DocumentTemplateVariables.AsNoTracking()
            .Where(v => v.TemplateVersionId.HasValue && versionIds.Contains(v.TemplateVersionId.Value))
            .OrderBy(v => v.SortOrder)
            .ToListAsync(ct);

        var grouped = vars.GroupBy(v => v.TemplateVersionId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        return Ok(versions.Select(v => MapVersionResponse(v, grouped.GetValueOrDefault(v.Id, new List<DocumentTemplateVariable>())
            .Select(MapVariableResponse).ToList())).ToList());
    }

    [HttpPost("document-templates/{id:guid}/test-run")]
    public async Task<ActionResult<TemplateTestRunResponse>> TestRunTemplate(
        Guid id,
        [FromBody] TemplateTestRunRequest body,
        CancellationToken ct)
    {
        var template = await db.DocumentTemplates
            .Include(t => t.CurrentVersion)
            .Include(t => t.DocumentType)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound(new { message = "Template introuvable." });

        var version = template.CurrentVersion ?? await db.DocumentTemplateVersions
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version is null)
            return NotFound(new { message = "Aucune version de modèle disponible." });

        var versionId = version.Id;
        if (template.Kind == DocumentTemplateKind.Static)
        {
            return Ok(new TemplateTestRunResponse(
                "Modèle statique : le rendu est le fichier source (aperçu / téléchargement du fichier modèle).",
                Array.Empty<string>(),
                version.OriginalAssetUri ?? $"{template.Code}-source"));
        }

        var requiredVariables = await db.DocumentTemplateVariables.AsNoTracking()
            .Where(v => v.TemplateVersionId == versionId && v.IsRequired)
            .Select(v => v.VariableName)
            .ToListAsync(ct);
        var merged = await MergeTemplateVariablesAsync(body.BeneficiaryUserId, documentRequestId: null, body.SampleData, ct);
        await ApplyAiVariableRefinementAsync(
            merged,
            versionId,
            template.DocumentType?.Name ?? template.Name,
            ct).ConfigureAwait(false);
        var missing = requiredVariables
            .Where(n => !merged.TryGetValue(n, out var v) || string.IsNullOrWhiteSpace(v))
            .ToList();
        var mergedForDisplay = missing.Count > 0
            ? MergeWithMissingPlaceholders(merged, missing, documentWorkflowOptions.Value.MissingFieldPlaceholder)
            : merged;
        var rendered = templateEngine.RenderContent(version.StructuredContent, mergedForDisplay);

        return Ok(new TemplateTestRunResponse(rendered, missing, $"PREVIEW_{template.Code}.pdf"));
    }

    [HttpPost("document-templates/{id:guid}/generate")]
    public async Task<ActionResult<DocumentTemplateGenerateResponse>> GenerateFromTemplate(
        Guid id,
        [FromBody] DocumentTemplateGenerateRequest? body,
        CancellationToken ct)
    {
        var wf = new DocumentWorkflowRequest
        {
            TemplateId = id,
            DocumentRequestId = body?.DocumentRequestId,
            BeneficiaryUserId = body?.BeneficiaryUserId,
            DocumentTypeId = body?.DocumentTypeId,
            Variables = body?.Variables ?? new Dictionary<string, string>(),
        };
        var prep = await PrepareDocumentWorkflowAsync(wf, ct);
        if (prep.FailedResult is { } fail)
            return fail;
        if (prep.Template.Kind != DocumentTemplateKind.Static &&
            (prep.MissingRequired.Count > 0 || prep.InvalidFormat.Count > 0))
        {
            return BadRequest(new
            {
                message = "Données manquantes ou invalides : génération bloquée tant que les champs ne sont pas corrigés.",
                missingVariables = prep.MissingRequired,
                invalidVariables = prep.InvalidFormat,
            });
        }

        return await CompleteDocumentGenerationAsync(wf, prep, ct);
    }

    /// <summary>
    /// Aperçu PDF sans persistance (même pipeline que la génération finale). Réservé RH / Admin.
    /// </summary>
    [HttpPost("documents/preview")]
    public async Task<IActionResult> PreviewDocument([FromBody] DocumentWorkflowRequest req, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var prep = await PrepareDocumentWorkflowAsync(req, ct);
        if (prep.FailedResult is { } fail)
            return fail;
        if (prep.MissingRequired.Count > 0 && !documentWorkflowOptions.Value.RequireRhEditorReview)
        {
            return BadRequest(new
            {
                message = "Variables obligatoires manquantes.",
                missingVariables = prep.MissingRequired,
                invalidVariables = prep.InvalidFormat,
            });
        }
        if (prep.InvalidFormat.Count > 0)
        {
            return BadRequest(new
            {
                message = "Formats invalides détectés. Corrigez les champs avant de continuer.",
                missingVariables = prep.MissingRequired,
                invalidVariables = prep.InvalidFormat,
            });
        }

        if (prep.Template.Kind != DocumentTemplateKind.Static)
        {
            var filled = Math.Max(0, prep.RequiredVariableCount - prep.MissingRequired.Count);
            var pct = prep.RequiredVariableCount > 0
                ? (int)Math.Round(100.0 * filled / prep.RequiredVariableCount)
                : 100;
            Response.Headers.Append("X-Document-Required-Total", prep.RequiredVariableCount.ToString(CultureInfo.InvariantCulture));
            Response.Headers.Append("X-Document-Missing-Count", prep.MissingRequired.Count.ToString(CultureInfo.InvariantCulture));
            Response.Headers.Append("X-Document-Filled-Count", filled.ToString(CultureInfo.InvariantCulture));
            Response.Headers.Append("X-Document-Filled-Percent", pct.ToString(CultureInfo.InvariantCulture));
            Response.Headers.Append("X-Document-Missing-Variables", string.Join(',', prep.MissingRequired));
            Response.Headers.Append("X-Document-Invalid-Count", prep.InvalidFormat.Count.ToString(CultureInfo.InvariantCulture));
        }

        if (prep.Template.Kind == DocumentTemplateKind.Static)
        {
            var payload = await templateBlobStorage.TryReadObjectAsync(prep.Version.OriginalAssetUri, ct);
            if (payload is null)
                return NotFound(new { message = "Fichier modèle statique introuvable ou stockage indisponible." });
            var star = Uri.EscapeDataString(payload.FileName);
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"file\"; filename*=UTF-8''{star}");
            return File(payload.Content, payload.ContentType);
        }

        var wfOpts = documentWorkflowOptions.Value;
        var mergedForPreview = prep.Merged;
        if (prep.MissingRequired.Count > 0)
        {
            mergedForPreview = MergeWithMissingPlaceholders(
                prep.Merged,
                prep.MissingRequired,
                wfOpts.MissingFieldPlaceholder);
        }

        // Aperçu fidèle au fichier Word d’origine (substitutions OpenXML) — évite QuestPDF et reflète mise en page / styles du modèle.
        var originalAssetUri = prep.Version.OriginalAssetUri;
        var originalDocx = await templateBlobStorage.TryReadObjectAsync(originalAssetUri, ct);

        if (DocxTemplatePayloadInspector.IsWordProcessingOpenXml(originalDocx) && originalDocx is { Content.Length: > 0 })
        {
            var docxBytes = originalDocxTemplateRender.Render(originalDocx.Content, mergedForPreview);
            var safeCode = string.IsNullOrWhiteSpace(prep.Template.Code) ? "DOC" : prep.Template.Code.Trim();
            var docxFileName = $"PREVIEW_{safeCode}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.docx";
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Append("X-Preview-Source", "original-docx");
            return File(
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileDownloadName: docxFileName);
        }

        var rendered = templateEngine.RenderContent(prep.Version.StructuredContent, mergedForPreview);
        var (fileName, pdfBytes) = pdfExport.BuildPdf(
            prep.Template.Code,
            tenantAccessor.ResolvedTenantId,
            rendered,
            prep.TitleFallback);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        return File(pdfBytes, "application/pdf", fileDownloadName: fileName);
    }

    /// <summary>
    /// Génération IA directe : un seul payload (template + dbData + formData), sans substitution locale.
    /// Réponse 200 si le texte final est validé ; 422 si placeholders résiduels, données critiques absentes du texte, ou ERROR_MISSING_DATA.
    /// </summary>
    [HttpPost("documents/ai-direct/fill")]
    public async Task<IActionResult> AiDirectFillDocument(
        [FromBody] AiDirectDocumentFillRequest body,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var (doc, err) = await TryAiDirectFillValidatedAsync(body, ct);
        if (err is not null)
            return err;
        return Ok(new AiDirectDocumentFillResponse("ok", doc!, null, false));
    }

    /// <summary>Aperçu PDF pour la génération IA directe (même validation que <c>ai-direct/fill</c>, sans persistance).</summary>
    [HttpPost("documents/ai-direct/preview")]
    public async Task<IActionResult> AiDirectPreviewPdf([FromBody] AiDirectDocumentFillRequest body, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var (doc, err) = await TryAiDirectFillValidatedAsync(body, ct);
        if (err is not null)
            return err;

        var (fileName, pdfBytes) = pdfExport.BuildPdf(
            "AI_DIRECT_PREVIEW",
            tenantAccessor.ResolvedTenantId,
            doc!,
            string.IsNullOrWhiteSpace(body.DocumentTitle) ? "Aperçu" : body.DocumentTitle!.Trim());
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        return File(pdfBytes, "application/pdf", fileDownloadName: fileName);
    }

    /// <summary>Génération finale (MinIO / base) — même contenu que l’aperçu PDF après validation RH. Réservé RH / Admin.</summary>
    [HttpPost("documents/generate")]
    public async Task<ActionResult<DocumentTemplateGenerateResponse>> GenerateDocumentWorkflow(
        [FromBody] DocumentWorkflowRequest req,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var prep = await PrepareDocumentWorkflowAsync(req, ct);
        if (prep.FailedResult is { } fail)
            return fail;
        if (prep.MissingRequired.Count > 0 || prep.InvalidFormat.Count > 0)
        {
            return BadRequest(new
            {
                message = "Données manquantes ou invalides : génération bloquée tant que les champs ne sont pas corrigés.",
                missingVariables = prep.MissingRequired,
                invalidVariables = prep.InvalidFormat,
            });
        }
        return await CompleteDocumentGenerationAsync(req, prep, ct);
    }

    /// <summary>Upload direct d’un document déjà prêt (PDF/Word) sans étape modèle ; rattache la demande pour le pilote.</summary>
    [HttpPost("documents/upload-ready")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<DocumentTemplateGenerateResponse>> UploadReadyDocument(CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();
        if (!Request.HasFormContentType)
            return BadRequest(new { message = "Formulaire multipart/form-data requis." });

        var form = await Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Fichier « file » requis." });
        if (file.Length > 50 * 1024 * 1024)
            return BadRequest(new { message = "Fichier trop volumineux (max 50 Mo)." });

        Guid? requestId = null;
        var requestRaw = form["documentRequestId"].ToString();
        if (!string.IsNullOrWhiteSpace(requestRaw))
        {
            if (!Guid.TryParse(requestRaw, out var parsedReq) || parsedReq == Guid.Empty)
                return BadRequest(new { message = "documentRequestId invalide." });
            requestId = parsedReq;
        }

        Guid? beneficiaryId = null;
        var beneficiaryRaw = form["beneficiaryUserId"].ToString();
        if (!string.IsNullOrWhiteSpace(beneficiaryRaw))
        {
            if (!Guid.TryParse(beneficiaryRaw, out var parsedBeneficiary) || parsedBeneficiary == Guid.Empty)
                return BadRequest(new { message = "beneficiaryUserId invalide." });
            beneficiaryId = parsedBeneficiary;
        }

        Guid? explicitTypeId = null;
        var typeRaw = form["documentTypeId"].ToString();
        if (!string.IsNullOrWhiteSpace(typeRaw))
        {
            if (!Guid.TryParse(typeRaw, out var parsedType) || parsedType == Guid.Empty)
                return BadRequest(new { message = "documentTypeId invalide." });
            explicitTypeId = parsedType;
        }

        DocumentRequest? linkedRequest = null;
        if (requestId.HasValue)
        {
            linkedRequest = await db.DocumentRequests.FirstOrDefaultAsync(r => r.Id == requestId.Value, ct);
            if (linkedRequest is null)
                return NotFound(new { message = "Demande introuvable." });
            if (!string.Equals(linkedRequest.TenantId?.Trim(), tenantAccessor.ResolvedTenantId.Trim(), StringComparison.Ordinal))
                return Forbid();
            if (linkedRequest.Status != DocumentRequestStatus.Approved)
                return BadRequest(new { message = "La demande doit être approuvée avant l’upload du document final." });
        }

        var ownerId = beneficiaryId
            ?? linkedRequest?.BeneficiaryUserId
            ?? linkedRequest?.RequesterUserId
            ?? userContext.UserId.Value;
        var documentTypeId = explicitTypeId ?? linkedRequest?.DocumentTypeId;
        var now = DateTimeOffset.UtcNow;
        var genId = Guid.NewGuid();
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"document_pret_{now:yyyyMMdd_HHmmss}";

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        string storageUri;
        if (templateBlobStorage.IsConfigured)
        {
            await using var stream = new MemoryStream(bytes);
            var key = $"{tenantAccessor.ResolvedTenantId.TrimEnd('/')}/generated/{genId:N}/{Uri.EscapeDataString(fileName)}";
            storageUri = await templateBlobStorage.PutTemplateObjectAsync(key, stream, file.ContentType, ct);
        }
        else
        {
            storageUri = $"inline://generated/{genId:N}/{Uri.EscapeDataString(fileName)}";
        }

        var gen = new GeneratedDocument
        {
            Id = genId,
            DocumentRequestId = linkedRequest?.Id,
            OwnerUserId = ownerId,
            DocumentTypeId = documentTypeId,
            TemplateVersionId = null,
            FileName = fileName,
            StorageUri = storageUri,
            PdfContent = templateBlobStorage.IsConfigured ? null : bytes,
            MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes = bytes.LongLength,
            Status = GeneratedDocumentStatus.Generated,
            VersionNumber = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.GeneratedDocuments.Add(gen);

        if (linkedRequest is not null)
        {
            linkedRequest.Status = DocumentRequestStatus.Generated;
            linkedRequest.UpdatedAt = now;
            var auditDetails = JsonSerializer.Serialize(new
            {
                generatedDocumentId = gen.Id.ToString("D"),
                fileName,
                uploadReady = true,
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantAccessor.ResolvedTenantId,
                OccurredAt = now,
                ActorUserId = userContext.UserId,
                Action = "DOCUMENT_UPLOADED_READY",
                EntityType = "document_request",
                EntityId = linkedRequest.Id,
                Details = auditDetails,
                Success = true,
                RequestNumber = linkedRequest.RequestNumber,
            });
        }

        await db.SaveChangesAsync(ct);
        return Ok(new DocumentTemplateGenerateResponse(gen.Id.ToString("D"), fileName, storageUri, gen.Status.ToString()));
    }

    /// <summary>Téléchargement du PDF généré (binaire réel : colonne <c>pdf_content</c> ou objet MinIO).</summary>
    [HttpGet("generated-documents/{id:guid}/file")]
    public async Task<IActionResult> DownloadGeneratedDocumentFile(Guid id, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return Unauthorized();

        var gen = await db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            return NotFound(new { message = "Document généré introuvable." });

        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            return Forbid();

        if (gen.Status == GeneratedDocumentStatus.DraftPendingRhReview)
        {
            return Conflict(new
            {
                message = "Brouillon RH : aucun fichier final tant que la validation RH n’est pas terminée.",
                needsRhEditorReview = true,
                generatedDocumentId = gen.Id.ToString("D"),
            });
        }

        byte[]? bytes = gen.PdfContent;
        if (bytes is null || bytes.Length == 0)
        {
            var payload = await templateBlobStorage.TryReadObjectAsync(gen.StorageUri, ct);
            if (payload is null || payload.Content.Length == 0)
                return NotFound(new { message = "Fichier binaire introuvable (MinIO ou base)." });
            bytes = payload.Content;
        }

        var downloadName = string.IsNullOrWhiteSpace(gen.FileName) ? "document.pdf" : gen.FileName;
        var contentType = string.IsNullOrWhiteSpace(gen.MimeType) ? "application/pdf" : gen.MimeType;
        return File(bytes, contentType, downloadName);
    }

    /// <summary>Brouillon RH : texte généré + marqueurs d’aide ; édition avant PDF final.</summary>
    [HttpGet("generated-documents/{id:guid}/rh-editor")]
    public async Task<ActionResult<RhGeneratedDocumentEditorResponse>> GetRhGeneratedDocumentEditor(Guid id, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var gen = await db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            return NotFound(new { message = "Document généré introuvable." });
        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            return Forbid();
        if (!await IsGeneratedDocumentInTenantAsync(gen, ct))
            return Forbid();
        if (gen.Status != GeneratedDocumentStatus.DraftPendingRhReview)
            return BadRequest(new { message = "Ce document n’est pas un brouillon en attente de validation RH." });

        var missing = DeserializeRhMissingVariables(gen.RhMissingVariablesJson);
        var generated = gen.ContentGenerated ?? string.Empty;
        var editable = string.IsNullOrEmpty(gen.ContentFinal) ? generated : gen.ContentFinal!;
        return Ok(new RhGeneratedDocumentEditorResponse(
            gen.Id.ToString("D"),
            gen.Status == GeneratedDocumentStatus.DraftPendingRhReview ? "InProgress" : gen.Status.ToString(),
            generated,
            editable,
            missing));
    }

    /// <summary>Enregistre le texte édité (brouillon) avant finalisation PDF.</summary>
    [HttpPut("generated-documents/{id:guid}/rh-editor")]
    public async Task<IActionResult> PutRhGeneratedDocumentEditor(
        Guid id,
        [FromBody] UpdateRhGeneratedDocumentContentRequest body,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var gen = await db.GeneratedDocuments.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            return NotFound(new { message = "Document généré introuvable." });
        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            return Forbid();
        if (!await IsGeneratedDocumentInTenantAsync(gen, ct))
            return Forbid();
        if (gen.Status != GeneratedDocumentStatus.DraftPendingRhReview)
            return BadRequest(new { message = "Ce document n’est pas un brouillon modifiable." });

        gen.ContentFinal = body.Content ?? string.Empty;
        gen.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Finalise le brouillon RH : valide <c>content_final</c> (marqueurs) puis enregistre le document officiel.
    /// Si le modèle a un fichier Word d’origine, le binaire officiel est le <b>DOCX rempli</b> (même styles que le template) ;
    /// sinon un <b>PDF</b> est produit via QuestPDF à partir du texte validé.
    /// </summary>
    [HttpPost("generated-documents/{id:guid}/finalize-rh")]
    public async Task<ActionResult<DocumentTemplateGenerateResponse>> FinalizeRhGeneratedDocument(Guid id, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return Unauthorized();
        if (userContext.Role is not (AppRole.Rh or AppRole.Admin))
            return Forbid();

        var gen = await db.GeneratedDocuments
            .Include(g => g.DocumentRequest)
            .Include(g => g.DocumentType)
            .Include(g => g.TemplateVersion)!.ThenInclude(v => v!.Template)!.ThenInclude(t => t!.DocumentType)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gen is null)
            return NotFound(new { message = "Document généré introuvable." });
        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            return Forbid();
        if (!await IsGeneratedDocumentInTenantAsync(gen, ct))
            return Forbid();
        if (gen.Status != GeneratedDocumentStatus.DraftPendingRhReview)
            return BadRequest(new { message = "Ce document n’est pas un brouillon en attente de finalisation." });

        var rendered = gen.ContentFinal?.Trim();
        if (string.IsNullOrEmpty(rendered))
            return BadRequest(new { message = "Contenu vide : complétez le texte avant de valider le document." });
        var unresolvedMarkers = DetectUnresolvedLegacyMarkers(rendered);
        if (unresolvedMarkers.Count > 0)
        {
            return BadRequest(new
            {
                message = "Données manquantes : certains champs du document contiennent encore des marqueurs vides. Complétez les données avant validation finale.",
                missingVariables = unresolvedMarkers,
            });
        }

        var template = gen.TemplateVersion?.Template;
        if (template is null)
            return BadRequest(new { message = "Modèle introuvable pour ce document." });

        var titleFallback = gen.DocumentType?.Name
            ?? template.DocumentType?.Name
            ?? template.Name
            ?? template.Code;

        var now = DateTimeOffset.UtcNow;
        var exportContext = await TryBuildStructuredExportContextAsync(gen, ct);
        var originalAssetUri = exportContext?.Version?.OriginalAssetUri;
        var originalDocx = await templateBlobStorage.TryReadObjectAsync(originalAssetUri, ct);
        if (exportContext is not null &&
            DocxTemplatePayloadInspector.IsWordProcessingOpenXml(originalDocx) &&
            originalDocx is { Content.Length: > 0 })
        {
            var docxBytes = originalDocxTemplateRender.Render(originalDocx.Content, exportContext.Value.Merged);
            var stem = await BuildExportFileStemAsync(gen, ct);
            var docxFileName = $"{stem}.docx";
            string storageUri;
            if (templateBlobStorage.IsConfigured)
            {
                await using var stream = new MemoryStream(docxBytes);
                var key = $"{tenantAccessor.ResolvedTenantId.TrimEnd('/')}/generated/{gen.Id:N}/{docxFileName}";
                storageUri = await templateBlobStorage.PutTemplateObjectAsync(
                    key,
                    stream,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ct);
            }
            else
            {
                storageUri = $"inline://generated/{gen.Id:N}/{Uri.EscapeDataString(docxFileName)}";
            }

            gen.FileName = docxFileName;
            gen.StorageUri = storageUri;
            gen.PdfContent = templateBlobStorage.IsConfigured ? null : docxBytes;
            gen.MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            gen.FileSizeBytes = docxBytes.LongLength;
            gen.Status = GeneratedDocumentStatus.Generated;
            gen.UpdatedAt = now;
        }
        else
        {
            var (fileName, pdfBytes) = pdfExport.BuildPdf(
                template.Code,
                tenantAccessor.ResolvedTenantId,
                rendered,
                titleFallback);

            string storageUri;
            if (templateBlobStorage.IsConfigured)
            {
                await using var stream = new MemoryStream(pdfBytes);
                var key = $"{tenantAccessor.ResolvedTenantId.TrimEnd('/')}/generated/{gen.Id:N}/{fileName}";
                storageUri = await templateBlobStorage.PutTemplateObjectAsync(key, stream, "application/pdf", ct);
            }
            else
            {
                storageUri = $"inline://generated/{gen.Id:N}/{Uri.EscapeDataString(fileName)}";
            }

            gen.FileName = fileName;
            gen.StorageUri = storageUri;
            gen.PdfContent = templateBlobStorage.IsConfigured ? null : pdfBytes;
            gen.MimeType = "application/pdf";
            gen.FileSizeBytes = pdfBytes.LongLength;
            gen.Status = GeneratedDocumentStatus.Generated;
            gen.UpdatedAt = now;
        }

        if (gen.DocumentRequest is { } linkedRequest)
        {
            linkedRequest.Status = DocumentRequestStatus.Generated;
            linkedRequest.UpdatedAt = now;
            var auditDetails = JsonSerializer.Serialize(new
            {
                generatedDocumentId = gen.Id.ToString("D"),
                fileName = gen.FileName,
                templateCode = template.Code,
                templateId = template.Id.ToString("D"),
                rhFinalized = true,
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantAccessor.ResolvedTenantId,
                OccurredAt = now,
                ActorUserId = userContext.UserId,
                Action = "DOCUMENT_GENERATED",
                EntityType = "document_request",
                EntityId = linkedRequest.Id,
                Details = auditDetails,
                Success = true,
                RequestNumber = linkedRequest.RequestNumber,
            });
        }

        await db.SaveChangesAsync(ct);
        return Ok(new DocumentTemplateGenerateResponse(gen.Id.ToString("D"), gen.FileName, gen.StorageUri, gen.Status.ToString()));
    }

    /// <summary>
    /// Export multi-format : pour un document finalisé sur modèle Word, le binaire stocké est le DOCX ; l’export PDF n’est
    /// pas disponible tant qu’aucun convertisseur DOCX→PDF n’est branché (<see cref="ExportGeneratedDocumentCoreAsync"/>).
    /// </summary>
    [HttpGet("generated-documents/{id:guid}/export")]
    public async Task<IActionResult> ExportGeneratedDocument(Guid id, [FromQuery] string format = "pdf", CancellationToken ct = default)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return Unauthorized();

        var gen = await LoadGeneratedDocumentForExportAsync(id, ct);
        if (gen is null)
            return NotFound(new { message = "Document généré introuvable." });

        if (!await CanAccessGeneratedDocumentAsync(gen, ct))
            return Forbid();

        return await ExportGeneratedDocumentCoreAsync(gen, format, ct);
    }

    /// <summary>
    /// Alias REST : <c>GET documents/{id}/download?format=pdf|docx|html|txt</c> — même logique que
    /// <see cref="ExportGeneratedDocument"/> (PDF stocké, autres formats générés à la demande).
    /// </summary>
    [HttpGet("documents/{id:guid}/download")]
    public Task<IActionResult> DownloadDocument(Guid id, [FromQuery] string format = "pdf", CancellationToken ct = default) =>
        ExportGeneratedDocument(id, format, ct);

    /// <summary>Téléchargement/export par demande (dernier PDF généré pour la demande).</summary>
    [HttpGet("document-requests/{requestId:guid}/download")]
    public async Task<IActionResult> DownloadDocumentRequestExport(Guid requestId, [FromQuery] string format = "pdf", CancellationToken ct = default)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return Unauthorized();

        var req = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null)
            return NotFound(new { message = "Demande introuvable." });

        if (!await CanActorViewDocumentRequestAsync(req, ct))
            return Forbid();

        var latest = await DocumentRequestMappingHelper.LoadLatestGeneratedForRequestAsync(db, requestId, ct);
        if (latest is null)
            return NotFound(new { message = "Aucun document généré pour cette demande." });

        var gen = await LoadGeneratedDocumentForExportAsync(latest.Id, ct);
        if (gen is null)
            return NotFound(new { message = "Document généré introuvable." });

        return await ExportGeneratedDocumentCoreAsync(gen, format, ct);
    }

    /// <summary>
    /// Liste l’annuaire du tenant courant (filtre global EF). Aucune ligne en base → 200 avec liste vide (pas d’erreur).
    /// Les exceptions (ex. SQL) sont gérées par <c>UnhandledExceptionMiddleware</c> — pas de try/catch dupliqué ici.
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetDirectoryUsers(CancellationToken ct)
    {
        var rows = await db.DirectoryUsers.AsNoTracking()
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(ct);
        return Ok(await MapDirectoryUsersAsync(rows, ct));
    }

    /// <summary>Profil de l’utilisateur identifié par <c>X-User-Id</c>.</summary>
    [HttpGet("users/me")]
    public async Task<ActionResult<DirectoryUserResponse>> GetDirectoryUserMe(CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return Unauthorized();
        var row = await db.DirectoryUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userContext.UserId.Value, ct);
        if (row is null)
            return NotFound(new { message = "Utilisateur absent de l’annuaire pour ce tenant." });
        return await MapDirectoryUserAsync(row, ct);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<DirectoryUserResponse>> GetDirectoryUser(Guid id, CancellationToken ct)
    {
        var row = await db.DirectoryUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (row is null)
            return NotFound();
        return await MapDirectoryUserAsync(row, ct);
    }

    [HttpGet("organisation/poles")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetPoles(CancellationToken ct) =>
        QueryOrganisationPolesAsync(ct);

    /// <summary>Alias orthographe US (même handler) — utile si un proxy ou une ancienne config attend <c>organization</c>.</summary>
    [HttpGet("organization/poles")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetPolesOrganizationSpelling(CancellationToken ct) =>
        QueryOrganisationPolesAsync(ct);

    [HttpGet("organisation/cellules")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetCellulesByPole([FromQuery] Guid poleId, CancellationToken ct) =>
        QueryOrganisationCellulesByPoleAsync(poleId, ct);

    [HttpGet("organization/cellules")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetCellulesByPoleOrganizationSpelling(
        [FromQuery] Guid poleId,
        CancellationToken ct) =>
        QueryOrganisationCellulesByPoleAsync(poleId, ct);

    [HttpGet("organisation/departements")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetDepartementsByCellule(
        [FromQuery] Guid celluleId,
        CancellationToken ct) =>
        QueryOrganisationDepartementsByCelluleAsync(celluleId, ct);

    [HttpGet("organization/departements")]
    public Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> GetDepartementsByCelluleOrganizationSpelling(
        [FromQuery] Guid celluleId,
        CancellationToken ct) =>
        QueryOrganisationDepartementsByCelluleAsync(celluleId, ct);

    private async Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> QueryOrganisationPolesAsync(CancellationToken ct)
    {
        var rows = await db.OrganisationUnits.AsNoTracking()
            .Where(u => u.UnitType != null && u.UnitType.ToLower() == OrgUnitTypePole)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
        return rows.Select(u => new OrganizationalUnitSummary(u.Id.ToString(), u.Code, u.Name, u.UnitType)).ToList();
    }

    private async Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> QueryOrganisationCellulesByPoleAsync(
        Guid poleId,
        CancellationToken ct)
    {
        var rows = await db.OrganisationUnits.AsNoTracking()
            .Where(u => u.UnitType != null && u.UnitType.ToLower() == OrgUnitTypeCellule && u.ParentId == poleId)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
        return rows.Select(u => new OrganizationalUnitSummary(u.Id.ToString(), u.Code, u.Name, u.UnitType)).ToList();
    }

    private async Task<ActionResult<IReadOnlyList<OrganizationalUnitSummary>>> QueryOrganisationDepartementsByCelluleAsync(
        Guid celluleId,
        CancellationToken ct)
    {
        var rows = await db.OrganisationUnits.AsNoTracking()
            .Where(u => u.UnitType != null && u.UnitType.ToLower() == OrgUnitTypeDepartement && u.ParentId == celluleId)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
        return rows.Select(u => new OrganizationalUnitSummary(u.Id.ToString(), u.Code, u.Name, u.UnitType)).ToList();
    }

    /// <summary>Utilisateurs filtrés par rôle applicatif et rattachement organisationnel (pôle, cellule, département).</summary>
    [HttpGet("users/by-role-org")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetUsersByRoleAndOrg(
        [FromQuery] string role,
        [FromQuery] Guid poleId,
        [FromQuery] Guid celluleId,
        [FromQuery] Guid departementId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(role) || !AppRoleHeaderParser.TryParse(role, out var appRole))
            return BadRequest(new { message = "role invalide (pilote, coach, manager, rp, rh, admin, audit)." });

        var rows = await db.DirectoryUsers.AsNoTracking()
            .Where(u =>
                u.Role == appRole &&
                u.PoleId == poleId &&
                u.CelluleId == celluleId &&
                u.DepartementId == departementId)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(ct);
        return Ok(await MapDirectoryUsersAsync(rows, ct));
    }

    /// <summary>Managers rattachés au département (hiérarchie métier + même triplet org).</summary>
    [HttpGet("users/managers")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetManagersByDepartement(
        [FromQuery] Guid departementId,
        CancellationToken ct)
    {
        var rows = await db.DirectoryUsers.AsNoTracking()
            .Where(u => u.Role == AppRole.Manager && u.DepartementId == departementId)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(ct);
        return Ok(await MapDirectoryUsersAsync(rows, ct));
    }

    /// <summary>Coachs sous un manager (optionnellement filtrés par département).</summary>
    [HttpGet("users/coaches")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetCoachsByManager(
        [FromQuery] Guid managerId,
        [FromQuery] Guid? departementId,
        CancellationToken ct)
    {
        var q = db.DirectoryUsers.AsNoTracking()
            .Where(u => u.Role == AppRole.Coach && u.ManagerId == managerId);
        if (departementId.HasValue)
            q = q.Where(u => u.DepartementId == departementId.Value);
        var rows = await q.OrderBy(u => u.Nom).ThenBy(u => u.Prenom).ToListAsync(ct);
        return Ok(await MapDirectoryUsersAsync(rows, ct));
    }

    /// <summary>Pilotes rattachés à un coach (optionnellement filtrés par département).</summary>
    [HttpGet("users/pilotes")]
    public async Task<ActionResult<IReadOnlyList<DirectoryUserResponse>>> GetPilotesByCoach(
        [FromQuery] Guid coachId,
        [FromQuery] Guid? departementId,
        CancellationToken ct)
    {
        var q = db.DirectoryUsers.AsNoTracking()
            .Where(u => u.Role == AppRole.Pilote && u.CoachId == coachId);
        if (departementId.HasValue)
            q = q.Where(u => u.DepartementId == departementId.Value);
        var rows = await q.OrderBy(u => u.Nom).ThenBy(u => u.Prenom).ToListAsync(ct);
        return Ok(await MapDirectoryUsersAsync(rows, ct));
    }

    private async Task<GeneratedDocument?> LoadGeneratedDocumentForExportAsync(Guid id, CancellationToken ct)
    {
        return await db.GeneratedDocuments.AsNoTracking()
            .Include(g => g.TemplateVersion)
                .ThenInclude(v => v!.Template)
            .Include(g => g.DocumentType)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    private static string? NormalizeExportFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "pdf";
        return format.Trim().ToLowerInvariant() switch
        {
            "pdf" => "pdf",
            "docx" => "docx",
            "txt" => "txt",
            "html" => "html",
            _ => null,
        };
    }

    private async Task<byte[]?> TryGetStoredPdfBytesAsync(GeneratedDocument gen, CancellationToken ct)
    {
        byte[]? bytes = gen.PdfContent;
        if (bytes is null || bytes.Length == 0)
        {
            var payload = await templateBlobStorage.TryReadObjectAsync(gen.StorageUri, ct);
            if (payload is null || payload.Content.Length == 0)
                return null;
            bytes = payload.Content;
        }

        return bytes;
    }

    /// <summary>
    /// Dictionnaire fusionné pour exports / DOCX : si un instantané a été enregistré à la création du brouillon,
    /// il est utilisé tel quel (alignement strict avec l’aperçu) ; sinon fusion annuaire + champs demande uniquement.
    /// </summary>
    private async Task<Dictionary<string, string>> BuildExportMergedDictionaryAsync(
        GeneratedDocument gen,
        DocumentTemplateVersion version,
        string titleFallback,
        CancellationToken ct)
    {
        var fromSnap = TryParseWorkflowVariablesSnapshot(gen.WorkflowVariablesSnapshotJson);
        if (fromSnap is not null)
            return fromSnap;

        Guid? beneficiaryId = null;
        if (gen.DocumentRequestId is { } drId)
        {
            var reqRow = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == drId, ct);
            beneficiaryId = reqRow?.BeneficiaryUserId ?? reqRow?.RequesterUserId;
        }

        var merged = await MergeTemplateVariablesAsync(beneficiaryId, gen.DocumentRequestId, null, ct);
        var d = gen.CreatedAt;
        merged["date"] = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        merged["date_fr"] = d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
        EnsureFrenchDateAlias(merged);
        await ApplyAiVariableRefinementAsync(merged, version.Id, titleFallback, ct).ConfigureAwait(false);
        return merged;
    }

    private static Dictionary<string, string>? TryParseWorkflowVariablesSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (d is null || d.Count == 0)
                return null;
            return new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<(string Rendered, string TitleFallback)?> TryRenderStructuredExportAsync(GeneratedDocument gen, CancellationToken ct)
    {
        var version = gen.TemplateVersion;
        if (version is null && gen.TemplateVersionId is { } tvId)
        {
            version = await db.DocumentTemplateVersions.AsNoTracking()
                .Include(v => v.Template)
                .FirstOrDefaultAsync(v => v.Id == tvId, ct);
        }

        var titleFallback = gen.DocumentType?.Name
            ?? version?.Template?.Name
            ?? version?.Template?.Code
            ?? "Document";

        // PDF / exports : texte validé par le RH (pas un re-rendu template qui réinjecterait l’annuaire).
        if (gen.Status == GeneratedDocumentStatus.Generated && !string.IsNullOrWhiteSpace(gen.ContentFinal))
            return (gen.ContentFinal!, titleFallback);

        if (version is null || string.IsNullOrWhiteSpace(version.StructuredContent))
            return null;

        var merged = await BuildExportMergedDictionaryAsync(gen, version, titleFallback, ct);
        var rendered = templateEngine.RenderContent(version.StructuredContent, merged);
        return (rendered, titleFallback);
    }

    private async Task<(Dictionary<string, string> Merged, DocumentTemplateVersion? Version, string TitleFallback)?> TryBuildStructuredExportContextAsync(
        GeneratedDocument gen,
        CancellationToken ct)
    {
        var version = gen.TemplateVersion;
        if (version is null && gen.TemplateVersionId is { } tvId)
        {
            version = await db.DocumentTemplateVersions.AsNoTracking()
                .Include(v => v.Template)
                .FirstOrDefaultAsync(v => v.Id == tvId, ct);
        }

        var titleFallback = gen.DocumentType?.Name
            ?? version?.Template?.Name
            ?? version?.Template?.Code
            ?? "Document";

        if (version is null)
            return null;

        var merged = await BuildExportMergedDictionaryAsync(gen, version, titleFallback, ct);
        return (merged, version, titleFallback);
    }

    private async Task<string> BuildExportFileStemAsync(GeneratedDocument gen, CancellationToken ct)
    {
        static string SlugifySegment(string s)
        {
            var cleaned = string.Join("_", s.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
                .Replace(" ", "_", StringComparison.Ordinal);
            while (cleaned.Contains("__", StringComparison.Ordinal))
                cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(cleaned))
                return "document";
            return cleaned.Length > 64 ? cleaned[..64] : cleaned;
        }

        var typePart = "document";
        if (gen.DocumentType is { } dt && !string.IsNullOrWhiteSpace(dt.Code))
            typePart = SlugifySegment(dt.Code);
        else if (gen.TemplateVersion?.Template is { } tpl && !string.IsNullOrWhiteSpace(tpl.Code))
            typePart = SlugifySegment(tpl.Code);

        var namePart = "beneficiaire";
        if (gen.DocumentRequestId is { } rid)
        {
            var req = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct);
            if (req is not null)
            {
                var uid = req.BeneficiaryUserId ?? req.RequesterUserId;
                var du = await db.DirectoryUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct);
                if (du is not null)
                {
                    var raw = $"{du.Prenom}{du.Nom}".Trim();
                    if (raw.Length > 0)
                        namePart = SlugifySegment(raw);
                }
            }
        }

        return $"{typePart}_{namePart}";
    }

    private async Task LogDocumentDownloadAsync(GeneratedDocument gen, string format, string fileName, CancellationToken ct)
    {
        DocumentRequest? req = null;
        if (gen.DocumentRequestId is { } rid)
            req = await db.DocumentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct);

        var details = JsonSerializer.Serialize(new
        {
            format,
            fileName,
            generatedDocumentId = gen.Id.ToString("D"),
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantAccessor.ResolvedTenantId,
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = userContext.UserId,
            Action = "DOCUMENT_DOWNLOAD",
            EntityType = req is not null ? "document_request" : "generated_document",
            EntityId = req?.Id ?? gen.Id,
            Details = details,
            Success = true,
            RequestNumber = req?.RequestNumber,
            IpAddress = HttpContext.Connection.RemoteIpAddress,
            UserAgent = Request.Headers.UserAgent.ToString(),
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<IActionResult> ExportGeneratedDocumentCoreAsync(GeneratedDocument gen, string formatRaw, CancellationToken ct)
    {
        var format = NormalizeExportFormat(formatRaw);
        if (format is null)
            return BadRequest(new { message = "Format non pris en charge (pdf, docx, txt, html)." });

        if (gen.Status == GeneratedDocumentStatus.DraftPendingRhReview)
        {
            return Conflict(new
            {
                message = "Document en cours de finalisation RH.",
                needsRhEditorReview = true,
                generatedDocumentId = gen.Id.ToString("D"),
                status = "InProgress",
            });
        }

        byte[] bytes;
        string contentType;
        string ext;

        if (format == "pdf")
        {
            var officialMime = (gen.MimeType ?? string.Empty).ToLowerInvariant();
            if (officialMime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
            {
                return UnprocessableEntity(new
                {
                    message =
                        "Le document officiel est le fichier Word (mise en page identique au modèle uploadé). Utilisez l’export DOCX. Une conversion PDF fidèle au modèle nécessite un moteur serveur (ex. LibreOffice) non branché ici.",
                    officialFormat = "docx",
                });
            }

            var pdf = await TryGetStoredPdfBytesAsync(gen, ct);
            if (pdf is null)
                return NotFound(new { message = "Fichier PDF introuvable (MinIO ou base)." });
            bytes = pdf;
            contentType = "application/pdf";
            ext = ".pdf";
        }
        else
        {
            var exportContext = await TryBuildStructuredExportContextAsync(gen, ct);
            if (exportContext is null)
                return BadRequest(new { message = "Export indisponible : version de modèle ou contenu structuré absent." });

            var rendered = await TryRenderStructuredExportAsync(gen, ct);
            if (rendered is null)
                return BadRequest(new { message = "Export indisponible : rendu introuvable." });

            var parts = StructuredDocumentExportParser.Parse(rendered.Value.Rendered, rendered.Value.TitleFallback);
            const string watermark = "Officiel";
            switch (format)
            {
                case "docx":
                {
                    var genMime = (gen.MimeType ?? string.Empty).ToLowerInvariant();
                    if (gen.Status == GeneratedDocumentStatus.Generated &&
                        genMime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
                    {
                        var officialBytes = await TryGetStoredPdfBytesAsync(gen, ct);
                        if (officialBytes is { Length: > 0 })
                        {
                            bytes = officialBytes;
                            contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                            ext = ".docx";
                            break;
                        }
                    }

                    var originalAssetUri = exportContext.Value.Version?.OriginalAssetUri;
                    var originalDocx = await templateBlobStorage.TryReadObjectAsync(originalAssetUri, ct);

                    if (DocxTemplatePayloadInspector.IsWordProcessingOpenXml(originalDocx) && originalDocx is not null && originalDocx.Content.Length > 0)
                    {
                        bytes = originalDocxTemplateRender.Render(originalDocx.Content, exportContext.Value.Merged);
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        ext = ".docx";
                    }
                    else
                    {
                        bytes = StructuredDocumentDocxExporter.Build(parts.Title, parts.MainText, parts.SignatureText, watermark);
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        ext = ".docx";
                    }

                    break;
                }
                case "txt":
                    bytes = StructuredDocumentHtmlTxtExporter.BuildTxtUtf8(parts, watermark);
                    contentType = "text/plain; charset=utf-8";
                    ext = ".txt";
                    break;
                case "html":
                    bytes = StructuredDocumentHtmlTxtExporter.BuildHtmlUtf8(parts.Title, parts, watermark);
                    contentType = "text/html; charset=utf-8";
                    ext = ".html";
                    break;
                default:
                    return BadRequest(new { message = "Format non pris en charge." });
            }
        }

        var stem = await BuildExportFileStemAsync(gen, ct);
        var fileName = $"{stem}{ext}";
        await LogDocumentDownloadAsync(gen, format, fileName, ct);
        return File(bytes, contentType, fileName);
    }

    private sealed class DocumentWorkflowPreparation
    {
        public ActionResult? FailedResult { get; private init; }
        public DocumentTemplate Template { get; private init; } = null!;
        public DocumentTemplateVersion Version { get; private init; } = null!;
        public Dictionary<string, string> Merged { get; private init; } = null!;
        public IReadOnlyList<string> MissingRequired { get; private init; } = Array.Empty<string>();
        public IReadOnlyList<string> InvalidFormat { get; private init; } = Array.Empty<string>();
        /// <summary>Nombre de variables marquées obligatoires sur la version (pour KPI remplissage).</summary>
        public int RequiredVariableCount { get; private init; }
        public DocumentRequest? LinkedRequest { get; private init; }
        public string TitleFallback { get; private init; } = "";

        public static DocumentWorkflowPreparation Fail(ActionResult error) => new() { FailedResult = error };

        public static DocumentWorkflowPreparation Ok(
            DocumentTemplate template,
            DocumentTemplateVersion version,
            Dictionary<string, string> merged,
            IReadOnlyList<string> missingRequired,
            IReadOnlyList<string> invalidFormat,
            int requiredVariableCount,
            DocumentRequest? linkedRequest,
            string titleFallback) => new()
        {
            FailedResult = null,
            Template = template,
            Version = version,
            Merged = merged,
            MissingRequired = missingRequired,
            InvalidFormat = invalidFormat,
            RequiredVariableCount = requiredVariableCount,
            LinkedRequest = linkedRequest,
            TitleFallback = titleFallback,
        };
    }

    private async Task<DocumentWorkflowPreparation> PrepareDocumentWorkflowAsync(
        DocumentWorkflowRequest req,
        CancellationToken ct)
    {
        if (!userContext.UserId.HasValue)
            return DocumentWorkflowPreparation.Fail(Unauthorized());

        if (req.TemplateId == Guid.Empty)
            return DocumentWorkflowPreparation.Fail(BadRequest(new { message = "templateId est obligatoire." }));

        var template = await db.DocumentTemplates
            .Include(t => t.DocumentType)
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == req.TemplateId, ct);
        if (template is null)
            return DocumentWorkflowPreparation.Fail(NotFound(new { message = "Template introuvable." }));
        if (!template.IsActive)
            return DocumentWorkflowPreparation.Fail(BadRequest(new { message = "Ce template est inactif." }));

        var version = template.CurrentVersion;
        if (version is null)
        {
            version = await db.DocumentTemplateVersions
                .Where(v => v.TemplateId == template.Id)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(ct);
        }

        if (version is null)
        {
            return DocumentWorkflowPreparation.Fail(BadRequest(new { message = "Aucune version de modèle n'est disponible. Publiez une version ou réimportez le fichier." }));
        }

        DocumentRequest? linkedRequest = null;
        if (req.DocumentRequestId is { } linkRequestId && linkRequestId != Guid.Empty)
        {
            linkedRequest = await db.DocumentRequests.FirstOrDefaultAsync(r => r.Id == linkRequestId, ct);
            if (linkedRequest is null)
                return DocumentWorkflowPreparation.Fail(BadRequest(new { message = "Demande introuvable." }));
            if (linkedRequest.Status != DocumentRequestStatus.Approved)
                return DocumentWorkflowPreparation.Fail(BadRequest(new { message = "La demande doit être approuvée avant génération du document." }));
        }

        var merged = await MergeTemplateVariablesAsync(req.BeneficiaryUserId, req.DocumentRequestId, req.Variables, ct);
        var versionId = version.Id;
        var titleFallback = template.DocumentType?.Name ?? template.Name;
        if (string.IsNullOrWhiteSpace(titleFallback))
            titleFallback = template.Code;

        var requiredVariableCount = 0;
        IReadOnlyList<string> missing;
        IReadOnlyList<string> invalid;
        if (template.Kind == DocumentTemplateKind.Static)
        {
            missing = Array.Empty<string>();
            invalid = Array.Empty<string>();
        }
        else
        {
            var variableRows = await db.DocumentTemplateVariables.AsNoTracking()
                .Where(v => v.TemplateVersionId == versionId)
                .Select(v => new { v.VariableName, v.VariableType, v.IsRequired, v.ValidationRule })
                .ToListAsync(ct);
            requiredVariableCount = variableRows.Count(v => v.IsRequired);
            var specs = variableRows
                .Where(v => !string.IsNullOrWhiteSpace(v.VariableName))
                .Select(v => new DetectedTemplateVariable(
                    v.VariableName.Trim(),
                    string.IsNullOrWhiteSpace(v.VariableType) ? "text" : v.VariableType.Trim().ToLowerInvariant(),
                    v.IsRequired,
                    string.IsNullOrWhiteSpace(v.ValidationRule)
                        ? InferStrictValidationRuleByName(v.VariableName)
                        : v.ValidationRule.Trim()))
                .ToList();
            if (specs.Count == 0)
            {
                missing = Array.Empty<string>();
                invalid = Array.Empty<string>();
            }
            else
            {
                var validation = templateEngine.ValidateVariables(specs, merged);
                missing = validation.MissingRequired;
                invalid = validation.InvalidFormat;
            }
        }

        return DocumentWorkflowPreparation.Ok(
            template,
            version,
            merged,
            missing,
            invalid,
            requiredVariableCount,
            linkedRequest,
            titleFallback);
    }

    private async Task<ActionResult<DocumentTemplateGenerateResponse>> CompleteStaticDocumentGenerationAsync(
        DocumentWorkflowRequest req,
        DocumentWorkflowPreparation prep,
        CancellationToken ct)
    {
        var template = prep.Template;
        var version = prep.Version;
        var linkedRequest = prep.LinkedRequest;

        if (string.IsNullOrWhiteSpace(version.OriginalAssetUri))
            return BadRequest(new { message = "Modèle statique sans fichier source." });

        var payload = await templateBlobStorage.TryReadObjectAsync(version.OriginalAssetUri, ct);
        if (payload is null)
            return BadRequest(new { message = "Impossible de lire le fichier modèle (MinIO / taille)." });

        var typeId = template.DocumentTypeId ?? req.DocumentTypeId;
        if (typeId == Guid.Empty)
            typeId = null;

        var now = DateTimeOffset.UtcNow;
        var genId = Guid.NewGuid();
        var fileName = string.IsNullOrWhiteSpace(payload.FileName) ? $"{template.Code}_document" : payload.FileName;
        string storageUri;
        if (templateBlobStorage.IsConfigured)
        {
            await using var stream = new MemoryStream(payload.Content);
            var key = $"{tenantAccessor.ResolvedTenantId.TrimEnd('/')}/generated/{genId:N}/{Uri.EscapeDataString(fileName)}";
            storageUri = await templateBlobStorage.PutTemplateObjectAsync(key, stream, payload.ContentType, ct);
        }
        else
        {
            storageUri = $"inline://generated/{genId:N}/{Uri.EscapeDataString(fileName)}";
        }

        var gen = new GeneratedDocument
        {
            Id = genId,
            DocumentRequestId = req.DocumentRequestId,
            OwnerUserId = userContext.UserId!.Value,
            DocumentTypeId = typeId,
            TemplateVersionId = version.Id,
            FileName = fileName,
            StorageUri = storageUri,
            PdfContent = templateBlobStorage.IsConfigured ? null : payload.Content,
            MimeType = payload.ContentType,
            FileSizeBytes = payload.Content.LongLength,
            Status = GeneratedDocumentStatus.Generated,
            VersionNumber = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.GeneratedDocuments.Add(gen);
        if (linkedRequest is not null)
        {
            linkedRequest.Status = DocumentRequestStatus.Generated;
            linkedRequest.UpdatedAt = now;
            var auditDetails = JsonSerializer.Serialize(new
            {
                generatedDocumentId = gen.Id.ToString("D"),
                fileName,
                templateCode = template.Code,
                templateId = template.Id.ToString("D"),
                staticTemplate = true,
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantAccessor.ResolvedTenantId,
                OccurredAt = now,
                ActorUserId = userContext.UserId,
                Action = "DOCUMENT_GENERATED",
                EntityType = "document_request",
                EntityId = linkedRequest.Id,
                Details = auditDetails,
                Success = true,
                RequestNumber = linkedRequest.RequestNumber,
            });
        }

        await db.SaveChangesAsync(ct);
        return Ok(new DocumentTemplateGenerateResponse(gen.Id.ToString(), fileName, storageUri, gen.Status.ToString()));
    }

    private async Task<ActionResult<DocumentTemplateGenerateResponse>> CompleteRhDraftDocumentGenerationAsync(
        DocumentWorkflowRequest req,
        DocumentWorkflowPreparation prep,
        CancellationToken ct)
    {
        var template = prep.Template;
        var version = prep.Version;
        var merged = prep.Merged;
        var linkedRequest = prep.LinkedRequest;
        var wfOpts = documentWorkflowOptions.Value;

        var structural = templateEngine.ListStructuralResidualsAfterRender(version.StructuredContent, merged);
        if (structural.Count > 0)
        {
            logger.LogWarning(
                "Génération bloquée : marqueurs résiduels sur le modèle {TemplateCode} : {Residuals}",
                template.Code,
                string.Join(", ", structural));
            return BadRequest(new
            {
                message =
                    "Le document contient encore des marqueurs non remplis ((X), {{variable}}, masques date, tirets, etc.). Complétez les données ou le modèle avant de générer.",
                structuralResiduals = structural,
            });
        }

        var contentGeneratedStrict = templateEngine.RenderContent(version.StructuredContent, merged);

        var missingPh = string.IsNullOrWhiteSpace(wfOpts.MissingFieldPlaceholder)
            ? "________"
            : wfOpts.MissingFieldPlaceholder.Trim();
        var displayMerged = new Dictionary<string, string>(merged, StringComparer.OrdinalIgnoreCase);
        if (wfOpts.MarkMissingFieldsInRhDraft)
        {
            foreach (var name in prep.MissingRequired)
            {
                if (!displayMerged.TryGetValue(name, out var v) || string.IsNullOrWhiteSpace(v))
                    displayMerged[name] = missingPh;
            }
        }

        var initialRhText = wfOpts.MarkMissingFieldsInRhDraft
            ? templateEngine.RenderContent(version.StructuredContent, displayMerged)
            : contentGeneratedStrict;

        var typeId = template.DocumentTypeId ?? req.DocumentTypeId;
        if (typeId == Guid.Empty)
            typeId = null;

        var now = DateTimeOffset.UtcNow;
        var genId = Guid.NewGuid();
        var draftLabel = $"{template.Code}_brouillon";

        var gen = new GeneratedDocument
        {
            Id = genId,
            DocumentRequestId = req.DocumentRequestId,
            OwnerUserId = userContext.UserId!.Value,
            DocumentTypeId = typeId,
            TemplateVersionId = version.Id,
            FileName = $"{draftLabel}.pdf",
            StorageUri = string.Empty,
            PdfContent = null,
            MimeType = null,
            FileSizeBytes = null,
            Status = GeneratedDocumentStatus.DraftPendingRhReview,
            VersionNumber = 1,
            ContentGenerated = contentGeneratedStrict,
            ContentFinal = initialRhText,
            RhMissingVariablesJson = prep.MissingRequired.Count > 0
                ? JsonSerializer.Serialize(prep.MissingRequired)
                : null,
            // Même fusion que l’aperçu / le formulaire (incl. champs RH non persistés en base).
            WorkflowVariablesSnapshotJson = JsonSerializer.Serialize(prep.Merged),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.GeneratedDocuments.Add(gen);

        if (linkedRequest is not null)
        {
            var auditDetails = JsonSerializer.Serialize(new
            {
                generatedDocumentId = gen.Id.ToString("D"),
                templateCode = template.Code,
                templateId = template.Id.ToString("D"),
                draftPendingRh = true,
                missingVariables = prep.MissingRequired,
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantAccessor.ResolvedTenantId,
                OccurredAt = now,
                ActorUserId = userContext.UserId,
                Action = "DOCUMENT_DRAFT_CREATED",
                EntityType = "document_request",
                EntityId = linkedRequest.Id,
                Details = auditDetails,
                Success = true,
                RequestNumber = linkedRequest.RequestNumber,
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new DocumentTemplateGenerateResponse(
            gen.Id.ToString("D"),
            gen.FileName,
            string.Empty,
            gen.Status.ToString(),
            true,
            prep.MissingRequired));
    }

    private static IReadOnlyList<string> DeserializeRhMissingVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is null || list.Count == 0)
                return Array.Empty<string>();
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Appelle l’IA (sans moteur local de remplacement) puis valide placeholders résiduels et données critiques.</summary>
    private async Task<(string? Document, IActionResult? Error)> TryAiDirectFillValidatedAsync(
        AiDirectDocumentFillRequest body,
        CancellationToken ct)
    {
        var outcome = await aiDirectOrchestrator.FillAsync(body, AiDirectFillValidationPolicy.Full, ct);
        if (!outcome.Success)
            return (null, StatusCode(outcome.ErrorStatusCode, outcome.ErrorBody));
        return (outcome.Document, null);
    }

    private async Task<bool> IsGeneratedDocumentInTenantAsync(GeneratedDocument gen, CancellationToken ct)
    {
        if (gen.DocumentRequestId is not { } rid)
            return true;
        var tenant = tenantAccessor.ResolvedTenantId.Trim();
        var rowTenant = await db.DocumentRequests.AsNoTracking()
            .Where(r => r.Id == rid)
            .Select(r => r.TenantId)
            .FirstOrDefaultAsync(ct);
        return string.Equals((rowTenant ?? string.Empty).Trim(), tenant, StringComparison.Ordinal);
    }

    private async Task<ActionResult<DocumentTemplateGenerateResponse>> CompleteDocumentGenerationAsync(
        DocumentWorkflowRequest req,
        DocumentWorkflowPreparation prep,
        CancellationToken ct)
    {
        var template = prep.Template;
        var version = prep.Version;
        var merged = prep.Merged;
        var linkedRequest = prep.LinkedRequest;

        if (template.Kind == DocumentTemplateKind.Static)
            return await CompleteStaticDocumentGenerationAsync(req, prep, ct);

        // Workflow RH imposé pour les templates dynamiques:
        // génération => brouillon modifiable RH ; finalisation explicite ensuite.
        if (template.Kind == DocumentTemplateKind.Dynamic)
            return await CompleteRhDraftDocumentGenerationAsync(req, prep, ct);

        var typeId = template.DocumentTypeId ?? req.DocumentTypeId;
        if (typeId == Guid.Empty)
            typeId = null;

        var rendered = templateEngine.RenderContent(version.StructuredContent, merged);
        var now = DateTimeOffset.UtcNow;
        var genId = Guid.NewGuid();
        var (fileName, pdfBytes) = pdfExport.BuildPdf(
            template.Code,
            tenantAccessor.ResolvedTenantId,
            rendered,
            prep.TitleFallback);
        string storageUri;
        if (templateBlobStorage.IsConfigured)
        {
            await using var stream = new MemoryStream(pdfBytes);
            var key = $"{tenantAccessor.ResolvedTenantId.TrimEnd('/')}/generated/{genId:N}/{fileName}";
            storageUri = await templateBlobStorage.PutTemplateObjectAsync(key, stream, "application/pdf", ct);
        }
        else
        {
            storageUri = $"inline://generated/{genId:N}/{Uri.EscapeDataString(fileName)}";
        }

        var gen = new GeneratedDocument
        {
            Id = genId,
            DocumentRequestId = req.DocumentRequestId,
            OwnerUserId = userContext.UserId!.Value,
            DocumentTypeId = typeId,
            TemplateVersionId = version.Id,
            FileName = fileName,
            StorageUri = storageUri,
            PdfContent = templateBlobStorage.IsConfigured ? null : pdfBytes,
            MimeType = "application/pdf",
            FileSizeBytes = pdfBytes.LongLength,
            Status = GeneratedDocumentStatus.Generated,
            VersionNumber = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.GeneratedDocuments.Add(gen);
        if (linkedRequest is not null)
        {
            linkedRequest.Status = DocumentRequestStatus.Generated;
            linkedRequest.UpdatedAt = now;
            var auditDetails = JsonSerializer.Serialize(new
            {
                generatedDocumentId = gen.Id.ToString("D"),
                fileName,
                templateCode = template.Code,
                templateId = template.Id.ToString("D"),
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantAccessor.ResolvedTenantId,
                OccurredAt = now,
                ActorUserId = userContext.UserId,
                Action = "DOCUMENT_GENERATED",
                EntityType = "document_request",
                EntityId = linkedRequest.Id,
                Details = auditDetails,
                Success = true,
                RequestNumber = linkedRequest.RequestNumber,
            });
        }

        await db.SaveChangesAsync(ct);
        return Ok(new DocumentTemplateGenerateResponse(gen.Id.ToString(), fileName, storageUri, gen.Status.ToString()));
    }

    /// <summary>Enrichit les valeurs fusionnées via OpenAI (civilité, CIN, poste vs service) si configuré.</summary>
    private async Task ApplyAiVariableRefinementAsync(
        Dictionary<string, string> merged,
        Guid templateVersionId,
        string? documentTitle,
        CancellationToken ct)
    {
        if (!aiTemplateOptions.Value.EnableVariableRefinementOnGenerate)
            return;

        if (merged.Count == 0)
            return;

        var nameRows = await db.DocumentTemplateVariables.AsNoTracking()
            .Where(v => v.TemplateVersionId == templateVersionId)
            .Select(v => v.VariableName)
            .ToListAsync(ct);

        var templateVarNames = nameRows.Count > 0
            ? (IReadOnlyList<string>)nameRows
            : merged.Keys.ToList();

        IReadOnlyDictionary<string, string> updates;
        try
        {
            updates = await aiTemplateGenerator
                .RefineMergedVariablesForDocumentAsync(merged, templateVarNames, documentTitle, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IA refinement failed for template version {VersionId}", templateVersionId);
            return;
        }

        if (updates.Count == 0)
            return;

        foreach (var kv in updates)
        {
            if (string.Equals(kv.Key, "role", StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsSensitiveHrField(kv.Key))
                continue;
            if (!merged.TryGetValue(kv.Key, out var existing) || string.IsNullOrWhiteSpace(existing))
                merged[kv.Key] = kv.Value;
        }
    }

    private static bool IsSensitiveHrField(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return key.Trim().ToLowerInvariant() switch
        {
            "civilite" => true,
            "cin" => true,
            "rib" => true,
            "compte_bancaire" => true,
            "numero_compte" => true,
            "iban" => true,
            "salaire" => true,
            "salary" => true,
            "matricule" => true,
            "numero_cnss" => true,
            "cnss" => true,
            "nom" => true,
            "prenom" => true,
            "nom_complet" => true,
            "prenom_nom" => true,
            "poste" => true,
            "email" => true,
            "telephone" => true,
            "tel" => true,
            "date_naissance" => true,
            "date_embauche" => true,
            "employe" => true,
            "numero_securite_sociale" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Copie fusionnée avec marqueurs pour rendu PDF / HTML (variables obligatoires encore vides).
    /// </summary>
    private static Dictionary<string, string> MergeWithMissingPlaceholders(
        IReadOnlyDictionary<string, string> merged,
        IReadOnlyList<string> missingRequired,
        string? configuredPlaceholder)
    {
        if (missingRequired.Count == 0)
            return new Dictionary<string, string>(merged, StringComparer.OrdinalIgnoreCase);
        var ph = string.IsNullOrWhiteSpace(configuredPlaceholder) ? "________" : configuredPlaceholder.Trim();
        var d = new Dictionary<string, string>(merged, StringComparer.OrdinalIgnoreCase);
        foreach (var name in missingRequired)
        {
            if (!d.TryGetValue(name, out var v) || string.IsNullOrWhiteSpace(v))
                d[name] = ph;
        }

        return d;
    }

    /// <summary>
    /// Priorité : annuaire (données « DB » métier) → champs persistés de la demande → variables explicites du payload.
    /// L’IA s’applique ensuite (<see cref="ApplyAiVariableRefinementAsync"/>), sans inventer les champs sensibles.
    /// </summary>
    private async Task<Dictionary<string, string>> MergeTemplateVariablesAsync(
        Guid? beneficiaryUserId,
        Guid? documentRequestId,
        IReadOnlyDictionary<string, string>? source,
        CancellationToken ct)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var targetId = beneficiaryUserId;
        if (!targetId.HasValue && documentRequestId.HasValue)
        {
            var req = await db.DocumentRequests.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == documentRequestId.Value, ct);
            if (req is not null)
                targetId = req.BeneficiaryUserId ?? req.RequesterUserId;
        }

        if (targetId.HasValue)
        {
            var row = await db.DirectoryUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == targetId.Value, ct);
            if (row is not null)
            {
                var mapped = await MapDirectoryUserAsync(row, ct);

                void SetFromDirectory(string key, string value)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        return;
                    dict[key] = NormalizeMergedFieldValue(key, value);
                }

                SetFromDirectory("prenom", mapped.Prenom);
                SetFromDirectory("nom", mapped.Nom);
                SetFromDirectory("email", mapped.Email);
                SetFromDirectory("role", mapped.Role);
                SetFromDirectory("pole", mapped.Pole?.Name ?? "");
                SetFromDirectory("cellule", mapped.Cellule?.Name ?? "");
                SetFromDirectory("departement", mapped.Departement?.Name ?? "");

                var nomComplet = $"{mapped.Prenom} {mapped.Nom}".Trim();
                SetFromDirectory("nom_complet", nomComplet);
                SetFromDirectory("prenom_nom", nomComplet);
                SetFromDirectory("nom_employe", nomComplet);
                SetFromDirectory("prenom_employe", mapped.Prenom);
                SetFromDirectory("nom_pilote", nomComplet);
            }
        }

        if (documentRequestId.HasValue)
        {
            var persisted = await db.DocumentRequestFieldValues.AsNoTracking()
                .Where(f => f.DocumentRequestId == documentRequestId.Value)
                .ToListAsync(ct);
            foreach (var persistedRow in persisted)
            {
                var key = persistedRow.FieldName.Trim();
                if (string.IsNullOrEmpty(key))
                    continue;
                dict[key] = NormalizeMergedFieldValue(key, persistedRow.FieldValue ?? "");
            }
        }

        if (source is not null)
        {
            foreach (var kv in source)
            {
                var key = kv.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                dict[key] = NormalizeMergedFieldValue(key, kv.Value ?? "");
            }
        }

        EnsureFrenchDateAlias(dict);
        return dict;
    }

    private static void EnsureFrenchDateAlias(Dictionary<string, string> dict)
    {
        if (dict.TryGetValue("date_fr", out var df) && !string.IsNullOrWhiteSpace(df))
            return;
        if (!dict.TryGetValue("date", out var iso) || string.IsNullOrWhiteSpace(iso))
            return;
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || DateTime.TryParse(iso, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out d))
            dict["date_fr"] = d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
    }

    private static IReadOnlyList<string> DetectUnresolvedLegacyMarkers(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<string>();
        var markers = LegacyMissingMarkerRegex.Matches(content)
            .Select(m => m.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        return markers;
    }

    private string? InferStrictValidationRuleByName(string variableName)
    {
        var k = (variableName ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(k))
            return null;

        if (k.Contains("cin", StringComparison.Ordinal))
            return @"^[A-Za-z]{1,2}[0-9]{6}$";
        if (k.Contains("rib", StringComparison.Ordinal) || k.Contains("compte_bancaire", StringComparison.Ordinal))
            return ribValidation.DigitsOnlyValidationPattern;
        if (k.Contains("telephone", StringComparison.Ordinal) || k.Contains("phone", StringComparison.Ordinal) || k == "tel")
            return @"^\+?[0-9]{10,15}$";
        if (k.Contains("email", StringComparison.Ordinal) || k.Contains("courriel", StringComparison.Ordinal))
            return @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (k == "nom" || k == "prenom")
            return @"^[A-ZÀ-Ý][A-Za-zÀ-ÖØ-öø-ÿ'\- ]*$";
        if (k.Contains("date", StringComparison.Ordinal))
            return @"^(0[1-9]|[12][0-9]|3[01])/(0[1-9]|1[0-2])/[0-9]{4}$";

        return null;
    }

    private static string NormalizeMergedFieldValue(string key, string value)
    {
        var v = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(v))
            return string.Empty;

        var lower = v.ToLowerInvariant();
        if (lower is "-" or "—" or "_" or "x" or "(x)" or "()" or "( )")
            return string.Empty;
        if (IsPlaceholderTokenValue(v))
            return string.Empty;
        if (!IsSensitiveHrField(key))
            return v;
        if (lower.Contains("pilote", StringComparison.Ordinal)
            || lower.Contains("coach", StringComparison.Ordinal)
            || lower.Contains("manager", StringComparison.Ordinal)
            || lower.Contains("admin", StringComparison.Ordinal)
            || lower.Contains("test", StringComparison.Ordinal)
            || lower.Contains("demo", StringComparison.Ordinal)
            || lower.Contains("n/a", StringComparison.Ordinal)
            || lower.Contains("xxx", StringComparison.Ordinal))
            return string.Empty;

        return v;
    }

    private static bool IsPlaceholderTokenValue(string value)
    {
        var t = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(t))
            return true;
        if (t.Length > 6)
            return false;
        if (t.Equals("x", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Equals("(x)", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t is "()" or "( )" or "-" or "—" or "_")
            return true;
        if (t.All(ch => ch == '_' || ch == '-' || ch == '—' || char.IsWhiteSpace(ch)))
            return true;
        return false;
    }

    private async Task<bool> CanAccessGeneratedDocumentAsync(GeneratedDocument g, CancellationToken ct)
    {
        if (!userContext.UserId.HasValue || !userContext.Role.HasValue)
            return false;
        if (g.OwnerUserId == userContext.UserId.Value)
            return true;
        if (userContext.Role.Value is AppRole.Rh or AppRole.Admin or AppRole.Audit)
            return true;

        if (!g.DocumentRequestId.HasValue)
            return false;

        var req = await db.DocumentRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == g.DocumentRequestId.Value, ct);
        if (req is null)
            return false;

        // Même périmètre que la liste des demandes : pilote, coach, manager/RP selon scope, etc.
        return await CanActorViewDocumentRequestAsync(req, ct);
    }

    private async Task<IReadOnlyList<DirectoryUserResponse>> MapDirectoryUsersAsync(IReadOnlyList<DirectoryUser> rows, CancellationToken ct)
    {
        var ids = rows.SelectMany(u => new[] { u.PoleId, u.CelluleId, u.DepartementId }).Distinct().ToArray();
        var units = await LoadOrgUnitsByIdsAsync(ids, ct);
        return rows.Select(u => DirectoryUserMapper.ToResponse(
            u,
            units.GetValueOrDefault(u.PoleId),
            units.GetValueOrDefault(u.CelluleId),
            units.GetValueOrDefault(u.DepartementId))).ToList();
    }

    private async Task<DirectoryUserResponse> MapDirectoryUserAsync(DirectoryUser row, CancellationToken ct)
    {
        var ids = new[] { row.PoleId, row.CelluleId, row.DepartementId };
        var units = await LoadOrgUnitsByIdsAsync(ids, ct);
        return DirectoryUserMapper.ToResponse(
            row,
            units.GetValueOrDefault(row.PoleId),
            units.GetValueOrDefault(row.CelluleId),
            units.GetValueOrDefault(row.DepartementId));
    }

    private async Task<Dictionary<Guid, OrganisationUnit>> LoadOrgUnitsByIdsAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
            return new Dictionary<Guid, OrganisationUnit>();
        return await db.OrganisationUnits.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    private ActionResult MapTemplateUploadException(InvalidOperationException ex)
    {
        var msg = ex.Message;
        if (msg.Contains("existe déjà", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = msg });
        if (msg.Contains("non configuré", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Stockage MinIO", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("MinIO / S3", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("MinIO : impossible", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Échec d’envoi vers MinIO", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Impossible de joindre MinIO", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = msg });
        return BadRequest(new { message = msg });
    }

    private static string NormalizeSource(string source)
    {
        var normalized = source.Trim().ToUpperInvariant();
        return normalized is "UPLOAD" or "RULE_BASED" or "AI_GENERATED" ? normalized : "UPLOAD";
    }

    private static string NormalizeVersionStatus(string status)
    {
        var normalized = status.Trim().ToLowerInvariant();
        return normalized is "draft" or "published" or "archived" ? normalized : "draft";
    }

    private async Task<DocumentTemplateVersion> CreateTemplateVersionInternalAsync(
        DocumentTemplate template,
        string structuredContent,
        string status,
        string? originalAssetUri,
        IReadOnlyList<TemplateVariableInput> variables,
        Guid? createdByUserId,
        CancellationToken ct)
    {
        var maxVersion = await db.DocumentTemplateVersions
            .Where(v => v.TemplateId == template.Id)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        var now = DateTimeOffset.UtcNow;
        var version = new DocumentTemplateVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            TenantId = tenantAccessor.ResolvedTenantId,
            VersionNumber = maxVersion + 1,
            Status = status,
            StructuredContent = string.IsNullOrWhiteSpace(structuredContent) ? "{}" : structuredContent,
            OriginalAssetUri = string.IsNullOrWhiteSpace(originalAssetUri) ? null : originalAssetUri.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            PublishedAt = status == "published" ? now : null,
        };
        db.DocumentTemplateVersions.Add(version);
        await db.SaveChangesAsync(ct);

        var rows = variables.Select((v, index) => new DocumentTemplateVariable
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            TemplateVersionId = version.Id,
            VariableName = v.Name.Trim(),
            VariableType = string.IsNullOrWhiteSpace(v.Type) ? "text" : v.Type.Trim().ToLowerInvariant(),
            IsRequired = v.IsRequired,
            DefaultValue = string.IsNullOrWhiteSpace(v.DefaultValue) ? null : v.DefaultValue.Trim(),
            ValidationRule = string.IsNullOrWhiteSpace(v.ValidationRule) ? null : v.ValidationRule.Trim(),
            DisplayLabel = string.IsNullOrWhiteSpace(v.DisplayLabel) ? null : v.DisplayLabel.Trim(),
            FormScope = NormalizeFormScope(v.FormScope),
            SourcePriority = v.SourcePriority ?? GuessSourcePriority(v.FormScope),
            NormalizedName = string.IsNullOrWhiteSpace(v.NormalizedName) ? null : v.NormalizedName.Trim(),
            RawPlaceholder = string.IsNullOrWhiteSpace(v.RawPlaceholder) ? null : v.RawPlaceholder.Trim(),
            SortOrder = index,
        })
            .Where(v => IsValidVariableName(v.VariableName))
            .Take(MaxTemplateVariables)
            .ToList();
        if (rows.Count > 0)
            db.DocumentTemplateVariables.AddRange(rows);
        await db.SaveChangesAsync(ct);
        return version;
    }

    private static DocumentTemplateVersionResponse MapVersionResponse(
        DocumentTemplateVersion version,
        IReadOnlyList<DocumentTemplateVariableResponse> variables) =>
        new(
            version.Id.ToString(),
            version.VersionNumber,
            version.Status,
            SanitizeForJson(version.StructuredContent),
            version.OriginalAssetUri,
            version.CreatedAt.ToString("O"),
            version.PublishedAt?.ToString("O"),
            variables);

    /// <summary>
    /// Certains contenus legacy peuvent contenir des surrogates UTF-16 invalides
    /// (copier/coller Word / extraction DOCX). On les retire pour éviter 500 à la sérialisation JSON.
    /// </summary>
    private static string SanitizeForJson(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;
        var sb = new System.Text.StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsHighSurrogate(ch))
            {
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    sb.Append(ch);
                    sb.Append(value[++i]);
                }
                continue;
            }
            if (char.IsLowSurrogate(ch))
                continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static PostgresException? FindPostgresException(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is PostgresException pg)
                return pg;
            ex = ex.InnerException;
        }

        return null;
    }

    private static DocumentTemplateVariableResponse MapVariableResponse(DocumentTemplateVariable v) =>
        new(
            v.Id.ToString(),
            v.VariableName,
            v.VariableType,
            v.IsRequired,
            v.DefaultValue,
            v.ValidationRule,
            v.DisplayLabel,
            NormalizeFormScope(v.FormScope),
            v.SourcePriority,
            v.NormalizedName,
            v.RawPlaceholder,
            v.SortOrder);

    private static DocumentTemplateVariableResponse ToVariableResponse(TemplateVariableInput v, int order) =>
        new(
            Guid.Empty.ToString(),
            v.Name,
            v.Type,
            v.IsRequired,
            v.DefaultValue,
            v.ValidationRule,
            v.DisplayLabel,
            NormalizeFormScope(v.FormScope),
            v.SourcePriority ?? GuessSourcePriority(v.FormScope),
            v.NormalizedName,
            v.RawPlaceholder,
            order);

    private void AddDocumentRequestFieldValueRows(
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

    private static bool IsValidVariableName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static bool IsValidTemplateCode(string code) =>
        !string.IsNullOrWhiteSpace(code) && code.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');

    private static string NormalizeFormScope(string? formScope)
    {
        var normalized = (formScope ?? "pilot").Trim().ToLowerInvariant();
        return normalized is "pilot" or "hr" or "both" or "db" ? normalized : "pilot";
    }

    private static int GuessSourcePriority(string? formScope) =>
        NormalizeFormScope(formScope) switch
        {
            "db" => 10,
            "pilot" => 20,
            "both" => 25,
            "hr" => 30,
            _ => 20,
        };

    /// <summary>Pilote (demandeur / bénéficiaire), RH et Admin peuvent enregistrer les champs du formulaire lié à la demande.</summary>
    private bool CanActorEditDocumentRequestFieldValues(DocumentRequest r)
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

    /// <summary>Aligné sur les filtres de <see cref="GetDocumentRequests"/> — refuse la fuite hors périmètre rôle.</summary>
    private async Task<bool> CanActorViewDocumentRequestAsync(DocumentRequest r, CancellationToken ct)
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

    public sealed class WorkflowValidatePutBody
    {
        public string? Comment { get; set; }
    }

    public sealed class WorkflowRejectPutBody
    {
        public string? RejectionReason { get; set; }
    }

    public sealed class CreateDocumentRequestBody
    {
        public Guid? RequesterUserId { get; set; }
        public Guid? BeneficiaryUserId { get; set; }
        public string? DocumentTypeId { get; set; }
        public bool IsCustomType { get; set; }
        public string? CustomTypeDescription { get; set; }
        public string? Reason { get; set; }
        public string? ComplementaryComments { get; set; }

        /// <summary>Modèle RH choisi par le pilote (prioritaire sur type catalogue / « Autre »).</summary>
        public string? DocumentTemplateId { get; set; }

        /// <summary>Valeurs initiales pour les variables du modèle (noms de champs = clés template).</summary>
        public Dictionary<string, string>? InitialFieldValues { get; set; }
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

        if (s == "desc")
            return true;

        return false;
    }

    private enum RequestSortField
    {
        CreatedAt,
        Status,
        RequestNumber,
    }

    private enum AuditSortField
    {
        OccurredAt,
        Action,
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

    private static bool TryParseAuditSortField(string? sortBy, out AuditSortField field)
    {
        field = AuditSortField.OccurredAt;
        if (string.IsNullOrWhiteSpace(sortBy))
            return true;

        switch (sortBy.Trim().ToLowerInvariant())
        {
            case "occurredat":
                field = AuditSortField.OccurredAt;
                return true;
            case "action":
                field = AuditSortField.Action;
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

    private static IQueryable<AuditLog> ApplyAuditSort(IQueryable<AuditLog> q, AuditSortField sortField, bool desc) =>
        sortField switch
        {
            AuditSortField.Action => desc
                ? q.OrderByDescending(a => a.Action).ThenByDescending(a => a.OccurredAt)
                : q.OrderBy(a => a.Action).ThenByDescending(a => a.OccurredAt),
            _ => desc ? q.OrderByDescending(a => a.OccurredAt) : q.OrderBy(a => a.OccurredAt),
        };
}
