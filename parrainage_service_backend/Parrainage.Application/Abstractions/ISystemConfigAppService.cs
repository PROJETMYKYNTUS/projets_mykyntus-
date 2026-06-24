using Parrainage.Application.DTOs;

namespace Parrainage.Application.Abstractions;

public interface ISystemConfigAppService
{
    Task<SystemConfigDto> GetAsync(CancellationToken ct = default);
    Task<SystemConfigDto> UpdateAsync(UpdateConfigRequest body, CancellationToken ct = default);
}
