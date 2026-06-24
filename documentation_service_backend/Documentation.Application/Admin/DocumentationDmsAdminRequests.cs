using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.Admin;

public record GetDmsGeneralConfigQuery : IRequest<AdminGeneralConfigDto?>;
public sealed class GetDmsGeneralConfigQueryHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<GetDmsGeneralConfigQuery, AdminGeneralConfigDto?>
{
    public Task<AdminGeneralConfigDto?> Handle(GetDmsGeneralConfigQuery request, CancellationToken ct) =>
        admin.GetGeneralConfigAsync(ct);
}

public record SaveDmsGeneralConfigCommand(AdminGeneralConfigDto Body) : IRequest<AdminGeneralConfigDto?>;
public sealed class SaveDmsGeneralConfigCommandHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<SaveDmsGeneralConfigCommand, AdminGeneralConfigDto?>
{
    public Task<AdminGeneralConfigDto?> Handle(SaveDmsGeneralConfigCommand request, CancellationToken ct) =>
        admin.SaveGeneralConfigAsync(request.Body, ct);
}

public record GetAdminDocTypesQuery : IRequest<List<AdminDocTypeDto>>;
public sealed class GetAdminDocTypesQueryHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<GetAdminDocTypesQuery, List<AdminDocTypeDto>>
{
    public Task<List<AdminDocTypeDto>> Handle(GetAdminDocTypesQuery request, CancellationToken ct) =>
        admin.GetDocTypesAsync(ct);
}

public record CreateAdminDocTypeCommand(CreateDocTypeRequestDto Body) : IRequest<AdminDocTypeDto>;
public sealed class CreateAdminDocTypeCommandHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<CreateAdminDocTypeCommand, AdminDocTypeDto>
{
    public Task<AdminDocTypeDto> Handle(CreateAdminDocTypeCommand request, CancellationToken ct) =>
        admin.CreateDocTypeAsync(request.Body, ct);
}

public record UpdateAdminDocTypeCommand(Guid Id, CreateDocTypeRequestDto Body) : IRequest<AdminDocTypeDto?>;
public sealed class UpdateAdminDocTypeCommandHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<UpdateAdminDocTypeCommand, AdminDocTypeDto?>
{
    public Task<AdminDocTypeDto?> Handle(UpdateAdminDocTypeCommand request, CancellationToken ct) =>
        admin.UpdateDocTypeAsync(request.Id, request.Body, ct);
}

public record DeleteAdminDocTypeCommand(Guid Id) : IRequest<bool?>;
public sealed class DeleteAdminDocTypeCommandHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<DeleteAdminDocTypeCommand, bool?>
{
    public Task<bool?> Handle(DeleteAdminDocTypeCommand request, CancellationToken ct) =>
        admin.DeleteDocTypeAsync(request.Id, ct);
}

public record GetWorkflowDefinitionsQuery : IRequest<List<AdminWorkflowDefinitionDto>>;
public sealed class GetWorkflowDefinitionsQueryHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<GetWorkflowDefinitionsQuery, List<AdminWorkflowDefinitionDto>>
{
    public Task<List<AdminWorkflowDefinitionDto>> Handle(GetWorkflowDefinitionsQuery request, CancellationToken ct) =>
        admin.GetWorkflowDefinitionsAsync(ct);
}

public record UpdateWorkflowDefinitionCommand(Guid Id, AdminWorkflowDefinitionDto Body) : IRequest<AdminWorkflowDefinitionDto?>;
public sealed class UpdateWorkflowDefinitionCommandHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<UpdateWorkflowDefinitionCommand, AdminWorkflowDefinitionDto?>
{
    public Task<AdminWorkflowDefinitionDto?> Handle(UpdateWorkflowDefinitionCommand request, CancellationToken ct) =>
        admin.UpdateWorkflowDefinitionAsync(request.Id, request.Body, ct);
}

public record GetPermissionPoliciesQuery : IRequest<List<AdminPermissionPolicyDto>>;
public sealed class GetPermissionPoliciesQueryHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<GetPermissionPoliciesQuery, List<AdminPermissionPolicyDto>>
{
    public Task<List<AdminPermissionPolicyDto>> Handle(GetPermissionPoliciesQuery request, CancellationToken ct) =>
        admin.GetPermissionPoliciesAsync(ct);
}

public record SavePermissionPoliciesCommand(List<AdminPermissionPolicyDto> Body) : IRequest<List<AdminPermissionPolicyDto>>;
public sealed class SavePermissionPoliciesCommandHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<SavePermissionPoliciesCommand, List<AdminPermissionPolicyDto>>
{
    public Task<List<AdminPermissionPolicyDto>> Handle(SavePermissionPoliciesCommand request, CancellationToken ct) =>
        admin.SavePermissionPoliciesAsync(request.Body, ct);
}

public record GetStorageConfigQuery : IRequest<AdminStorageConfigDto?>;
public sealed class GetStorageConfigQueryHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<GetStorageConfigQuery, AdminStorageConfigDto?>
{
    public Task<AdminStorageConfigDto?> Handle(GetStorageConfigQuery request, CancellationToken ct) =>
        admin.GetStorageConfigAsync(ct);
}

public record SaveStorageConfigCommand(AdminStorageConfigDto Body) : IRequest<AdminStorageConfigDto?>;
public sealed class SaveStorageConfigCommandHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<SaveStorageConfigCommand, AdminStorageConfigDto?>
{
    public Task<AdminStorageConfigDto?> Handle(SaveStorageConfigCommand request, CancellationToken ct) =>
        admin.SaveStorageConfigAsync(request.Body, ct);
}

public record GetAdminRolesQuery : IRequest<IReadOnlyList<string>>;
public sealed class GetAdminRolesQueryHandler(IDocumentationDmsAdminAppService admin)
    : IRequestHandler<GetAdminRolesQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(GetAdminRolesQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetAdminRoles());
}
