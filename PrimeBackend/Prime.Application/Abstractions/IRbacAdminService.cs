using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IRbacAdminService
{
    Task<IReadOnlyList<RbacPermissionDto>> ListAsync(CancellationToken ct = default);
    Task<RbacCatalogDto> GetCatalogAsync(CancellationToken ct = default);
    Task<RbacPermissionDto> UpsertAsync(UpsertRbacPermissionRequest body, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
