using Planning.Application.DTOs;
using Planning.Application.Abstractions;

namespace Planning.Infrastructure.Services.EmployeeImport;

public sealed class EmployeeImportUserPersistence(IUserService userService) : IEmployeeImportUserPersistence
{
    public async Task<EmployeeImportUserResult> CreateFromImportAsync(
        CreateUserFromImportDto dto,
        CancellationToken ct = default)
    {
        var created = await userService.CreateUserFromImportAsync(dto);
        return ToResult(created);
    }

    public async Task<EmployeeImportUserResult?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await userService.GetUserByIdAsync(userId);
        return user is null ? null : ToResult(user);
    }

    public async Task<EmployeeImportUserResult?> UpdateAsync(
        int userId,
        UpdateUserDto dto,
        CancellationToken ct = default)
    {
        var updated = await userService.UpdateUserAsync(userId, dto);
        return updated is null ? null : ToResult(updated);
    }

    private static EmployeeImportUserResult ToResult(UserDto user) => new()
    {
        Id = user.Id,
        Guid = user.Guid,
        RoleId = user.RoleId,
        SubServiceId = user.SubServiceId,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        HireDate = user.HireDate,
        IsActive = user.IsActive,
        Level = user.Level,
        AuthUserId = user.AuthUserId
    };
}
