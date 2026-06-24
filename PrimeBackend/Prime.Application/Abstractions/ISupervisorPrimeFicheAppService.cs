using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface ISupervisorPrimeFicheAppService
{
    Task<SupervisorPrimeFicheResponseDto> CreateAsync(
        CreateSupervisorPrimeFicheRequest body,
        CancellationToken ct = default);

    Task<SupervisorPrimeFicheResponseDto> UpdateSaisieAsync(
        Guid id,
        UpdateSupervisorPrimeFicheSaisieRequest body,
        CancellationToken ct = default);

    Task<SupervisorPrimeFicheResponseDto> ValidateAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SupervisorPrimeFicheResponseDto>> ListAsync(
        string supervisorUserId,
        string? period,
        CancellationToken ct = default);
}
