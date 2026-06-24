using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Documentation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Documentation.Infrastructure.Services;

public sealed class DocumentTypeQueryService(DocumentationDbContext db) : IDocumentTypeQueryService
{
    public async Task<IReadOnlyList<DocumentTypeResponse>> ListAsync(CancellationToken ct = default)
    {
        return await db.DocumentTypes
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
    }
}
