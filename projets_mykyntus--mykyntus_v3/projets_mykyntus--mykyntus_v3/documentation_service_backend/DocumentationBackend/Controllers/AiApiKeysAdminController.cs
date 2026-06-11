using DocumentationBackend.Api;
using DocumentationBackend.Context;
using DocumentationBackend.Data;
using DocumentationBackend.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DocumentationBackend.Controllers;

/// <summary>Gestion des clés API IA par tenant (admin uniquement).</summary>
[ApiController]
[Route("api/documentation/data/admin/ai-api-keys")]
public sealed class AiApiKeysAdminController(
    DocumentationDbContext db,
    DocumentationUserContext userContext,
    IDocumentationTenantAccessor tenantAccessor,
    ILogger<AiApiKeysAdminController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AiApiKeyListItemResponse>>> List(CancellationToken ct)
    {
        if (!EnsureAdmin(out var forbidden))
            return forbidden;
        var rows = await db.AiApiKeys.AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        logger.LogDebug(
            "List AI keys tenant={Tenant} total={Total} active={Active}",
            tenantAccessor.ResolvedTenantId,
            rows.Count,
            rows.Count(k => k.IsActive));
        return Ok(rows.Select(MapListItem).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AiApiKeyListItemResponse>> Create([FromBody] CreateAiApiKeyRequest body, CancellationToken ct)
    {
        if (!EnsureAdmin(out var forbidden))
            return forbidden;
        if (string.IsNullOrWhiteSpace(body.ApiKey))
            return BadRequest(new { message = "apiKey est obligatoire." });
        var provider = string.IsNullOrWhiteSpace(body.Provider) ? "openai" : body.Provider.Trim().ToLowerInvariant();
        if (provider.Length > 32)
            return BadRequest(new { message = "provider trop long." });

        var tenant = tenantAccessor.ResolvedTenantId;
        var beforeRows = await db.AiApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        logger.LogDebug(
            "Create AI key before tenant={Tenant} provider={Provider} setActive={SetActive} beforeTotal={Total}",
            tenant,
            provider,
            body.SetActive,
            beforeRows.Count);
        if (body.SetActive)
        {
            var actives = await db.AiApiKeys.Where(k => k.IsActive).ToListAsync(ct).ConfigureAwait(false);
            foreach (var k in actives)
                k.IsActive = false;
        }

        var entity = new AiApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            Provider = provider,
            Label = string.IsNullOrWhiteSpace(body.Label) ? null : body.Label.Trim(),
            ApiKey = body.ApiKey.Trim(),
            IsActive = body.SetActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.AiApiKeys.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        var afterRows = await db.AiApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "AI API key created id={KeyId} tenant={TenantId} active={IsActive} afterTotal={Total}",
            entity.Id,
            tenant,
            entity.IsActive,
            afterRows.Count);
        return Ok(MapListItem(entity));
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<ActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (!EnsureAdmin(out var forbidden))
            return forbidden;
        var key = await db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct).ConfigureAwait(false);
        if (key is null)
            return NotFound();
        var others = await db.AiApiKeys.Where(k => k.Id != id && k.IsActive).ToListAsync(ct).ConfigureAwait(false);
        foreach (var o in others)
            o.IsActive = false;
        key.IsActive = true;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("AI API key activated id={KeyId}", id);
        return NoContent();
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!EnsureAdmin(out var forbidden))
            return forbidden;
        var key = await db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct).ConfigureAwait(false);
        if (key is null)
            return NotFound();
        key.IsActive = false;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!EnsureAdmin(out var forbidden))
            return forbidden;
        var key = await db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct).ConfigureAwait(false);
        if (key is null)
            return NotFound();
        db.AiApiKeys.Remove(key);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("AI API key deleted id={KeyId}", id);
        return NoContent();
    }

    private bool EnsureAdmin(out ActionResult forbidden)
    {
        if (!userContext.UserId.HasValue || userContext.Role != AppRole.Admin)
        {
            forbidden = Forbid();
            return false;
        }

        forbidden = null!;
        return true;
    }

    private static AiApiKeyListItemResponse MapListItem(AiApiKey k)
    {
        var raw = k.ApiKey ?? "";
        var preview = raw.Length <= 4 ? "****" : "…" + raw[^4..];
        return new AiApiKeyListItemResponse
        {
            Id = k.Id.ToString(),
            Provider = k.Provider,
            Label = k.Label,
            IsActive = k.IsActive,
            CreatedAt = k.CreatedAt.ToString("O"),
            KeyPreview = preview,
        };
    }
}
