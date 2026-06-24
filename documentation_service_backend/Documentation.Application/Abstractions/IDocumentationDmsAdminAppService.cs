using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IDocumentationDmsAdminAppService
{
    Task<AdminGeneralConfigDto?> GetGeneralConfigAsync(CancellationToken ct = default);
    Task<AdminGeneralConfigDto?> SaveGeneralConfigAsync(AdminGeneralConfigDto body, CancellationToken ct = default);
    Task<List<AdminDocTypeDto>> GetDocTypesAsync(CancellationToken ct = default);
    Task<AdminDocTypeDto> CreateDocTypeAsync(CreateDocTypeRequestDto payload, CancellationToken ct = default);
    Task<AdminDocTypeDto?> UpdateDocTypeAsync(Guid id, CreateDocTypeRequestDto payload, CancellationToken ct = default);
    Task<bool?> DeleteDocTypeAsync(Guid id, CancellationToken ct = default);
    Task<List<AdminWorkflowDefinitionDto>> GetWorkflowDefinitionsAsync(CancellationToken ct = default);
    Task<AdminWorkflowDefinitionDto?> UpdateWorkflowDefinitionAsync(Guid id, AdminWorkflowDefinitionDto body, CancellationToken ct = default);
    Task<List<AdminPermissionPolicyDto>> GetPermissionPoliciesAsync(CancellationToken ct = default);
    Task<List<AdminPermissionPolicyDto>> SavePermissionPoliciesAsync(List<AdminPermissionPolicyDto> body, CancellationToken ct = default);
    Task<AdminStorageConfigDto?> GetStorageConfigAsync(CancellationToken ct = default);
    Task<AdminStorageConfigDto?> SaveStorageConfigAsync(AdminStorageConfigDto body, CancellationToken ct = default);
    IReadOnlyList<string> GetAdminRoles();
}
