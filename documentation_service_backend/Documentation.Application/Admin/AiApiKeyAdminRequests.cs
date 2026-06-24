using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.Admin;

public record ListAiApiKeysQuery : IRequest<List<AiApiKeyListItemResponse>>;

public sealed class ListAiApiKeysQueryHandler(IAiApiKeyAdminAppService admin)
    : IRequestHandler<ListAiApiKeysQuery, List<AiApiKeyListItemResponse>>
{
    public Task<List<AiApiKeyListItemResponse>> Handle(ListAiApiKeysQuery request, CancellationToken ct) =>
        admin.ListAsync(ct);
}

public record CreateAiApiKeyCommand(CreateAiApiKeyRequest Body) : IRequest<AiApiKeyListItemResponse>;

public sealed class CreateAiApiKeyCommandHandler(IAiApiKeyAdminAppService admin)
    : IRequestHandler<CreateAiApiKeyCommand, AiApiKeyListItemResponse>
{
    public Task<AiApiKeyListItemResponse> Handle(CreateAiApiKeyCommand request, CancellationToken ct) =>
        admin.CreateAsync(request.Body, ct);
}

public record ActivateAiApiKeyCommand(Guid Id) : IRequest;

public sealed class ActivateAiApiKeyCommandHandler(IAiApiKeyAdminAppService admin)
    : IRequestHandler<ActivateAiApiKeyCommand>
{
    public Task Handle(ActivateAiApiKeyCommand request, CancellationToken ct) =>
        admin.ActivateAsync(request.Id, ct);
}

public record DeactivateAiApiKeyCommand(Guid Id) : IRequest;

public sealed class DeactivateAiApiKeyCommandHandler(IAiApiKeyAdminAppService admin)
    : IRequestHandler<DeactivateAiApiKeyCommand>
{
    public Task Handle(DeactivateAiApiKeyCommand request, CancellationToken ct) =>
        admin.DeactivateAsync(request.Id, ct);
}

public record DeleteAiApiKeyCommand(Guid Id) : IRequest;

public sealed class DeleteAiApiKeyCommandHandler(IAiApiKeyAdminAppService admin)
    : IRequestHandler<DeleteAiApiKeyCommand>
{
    public Task Handle(DeleteAiApiKeyCommand request, CancellationToken ct) =>
        admin.DeleteAsync(request.Id, ct);
}
