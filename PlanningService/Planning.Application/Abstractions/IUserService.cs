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
    Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null);
    Task SyncAllEmployesToCongeAsync();
    Task<List<UserDto>> GetManagersBySubServiceAsync(int subServiceId, CancellationToken ct = default);
    Task<SetNewEmployeeStatusResultDto?> SetNewEmployeeStatusAsync(
        int id,
        SetNewEmployeeDto dto,
        CancellationToken ct = default);
}