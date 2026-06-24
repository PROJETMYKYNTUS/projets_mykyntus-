using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IDocumentTypeQueryService
{
    Task<IReadOnlyList<DocumentTypeResponse>> ListAsync(CancellationToken ct = default);
}
