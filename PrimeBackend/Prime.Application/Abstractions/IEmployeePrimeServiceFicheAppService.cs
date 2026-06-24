using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IEmployeePrimeServiceFicheAppService
{
    Task<IReadOnlyList<EmployeePrimeServiceFicheListItemDto>> ListAsync(
        string? serviceId,
        string? celluleId,
        string period,
        string supervisorUserId,
        CancellationToken ct = default);

    Task<EmployeePrimeServiceFicheResponseDto> GetForEmployeeAsync(
        string supervisorUserId,
        string employeeId,
        string period,
        string? templateId,
        CancellationToken ct = default);

    Task<EmployeePrimeServiceFicheResponseDto> UpsertAsync(
        UpsertEmployeePrimeServiceFicheRequest body,
        CancellationToken ct = default);

    Task<EmployeePrimeServiceFicheResponseDto> PersistAmountsAsync(
        Guid ficheId,
        PersistFicheAmountsRequest body,
        CancellationToken ct = default);
}
