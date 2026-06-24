using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IAllowanceOperationsAppService
{
    Task<List<AllowanceTypeDto>> ListTypesAsync(CancellationToken ct = default);
    Task<AllowanceTypeDto> CreateTypeAsync(CreateAllowanceTypeRequest body, CancellationToken ct = default);
    Task<List<AllowanceTypeDto>> ListEligibleTypesAsync(string? businessDepartmentId, CancellationToken ct = default);
    Task<string?> GetManagerDepartmentIdAsync(string userId, CancellationToken ct = default);
    Task<bool> IsSupportDepartmentManagerAsync(string userId, CancellationToken ct = default);

    Task<List<AllowanceRequestDto>> ListRequestsAsync(
        string userId, string role, string? departmentId, string? period, CancellationToken ct = default);
    Task<List<AllowanceRequestDto>> InboxAsync(string userId, string role, CancellationToken ct = default);
    Task<AllowanceRequestDto> CreateRequestAsync(string userId, CreateAllowanceRequestBody body, CancellationToken ct = default);
    Task<AllowanceRequestDto> UpdateDraftAsync(Guid id, string userId, UpdateAllowanceRequestBody body, CancellationToken ct = default);
    Task<AllowanceRequestDto> SubmitAsync(Guid id, string userId, CancellationToken ct = default);
    Task<AllowanceRequestDto> ApproveAsync(Guid id, string userId, string role, CancellationToken ct = default);
    Task<AllowanceRequestDto> RejectAsync(Guid id, string userId, string role, string reason, CancellationToken ct = default);

    Task<int> GenerateProposalsAsync(string period, string businessDepartmentId, string userId, CancellationToken ct = default);

    Task<AllowanceTeamProgressDto> GetTeamProgressAsync(string userId, string period, CancellationToken ct = default);
    Task<AllowanceEmployeeAllocationsDto> GetEmployeeAllocationsAsync(
        string userId, string employeeId, string period, CancellationToken ct = default);
    Task MarkNoBonusAsync(string userId, string employeeId, string period, string? comment, CancellationToken ct = default);
    Task ClearNoBonusAsync(string userId, string employeeId, string period, CancellationToken ct = default);
    Task<List<AllowanceHistoryEntryDto>> GetHistoryAsync(
        string userId, string? fromPeriod, string? toPeriod, CancellationToken ct = default);
    Task<List<AllowancePeriodSummaryDto>> GetPeriodSummariesAsync(string userId, CancellationToken ct = default);
}
