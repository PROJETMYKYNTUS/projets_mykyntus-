namespace PlanningService.Interfaces;

using PlanningService.DTOs;

public interface IUserService
{
    Task<List<UserDto>> GetAllUsersAsync();
    Task<List<UserDto>> GetUsersBySubServiceAsync(int subServiceId);
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto dto);
    Task<bool> DeleteUserAsync(int id);
    Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null);
}