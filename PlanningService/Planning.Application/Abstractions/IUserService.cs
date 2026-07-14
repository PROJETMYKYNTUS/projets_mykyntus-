namespace Planning.Application.Abstractions;

using Planning.Application.DTOs;

public interface IUserService
{
    Task<List<UserDto>> GetAllUsersAsync();
    Task<List<UserDto>> GetUsersBySubServiceAsync(int subServiceId);
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task<UserDto> CreateUserFromImportAsync(CreateUserFromImportDto dto);
    Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto dto);
    Task<bool> DeleteUserAsync(int id);
    Task SyncMissingAuthUsersAsync();
    Task<UserDto?> GetUserByAuthIdAsync(int authUserId);
    Task<UserDto?> GetUserByEmailAsync(string email);
    Task<UserDto?> GetOrLinkUserForAuthAsync(int authUserId, string? email);
    Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null);
    Task SyncAllEmployesToCongeAsync();
    Task<List<UserDto>> GetManagersBySubServiceAsync(int subServiceId, CancellationToken ct = default);
    Task<SetNewEmployeeStatusResultDto?> SetNewEmployeeStatusAsync(
        int id,
        SetNewEmployeeDto dto,
        CancellationToken ct = default);

    Task RollbackImportCreatedUserAsync(int planningUserId, CancellationToken ct = default);

    Task RollbackImportUpdatedUserAsync(
        int planningUserId,
        UpdateUserDto previousState,
        Dictionary<string, string?> previousCustomFields,
        CancellationToken ct = default);

    Task<UserDto?> UpdateContractualLevelAsync(
        int targetUserId,
        int level,
        Guid actorSubjectId,
        string actorRole,
        CancellationToken ct = default);

    /// <summary>
    /// Sortie complète après rejet de formation initiale : désactivation, date de sortie, sync Directory / Auth.
    /// </summary>
    Task<bool> ExitAfterInitialTrainingRejectionAsync(
        Guid employeeGuid,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Passage en production après validation RH Formation : clear EnFormation, expertise, sync Directory.
    /// </summary>
    Task<bool> CompleteInitialTrainingAsync(
        Guid employeeGuid,
        int niveauExpertiseMetier,
        DateOnly productionStartDate,
        CancellationToken ct = default);
}