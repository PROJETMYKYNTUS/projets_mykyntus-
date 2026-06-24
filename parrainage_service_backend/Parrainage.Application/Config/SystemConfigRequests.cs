using MediatR;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Config;

public record GetSystemConfigQuery : IRequest<SystemConfigDto>;
public sealed class GetSystemConfigQueryHandler(ISystemConfigAppService config)
    : IRequestHandler<GetSystemConfigQuery, SystemConfigDto>
{
    public Task<SystemConfigDto> Handle(GetSystemConfigQuery request, CancellationToken ct) =>
        config.GetAsync(ct);
}

public record UpdateSystemConfigCommand(UpdateConfigRequest Body) : IRequest<SystemConfigDto>;
public sealed class UpdateSystemConfigCommandHandler(ISystemConfigAppService config)
    : IRequestHandler<UpdateSystemConfigCommand, SystemConfigDto>
{
    public Task<SystemConfigDto> Handle(UpdateSystemConfigCommand request, CancellationToken ct) =>
        config.UpdateAsync(request.Body, ct);
}
