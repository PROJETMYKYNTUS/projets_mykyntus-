using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IAiApiKeyAdminAppService
{
    Task<List<AiApiKeyListItemResponse>> ListAsync(CancellationToken ct = default);
    Task<AiApiKeyListItemResponse> CreateAsync(CreateAiApiKeyRequest body, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
