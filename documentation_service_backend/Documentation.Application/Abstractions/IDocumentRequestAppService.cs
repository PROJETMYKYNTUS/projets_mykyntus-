using Documentation.Application.Api;
using Documentation.Domain.Entities;

namespace Documentation.Application.Abstractions;

public interface IDocumentRequestAppService
{
    Task<PagedResponse<DocumentRequestResponse>> ListAsync(DocumentRequestListQuery query, CancellationToken ct = default);
    Task<DocumentRequestResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentRequestFieldValuesResponse?> GetFieldValuesAsync(Guid id, CancellationToken ct = default);
    Task<DocumentRequestFieldValuesResponse> PutFieldValuesAsync(Guid id, PutDocumentRequestFieldValuesRequest body, CancellationToken ct = default);
    Task<DocumentRequestResponse> CreateAsync(CreateDocumentRequestBody body, CancellationToken ct = default);
    Task<bool> CanActorViewAsync(DocumentRequest request, CancellationToken ct = default);
}
